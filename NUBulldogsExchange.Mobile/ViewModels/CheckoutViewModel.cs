using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NUBulldogsExchange.Mobile.Services;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.ViewModels;

public sealed class CheckoutViewModel : INotifyPropertyChanged
{
    public static readonly string CampusPickup = "Campus Pickup";
    public static readonly string DeliveryMethod = "Delivery";

    private readonly CartService _cart;
    private readonly AuthService _auth;
    private readonly ToastService _toast;
    private readonly AdminOrderService _adminOrders;
    private readonly AdminCustomerService _customers;
    private readonly OrderService _orders;
    private readonly ProductCatalogService _catalog;
    private readonly NotificationService _notifications;
    private readonly AdminNotificationService _adminNotifications;
    private readonly AdminPromotionService _promotions;
    private Page? _host;

    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private bool _hasError;
    private bool _isConfirmation;

    // Customer details
    private string _fullName = string.Empty;
    private string _email = string.Empty;
    private string _contactPhone = string.Empty;

    // Fulfillment
    private string _fulfillment = CampusPickup;
    private decimal _deliveryFee = 150m;

    // Delivery fields
    private string _shipRecipient = string.Empty;
    private string _shipPhone = string.Empty;
    private string _shipAddressLine = string.Empty;
    private string _shipBarangay = string.Empty;
    private string _shipCity = string.Empty;
    private string _shipProvince = "Batangas";
    private string _shipPostal = string.Empty;
    private bool _saveAddress = true;

    // Payment
    private string _paymentMethod = "Cash on Pickup";

    // Order Notes
    private string _orderNotes = string.Empty;

    // Promo
    private string _promoCodeInput = string.Empty;
    private string _promoMessage = string.Empty;
    private bool _isPromoSuccess;

    // Policy Checkbox
    private bool _agreeToTerms = true;

    // Confirmed Order
    private AdminOrder? _confirmedOrder;

    public CheckoutViewModel(
        CartService cart,
        AuthService auth,
        ToastService toast,
        AdminOrderService adminOrders,
        AdminCustomerService customers,
        OrderService orders,
        ProductCatalogService catalog,
        NotificationService notifications,
        AdminNotificationService adminNotifications,
        AdminPromotionService promotions)
    {
        _cart = cart;
        _auth = auth;
        _toast = toast;
        _adminOrders = adminOrders;
        _customers = customers;
        _orders = orders;
        _catalog = catalog;
        _notifications = notifications;
        _adminNotifications = adminNotifications;
        _promotions = promotions;

        PlaceOrderCommand = new Command(async () => await PlaceOrderAsync(), () => CanPlaceOrder);
        BackCommand = new Command(async () => await GoBackAsync());
        GoCartCommand = new Command(async () => await GoBackAsync());
        GoShopCommand = new Command(async () => await GoAsync("//shop"));
        GoOrdersCommand = new Command(async () => await GoAsync("//orders"));
        GoLoginCommand = new Command(async () => await GoLoginAsync());
        GoRegisterCommand = new Command(async () => await GoRegisterAsync());
        SelectPickupCommand = new Command(() => SetFulfillment(CampusPickup));
        SelectDeliveryCommand = new Command(() => SetFulfillment(DeliveryMethod));
        SelectPaymentMethodCommand = new Command<string>(SelectPaymentMethod);
        ApplyPromoCommand = new Command(async () => await ApplyPromoAsync());
        ToggleTermsCommand = new Command(() => AgreeToTerms = !AgreeToTerms);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Page? HostPage
    {
        get => _host;
        set => _host = value;
    }

    public ObservableCollection<CartItem> CartItems { get; } = [];

    public IReadOnlyList<string> Provinces { get; } =
    [
        "Batangas",
        "Metro Manila",
        "Laguna",
        "Cavite",
        "Rizal",
        "Bulacan",
        "Pampanga",
        "Quezon"
    ];

    public bool IsLoggedIn => _auth.IsLoggedIn;
    public bool IsGuest => !_auth.IsLoggedIn;
    public bool HasItems => _cart.Items.Count > 0;
    public bool IsEmpty => _auth.IsLoggedIn && !HasItems && !_isConfirmation;
    public bool ShowCheckoutForm => _auth.IsLoggedIn && HasItems && !_isConfirmation;
    public bool ShowLoginGate => !_auth.IsLoggedIn && !_isConfirmation;
    public bool IsConfirmation
    {
        get => _isConfirmation;
        private set
        {
            if (SetField(ref _isConfirmation, value))
            {
                OnPropertyChanged(nameof(ShowCheckoutForm));
                OnPropertyChanged(nameof(ShowLoginGate));
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    private bool CanPlaceOrder => !IsBusy && _auth.IsLoggedIn && _cart.Items.Count > 0 && AgreeToTerms;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
                ((Command)PlaceOrderCommand).ChangeCanExecute();
        }
    }

    // Customer
    public string FullName
    {
        get => _fullName;
        set => SetField(ref _fullName, value);
    }

    public string Email
    {
        get => _email;
        set => SetField(ref _email, value);
    }

    public string ContactPhone
    {
        get => _contactPhone;
        set => SetField(ref _contactPhone, value);
    }

    // Fulfillment
    public string Fulfillment
    {
        get => _fulfillment;
        private set
        {
            if (SetField(ref _fulfillment, value))
            {
                OnPropertyChanged(nameof(IsCampusPickup));
                OnPropertyChanged(nameof(IsDelivery));
                OnPropertyChanged(nameof(PickupStroke));
                OnPropertyChanged(nameof(PickupStrokeThickness));
                OnPropertyChanged(nameof(PickupBackgroundColor));
                OnPropertyChanged(nameof(DeliveryStroke));
                OnPropertyChanged(nameof(DeliveryStrokeThickness));
                OnPropertyChanged(nameof(DeliveryBackgroundColor));
                OnPropertyChanged(nameof(FulfillmentFeeText));
                OnPropertyChanged(nameof(SavingsBannerText));
                OnPropertyChanged(nameof(TotalText));
                OnPropertyChanged(nameof(GrandTotal));
                OnPropertyChanged(nameof(CashPaymentTitle));
                NotifyPaymentProperties();
            }
        }
    }

    public bool IsCampusPickup => Fulfillment == CampusPickup;
    public bool IsDelivery => Fulfillment == DeliveryMethod;

    public Color PickupStroke => IsCampusPickup ? Color.FromArgb("#00205B") : Color.FromArgb("#E2E8F0");
    public double PickupStrokeThickness => IsCampusPickup ? 2.0 : 1.0;
    public Color PickupBackgroundColor => IsCampusPickup ? Color.FromArgb("#F8FAFC") : Colors.White;

    public Color DeliveryStroke => IsDelivery ? Color.FromArgb("#00205B") : Color.FromArgb("#E2E8F0");
    public double DeliveryStrokeThickness => IsDelivery ? 2.0 : 1.0;
    public Color DeliveryBackgroundColor => IsDelivery ? Color.FromArgb("#F8FAFC") : Colors.White;

    public decimal FulfillmentFee => IsDelivery ? _deliveryFee : 0m;
    public string FulfillmentFeeText => IsDelivery ? $"₱{_deliveryFee:N0}" : "Free";
    public string SavingsBannerText => IsCampusPickup
        ? "🎉 You're saving on delivery fees! Free campus pickup at NU Lipa Campus."
        : "🚚 Estimated Delivery: 3–7 business days. We'll notify you on shipment.";

    // Shipping Fields
    public string ShipRecipient
    {
        get => _shipRecipient;
        set => SetField(ref _shipRecipient, value);
    }

    public string ShipPhone
    {
        get => _shipPhone;
        set => SetField(ref _shipPhone, value);
    }

    public string ShipAddressLine
    {
        get => _shipAddressLine;
        set => SetField(ref _shipAddressLine, value);
    }

    public string ShipBarangay
    {
        get => _shipBarangay;
        set => SetField(ref _shipBarangay, value);
    }

    public string ShipCity
    {
        get => _shipCity;
        set => SetField(ref _shipCity, value);
    }

    public string ShipProvince
    {
        get => _shipProvince;
        set => SetField(ref _shipProvince, value);
    }

    public string ShipPostal
    {
        get => _shipPostal;
        set => SetField(ref _shipPostal, value);
    }

    public bool SaveAddress
    {
        get => _saveAddress;
        set => SetField(ref _saveAddress, value);
    }

    // Payment
    public string PaymentMethod
    {
        get => _paymentMethod;
        private set
        {
            if (SetField(ref _paymentMethod, value))
            {
                NotifyPaymentProperties();
            }
        }
    }

    public void NotifyPaymentProperties()
    {
        OnPropertyChanged(nameof(IsCashSelected));
        OnPropertyChanged(nameof(IsEWalletSelected));
        OnPropertyChanged(nameof(IsOnlineSelected));
        OnPropertyChanged(nameof(IsGCashSelected));
        OnPropertyChanged(nameof(CashPaymentTitle));
        OnPropertyChanged(nameof(CashStroke));
        OnPropertyChanged(nameof(CashStrokeThickness));
        OnPropertyChanged(nameof(CashBackgroundColor));
        OnPropertyChanged(nameof(EWalletStroke));
        OnPropertyChanged(nameof(EWalletStrokeThickness));
        OnPropertyChanged(nameof(EWalletBackgroundColor));
        OnPropertyChanged(nameof(OnlineStroke));
        OnPropertyChanged(nameof(OnlineStrokeThickness));
        OnPropertyChanged(nameof(OnlineBackgroundColor));
        OnPropertyChanged(nameof(GCashStroke));
        OnPropertyChanged(nameof(GCashStrokeThickness));
        OnPropertyChanged(nameof(GCashBackgroundColor));
        OnPropertyChanged(nameof(HasOnlineNote));
    }

    public string CashPaymentTitle => IsDelivery ? "Cash on Delivery" : "Cash on Pickup";
    public bool IsCashSelected => PaymentMethod is "Cash on Pickup" or "Cash on Delivery" or "Cash";
    public bool IsEWalletSelected => PaymentMethod == "E-Wallet";
    public bool IsOnlineSelected => PaymentMethod == "Online Payment";
    public bool IsGCashSelected => PaymentMethod == "GCash";
    public bool HasOnlineNote => PaymentMethod is "E-Wallet" or "Online Payment" or "GCash";

    public Color CashStroke => IsCashSelected ? Color.FromArgb("#00205B") : Color.FromArgb("#E2E8F0");
    public double CashStrokeThickness => IsCashSelected ? 2.0 : 1.0;
    public Color CashBackgroundColor => IsCashSelected ? Color.FromArgb("#F8FAFC") : Colors.White;

    public Color EWalletStroke => IsEWalletSelected ? Color.FromArgb("#00205B") : Color.FromArgb("#E2E8F0");
    public double EWalletStrokeThickness => IsEWalletSelected ? 2.0 : 1.0;
    public Color EWalletBackgroundColor => IsEWalletSelected ? Color.FromArgb("#F8FAFC") : Colors.White;

    public Color OnlineStroke => IsOnlineSelected ? Color.FromArgb("#00205B") : Color.FromArgb("#E2E8F0");
    public double OnlineStrokeThickness => IsOnlineSelected ? 2.0 : 1.0;
    public Color OnlineBackgroundColor => IsOnlineSelected ? Color.FromArgb("#F8FAFC") : Colors.White;

    public Color GCashStroke => IsGCashSelected ? Color.FromArgb("#00205B") : Color.FromArgb("#E2E8F0");
    public double GCashStrokeThickness => IsGCashSelected ? 2.0 : 1.0;
    public Color GCashBackgroundColor => IsGCashSelected ? Color.FromArgb("#F8FAFC") : Colors.White;

    // Order Notes
    public string OrderNotes
    {
        get => _orderNotes;
        set => SetField(ref _orderNotes, value);
    }

    // Totals
    public int ItemCount => _cart.TotalCount;
    public string ItemCountText => $"({ItemCount} item{(ItemCount == 1 ? "" : "s")})";
    public decimal Subtotal => _cart.Subtotal;
    public string SubtotalText => $"₱{Subtotal:N0}";
    public decimal Discount => _cart.AppliedDiscount;
    public bool HasDiscount => Discount > 0;
    public string DiscountText => HasDiscount ? $"-₱{Discount:N0}" : "—";
    public decimal GrandTotal => Math.Max(0, Subtotal - Discount + FulfillmentFee);
    public string TotalText => $"₱{GrandTotal:N0}";

    // Promo
    public string PromoCodeInput
    {
        get => _promoCodeInput;
        set => SetField(ref _promoCodeInput, value);
    }

    public string PromoMessage
    {
        get => _promoMessage;
        private set
        {
            if (SetField(ref _promoMessage, value))
                OnPropertyChanged(nameof(HasPromoMessage));
        }
    }

    public bool HasPromoMessage => !string.IsNullOrWhiteSpace(PromoMessage);

    public bool IsPromoSuccess
    {
        get => _isPromoSuccess;
        private set => SetField(ref _isPromoSuccess, value);
    }

    public bool AgreeToTerms
    {
        get => _agreeToTerms;
        set
        {
            if (SetField(ref _agreeToTerms, value))
                ((Command)PlaceOrderCommand).ChangeCanExecute();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetField(ref _statusMessage, value))
                OnPropertyChanged(nameof(HasStatus));
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HasError
    {
        get => _hasError;
        private set => SetField(ref _hasError, value);
    }

    // Confirmed Order details
    public AdminOrder? ConfirmedOrder
    {
        get => _confirmedOrder;
        private set
        {
            if (SetField(ref _confirmedOrder, value))
            {
                OnPropertyChanged(nameof(ConfirmedOrderId));
                OnPropertyChanged(nameof(ConfirmedOrderTotal));
                OnPropertyChanged(nameof(ConfirmedOrderPayment));
                OnPropertyChanged(nameof(ConfirmedOrderFulfillment));
                OnPropertyChanged(nameof(ConfirmedOrderAddress));
                OnPropertyChanged(nameof(HasConfirmedOrderAddress));
            }
        }
    }

    public string ConfirmedOrderId => ConfirmedOrder is not null ? $"#{ConfirmedOrder.Id}" : string.Empty;
    public string ConfirmedOrderTotal => ConfirmedOrder is not null ? $"₱{ConfirmedOrder.Total:N0}" : "₱0";
    public string ConfirmedOrderPayment => ConfirmedOrder?.PaymentMethod ?? string.Empty;
    public string ConfirmedOrderFulfillment => ConfirmedOrder?.Fulfillment ?? string.Empty;
    public string ConfirmedOrderAddress => ConfirmedOrder is not null && !string.IsNullOrWhiteSpace(ConfirmedOrder.ShippingAddressLine)
        ? $"{ConfirmedOrder.ShippingAddressLine}, {ConfirmedOrder.ShippingBarangay}, {ConfirmedOrder.ShippingCity}, {ConfirmedOrder.ShippingProvince}"
        : "NU Lipa Campus Merchandise Desk";
    public bool HasConfirmedOrderAddress => ConfirmedOrder is not null;

    // Commands
    public ICommand PlaceOrderCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand GoCartCommand { get; }
    public ICommand GoShopCommand { get; }
    public ICommand GoOrdersCommand { get; }
    public ICommand GoLoginCommand { get; }
    public ICommand GoRegisterCommand { get; }
    public ICommand SelectPickupCommand { get; }
    public ICommand SelectDeliveryCommand { get; }
    public ICommand SelectPaymentMethodCommand { get; }
    public ICommand ApplyPromoCommand { get; }
    public ICommand ToggleTermsCommand { get; }

    public void Refresh()
    {
        IsConfirmation = false;
        ConfirmedOrder = null;

        if (_auth.IsLoggedIn)
        {
            MobileCheckoutIntent.Clear();
            var u = _auth.CurrentUser;
            FullName = u?.Name ?? string.Empty;
            Email = u?.Email ?? string.Empty;
            if (string.IsNullOrWhiteSpace(ContactPhone))
                ContactPhone = u?.Phone ?? string.Empty;

            if (string.IsNullOrWhiteSpace(ShipRecipient))
                ShipRecipient = FullName;
            if (string.IsNullOrWhiteSpace(ShipPhone))
                ShipPhone = ContactPhone;
        }

        CartItems.Clear();
        foreach (var item in _cart.Items)
            CartItems.Add(item);

        if (!string.IsNullOrWhiteSpace(_cart.AppliedPromoCode))
        {
            PromoCodeInput = _cart.AppliedPromoCode;
            PromoMessage = $"Code '{_cart.AppliedPromoCode}' applied (-₱{_cart.AppliedDiscount:N0})";
            IsPromoSuccess = true;
        }

        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsGuest));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ShowCheckoutForm));
        OnPropertyChanged(nameof(ShowLoginGate));
        OnPropertyChanged(nameof(ItemCount));
        OnPropertyChanged(nameof(ItemCountText));
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(SubtotalText));
        OnPropertyChanged(nameof(Discount));
        OnPropertyChanged(nameof(HasDiscount));
        OnPropertyChanged(nameof(DiscountText));
        OnPropertyChanged(nameof(GrandTotal));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(FulfillmentFeeText));
        OnPropertyChanged(nameof(SavingsBannerText));
        OnPropertyChanged(nameof(IsCampusPickup));
        OnPropertyChanged(nameof(IsDelivery));
        OnPropertyChanged(nameof(PickupStroke));
        OnPropertyChanged(nameof(PickupStrokeThickness));
        OnPropertyChanged(nameof(PickupBackgroundColor));
        OnPropertyChanged(nameof(DeliveryStroke));
        OnPropertyChanged(nameof(DeliveryStrokeThickness));
        OnPropertyChanged(nameof(DeliveryBackgroundColor));
        NotifyPaymentProperties();
        ((Command)PlaceOrderCommand).ChangeCanExecute();
    }

    public void SetFulfillment(string method)
    {
        Fulfillment = method;
        if (IsDelivery && PaymentMethod == "Cash on Pickup")
            PaymentMethod = "Cash on Delivery";
        else if (IsCampusPickup && PaymentMethod == "Cash on Delivery")
            PaymentMethod = "Cash on Pickup";
    }

    public void SelectPaymentMethod(string method)
    {
        if (method == "Cash")
            PaymentMethod = IsDelivery ? "Cash on Delivery" : "Cash on Pickup";
        else
            PaymentMethod = method;
    }

    public async Task ApplyPromoAsync()
    {
        if (string.IsNullOrWhiteSpace(PromoCodeInput))
        {
            PromoMessage = "Please enter a promotion code.";
            IsPromoSuccess = false;
            return;
        }

        try
        {
            await _promotions.EnsureLoadedAsync();
            var promoCartItems = _cart.Items.Select(i => new PromoCartItem
            {
                ProductId = i.Product.Id,
                Category = i.Product.Category,
                UnitPrice = i.Product.Price,
                Quantity = i.Quantity
            });

            var result = await _promotions.ValidateForCartAsync(
                PromoCodeInput.Trim(),
                promoCartItems,
                _auth.Email,
                _auth.CurrentUser?.UserId);

            if (result.Success && result.Promo is not null)
            {
                _cart.SetPromo(result.Promo.Code, result.Discount);
                PromoMessage = $"Promo '{result.Promo.Code}' applied! You saved ₱{result.Discount:N0}.";
                IsPromoSuccess = true;
                _toast.Show(PromoMessage);
            }
            else
            {
                PromoMessage = string.IsNullOrWhiteSpace(result.Message) ? "Invalid promo code." : result.Message;
                IsPromoSuccess = false;
                _cart.ClearPromo();
            }
        }
        catch
        {
            PromoMessage = "Unable to validate promo code.";
            IsPromoSuccess = false;
        }

        OnPropertyChanged(nameof(Discount));
        OnPropertyChanged(nameof(HasDiscount));
        OnPropertyChanged(nameof(DiscountText));
        OnPropertyChanged(nameof(GrandTotal));
        OnPropertyChanged(nameof(TotalText));
    }

    private async Task GoLoginAsync()
    {
        MobileCheckoutIntent.SetPending();
        await GoAsync("login");
    }

    private async Task GoRegisterAsync()
    {
        MobileCheckoutIntent.SetPending();
        await GoAsync("register");
    }

    private async Task PlaceOrderAsync()
    {
        if (!_auth.IsLoggedIn)
        {
            StatusMessage = "Please sign in to place an order.";
            HasError = true;
            return;
        }

        if (IsBusy || _auth.CurrentUser is null || !_cart.Items.Any())
            return;

        // Validation
        if (string.IsNullOrWhiteSpace(ContactPhone))
        {
            StatusMessage = "Please provide your contact number.";
            HasError = true;
            return;
        }

        if (IsDelivery)
        {
            if (string.IsNullOrWhiteSpace(ShipRecipient) ||
                string.IsNullOrWhiteSpace(ShipPhone) ||
                string.IsNullOrWhiteSpace(ShipAddressLine) ||
                string.IsNullOrWhiteSpace(ShipBarangay) ||
                string.IsNullOrWhiteSpace(ShipCity) ||
                string.IsNullOrWhiteSpace(ShipPostal))
            {
                StatusMessage = "Please fill in all required shipping address fields.";
                HasError = true;
                return;
            }
        }

        if (!AgreeToTerms)
        {
            StatusMessage = "Please agree to the store policies and Terms of Service.";
            HasError = true;
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        HasError = false;
        try
        {
            await _adminOrders.EnsureLoadedAsync();
            await _customers.EnsureLoadedAsync();

            var user = _auth.CurrentUser;
            var customer = _customers.EnsureCustomer(user.Name, user.Email, ContactPhone, user.UserId);
            var promoCode = _cart.AppliedPromoCode;

            var order = new AdminOrder
            {
                Id = string.Empty,
                CustomerId = user.UserId > 0 ? user.UserId.ToString() : customer.Id,
                CustomerName = user.Name,
                CustomerEmail = user.Email,
                CustomerPhone = ContactPhone,
                Date = DateTime.Now,
                Subtotal = Subtotal,
                DiscountAmount = Discount,
                ShippingFee = FulfillmentFee,
                Total = GrandTotal,
                PromotionCode = promoCode,
                PaymentStatus = "Pending",
                PaymentMethod = PaymentMethod,
                Fulfillment = Fulfillment,
                Status = "Pending",
                OrderNotes = string.IsNullOrWhiteSpace(OrderNotes) ? null : OrderNotes.Trim(),
                ShippingRecipientName = IsDelivery ? ShipRecipient : null,
                ShippingPhone = IsDelivery ? ShipPhone : null,
                ShippingAddressLine = IsDelivery ? ShipAddressLine : null,
                ShippingBarangay = IsDelivery ? ShipBarangay : null,
                ShippingCity = IsDelivery ? ShipCity : null,
                ShippingProvince = IsDelivery ? ShipProvince : null,
                ShippingPostalCode = IsDelivery ? ShipPostal : null,
                Items = _cart.Items.Select(i => new AdminOrderItem
                {
                    ProductId = i.Product.Id,
                    Name = i.Product.Name,
                    ImageUrl = i.Product.ImageUrl,
                    Quantity = i.Quantity,
                    Price = i.Product.Price,
                    VariantId = i.VariantId,
                    Size = i.SelectedSize,
                    VariantSku = i.VariantId is int vid
                        ? i.Product.Variants.FirstOrDefault(v => v.Id == vid)?.Sku
                        : null
                }).ToList()
            };

            order = await _adminOrders.AddAsync(order, promoCode, Discount);
            _orders.PlaceOrder(order);
            _catalog.ApplyPurchase(order.Items);

            await _notifications.AddAsync(new MockNotification
            {
                Title = "Order Confirmed",
                Message = $"Your order #{order.Id} has been placed and is being processed.",
                TimeAgo = "Just now",
                Icon = "check-circle",
                Tone = "green",
                IsRead = false
            });

            await _adminNotifications.AddAsync(new AdminNotificationItem
            {
                Type = "order",
                Title = "New Order Received",
                Message = $"Order #{order.Id} placed by {order.CustomerName} — ₱{order.Total:N0}.",
                RelatedId = order.Id,
                RelatedLabel = order.Id,
                RelatedHref = $"/admin/orders/{order.Id}",
                Timestamp = DateTime.Now,
                Read = false
            });

            _toast.Show($"Order {order.Id} placed successfully!");
            _cart.Clear();
            await _cart.PersistAsync(user.Email);
            MobileCheckoutIntent.Clear();

            ConfirmedOrder = order;
            IsConfirmation = true;
        }
        catch (InvalidOperationException ex)
        {
            HasError = true;
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = "Unable to place order. Please try again.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static async Task GoAsync(string route)
    {
        try { await Shell.Current.GoToAsync(route); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    private static async Task GoBackAsync()
    {
        MobileCheckoutIntent.Clear();
        try
        {
            if (Shell.Current.Navigation.NavigationStack.Count > 1)
                await Shell.Current.GoToAsync("..");
            else
                await Shell.Current.GoToAsync("cart");
        }
        catch
        {
            await Shell.Current.GoToAsync("cart");
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
