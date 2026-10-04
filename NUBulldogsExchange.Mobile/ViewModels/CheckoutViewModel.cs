using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NUBulldogsExchange.Mobile.Models;
using NUBulldogsExchange.Mobile.Services;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.ViewModels;

public sealed class ConfirmedOrderItemViewModel
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal Price { get; set; }
    public string? Size { get; set; }
    public decimal ItemTotal => Price * Quantity;
    public string FormattedPrice => $"₱{ItemTotal:N0}";
    public string QuantityLabel => $"Qty: {Quantity}";
    public bool HasSize => !string.IsNullOrWhiteSpace(Size);
    public string SizeLabel => HasSize ? $"Size: {Size}" : string.Empty;
}

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
    private readonly IAppDatabase _db;
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
        AdminPromotionService promotions,
        IAppDatabase db)
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
        _db = db;

        PlaceOrderCommand = new Command(async () => await PlaceOrderAsync(), () => CanPlaceOrder);
        BackCommand = new Command(async () => await GoBackAsync());
        GoCartCommand = new Command(async () => await GoBackAsync());
        GoShopCommand = new Command(async () => await GoAsync("//shop"));
        GoOrdersCommand = new Command(async () => await GoAsync("//orders"));
        TrackOrderCommand = new Command(async () => await GoAsync("//orders"));
        GoLoginCommand = new Command(async () => await GoLoginAsync());
        GoRegisterCommand = new Command(async () => await GoRegisterAsync());
        SelectPickupCommand = new Command(() => SetFulfillment(CampusPickup));
        SelectDeliveryCommand = new Command(() => SetFulfillment(DeliveryMethod));
        SelectPaymentMethodCommand = new Command<string>(SelectPaymentMethod);
        ApplyPromoCommand = new Command(async () => await ApplyPromoAsync());
        ToggleTermsCommand = new Command(() => AgreeToTerms = !AgreeToTerms);
        SelectSavedAddressModeCommand = new Command(SelectSavedAddressMode);
        SelectNewAddressModeCommand = new Command(SelectNewAddressMode);
        ToggleSaveAddressCommand = new Command(() => SaveAddress = !SaveAddress);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Page? HostPage
    {
        get => _host;
        set => _host = value;
    }

    public ObservableCollection<CartItem> CartItems { get; } = [];
    public ObservableCollection<ConfirmedOrderItemViewModel> ConfirmedItems { get; } = [];

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
        ? "You're saving on delivery fees! Free campus pickup at NU Lipa Campus."
        : "Estimated Delivery: 3–7 business days. We'll notify you on shipment.";

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
        set
        {
            if (SetField(ref _saveAddress, value))
            {
                OnPropertyChanged(nameof(SaveAddressBoxBg));
                OnPropertyChanged(nameof(SaveAddressBoxStroke));
            }
        }
    }

    // Saved Addresses
    public ObservableCollection<SavedAddressItemModel> SavedAddresses { get; } = [];

    private SavedAddressItemModel? _selectedSavedAddress;
    public SavedAddressItemModel? SelectedSavedAddress
    {
        get => _selectedSavedAddress;
        set
        {
            if (SetField(ref _selectedSavedAddress, value))
            {
                OnPropertyChanged(nameof(HasSelectedSavedAddress));
                if (value is not null && IsUseSavedAddress)
                {
                    ApplySavedAddress(value);
                }
            }
        }
    }

    public bool HasSelectedSavedAddress => SelectedSavedAddress is not null;

    private bool _hasSavedAddresses;
    public bool HasSavedAddresses
    {
        get => _hasSavedAddresses;
        private set
        {
            if (SetField(ref _hasSavedAddresses, value))
            {
                OnPropertyChanged(nameof(ShowSavedAddressSection));
                OnPropertyChanged(nameof(ShowNewAddressSection));
            }
        }
    }

    private bool _isUseSavedAddress;
    public bool IsUseSavedAddress
    {
        get => _isUseSavedAddress;
        set
        {
            if (SetField(ref _isUseSavedAddress, value))
            {
                OnPropertyChanged(nameof(IsAddNewAddress));
                OnPropertyChanged(nameof(UseSavedAddressRadioStroke));
                OnPropertyChanged(nameof(AddNewAddressRadioStroke));
                OnPropertyChanged(nameof(ShowSavedAddressSection));
                OnPropertyChanged(nameof(ShowNewAddressSection));
            }
        }
    }

    public bool IsAddNewAddress => !IsUseSavedAddress;
    public bool ShowSavedAddressSection => HasSavedAddresses && IsUseSavedAddress;
    public bool ShowNewAddressSection => !HasSavedAddresses || IsAddNewAddress;

    public Color UseSavedAddressRadioStroke => IsUseSavedAddress ? Color.FromArgb("#00205B") : Color.FromArgb("#CBD5E1");
    public Color AddNewAddressRadioStroke => IsAddNewAddress ? Color.FromArgb("#00205B") : Color.FromArgb("#CBD5E1");

    public Color SaveAddressBoxBg => SaveAddress ? Color.FromArgb("#00205B") : Colors.White;
    public Color SaveAddressBoxStroke => SaveAddress ? Color.FromArgb("#00205B") : Color.FromArgb("#CBD5E1");

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
        OnPropertyChanged(nameof(IsGCashSelected));
        OnPropertyChanged(nameof(IsMayaSelected));
        OnPropertyChanged(nameof(IsCreditCardSelected));
        OnPropertyChanged(nameof(IsEWalletSelected));
        OnPropertyChanged(nameof(IsOnlineSelected));
        OnPropertyChanged(nameof(CashPaymentTitle));
        OnPropertyChanged(nameof(CashStroke));
        OnPropertyChanged(nameof(CashStrokeThickness));
        OnPropertyChanged(nameof(CashBackgroundColor));
        OnPropertyChanged(nameof(CashCheckBg));
        OnPropertyChanged(nameof(GCashStroke));
        OnPropertyChanged(nameof(GCashStrokeThickness));
        OnPropertyChanged(nameof(GCashBackgroundColor));
        OnPropertyChanged(nameof(GCashCheckBg));
        OnPropertyChanged(nameof(MayaStroke));
        OnPropertyChanged(nameof(MayaStrokeThickness));
        OnPropertyChanged(nameof(MayaBackgroundColor));
        OnPropertyChanged(nameof(MayaCheckBg));
        OnPropertyChanged(nameof(CreditCardStroke));
        OnPropertyChanged(nameof(CreditCardStrokeThickness));
        OnPropertyChanged(nameof(CreditCardBackgroundColor));
        OnPropertyChanged(nameof(CreditCardCheckBg));
        OnPropertyChanged(nameof(EWalletStroke));
        OnPropertyChanged(nameof(EWalletStrokeThickness));
        OnPropertyChanged(nameof(EWalletBackgroundColor));
        OnPropertyChanged(nameof(OnlineStroke));
        OnPropertyChanged(nameof(OnlineStrokeThickness));
        OnPropertyChanged(nameof(OnlineBackgroundColor));
        OnPropertyChanged(nameof(HasOnlineNote));
        OnPropertyChanged(nameof(OnlineNoteText));
    }

    public string CashPaymentTitle => IsDelivery ? "Cash on Delivery" : "Cash on Pickup";
    public bool IsCashSelected => PaymentMethod is "Cash on Pickup" or "Cash on Delivery" or "Cash";
    public bool IsGCashSelected => PaymentMethod == "GCash";
    public bool IsMayaSelected => PaymentMethod == "Maya";
    public bool IsCreditCardSelected => PaymentMethod == "Credit Card";
    public bool IsEWalletSelected => PaymentMethod is "E-Wallet" or "Maya";
    public bool IsOnlineSelected => PaymentMethod is "Online Payment" or "Credit Card";
    public bool HasOnlineNote => PaymentMethod is "GCash" or "Maya" or "Credit Card" or "E-Wallet" or "Online Payment";

    public string OnlineNoteText => PaymentMethod switch
    {
        "Credit Card" => "This payment method is currently running in development/test mode. Card numbers, CVV, and PINs are not collected or stored.",
        _ => "Verification or payment QR will be confirmed upon placing your order."
    };

    private static readonly Color SelectedStrokeColor = Color.FromArgb("#0A2540");
    private static readonly Color UnselectedStrokeColor = Color.FromArgb("#E2E8F0");
    private static readonly Color UnselectedCheckBg = Color.FromArgb("#E2E8F0");

    public Color CashStroke => IsCashSelected ? SelectedStrokeColor : UnselectedStrokeColor;
    public double CashStrokeThickness => IsCashSelected ? 1.5 : 1.0;
    public Color CashBackgroundColor => Colors.White;
    public Color CashCheckBg => IsCashSelected ? SelectedStrokeColor : UnselectedCheckBg;

    public Color GCashStroke => IsGCashSelected ? SelectedStrokeColor : UnselectedStrokeColor;
    public double GCashStrokeThickness => IsGCashSelected ? 1.5 : 1.0;
    public Color GCashBackgroundColor => Colors.White;
    public Color GCashCheckBg => IsGCashSelected ? SelectedStrokeColor : UnselectedCheckBg;

    public Color MayaStroke => IsMayaSelected ? SelectedStrokeColor : UnselectedStrokeColor;
    public double MayaStrokeThickness => IsMayaSelected ? 1.5 : 1.0;
    public Color MayaBackgroundColor => Colors.White;
    public Color MayaCheckBg => IsMayaSelected ? SelectedStrokeColor : UnselectedCheckBg;

    public Color CreditCardStroke => IsCreditCardSelected ? SelectedStrokeColor : UnselectedStrokeColor;
    public double CreditCardStrokeThickness => IsCreditCardSelected ? 1.5 : 1.0;
    public Color CreditCardBackgroundColor => Colors.White;
    public Color CreditCardCheckBg => IsCreditCardSelected ? SelectedStrokeColor : UnselectedCheckBg;

    public Color EWalletStroke => IsEWalletSelected ? SelectedStrokeColor : UnselectedStrokeColor;
    public double EWalletStrokeThickness => IsEWalletSelected ? 1.5 : 1.0;
    public Color EWalletBackgroundColor => Colors.White;

    public Color OnlineStroke => IsOnlineSelected ? SelectedStrokeColor : UnselectedStrokeColor;
    public double OnlineStrokeThickness => IsOnlineSelected ? 1.5 : 1.0;
    public Color OnlineBackgroundColor => Colors.White;

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
                OnPropertyChanged(nameof(ConfirmedOrderDate));
                OnPropertyChanged(nameof(ConfirmedOrderTotal));
                OnPropertyChanged(nameof(ConfirmedOrderPayment));
                OnPropertyChanged(nameof(ConfirmedOrderPaymentStatus));
                OnPropertyChanged(nameof(ConfirmedOrderFulfillment));
                OnPropertyChanged(nameof(IsConfirmedDelivery));
                OnPropertyChanged(nameof(IsConfirmedCampusPickup));
                OnPropertyChanged(nameof(IsPaymentPaid));
                OnPropertyChanged(nameof(PaymentBadgeBg));
                OnPropertyChanged(nameof(PaymentBadgeTextColor));
                OnPropertyChanged(nameof(ConfirmedRecipientName));
                OnPropertyChanged(nameof(ConfirmedPhone));
                OnPropertyChanged(nameof(ConfirmedAddress));
            }
        }
    }

    public string ConfirmedOrderId => ConfirmedOrder is not null ? ConfirmedOrder.Id : string.Empty;
    public string ConfirmedOrderDate => ConfirmedOrder?.Date.ToString("MMMM d, yyyy") ?? DateTime.Now.ToString("MMMM d, yyyy");
    public string ConfirmedOrderTotal => ConfirmedOrder is not null ? $"₱{ConfirmedOrder.Total:N0}" : $"₱{GrandTotal:N0}";
    public string ConfirmedOrderPayment => !string.IsNullOrWhiteSpace(ConfirmedOrder?.PaymentMethod) ? ConfirmedOrder.PaymentMethod : PaymentMethod;
    public string ConfirmedOrderPaymentStatus => ConfirmedOrder?.PaymentStatus ?? "Pending";
    public string ConfirmedOrderFulfillment => ConfirmedOrder?.Fulfillment ?? Fulfillment;

    public bool IsConfirmedDelivery => string.Equals(ConfirmedOrderFulfillment, DeliveryMethod, StringComparison.OrdinalIgnoreCase);
    public bool IsConfirmedCampusPickup => !IsConfirmedDelivery;

    public bool IsPaymentPaid => string.Equals(ConfirmedOrderPaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase);
    public Color PaymentBadgeBg => IsPaymentPaid ? Color.FromArgb("#DCFCE7") : Color.FromArgb("#FEF3C7");
    public Color PaymentBadgeTextColor => IsPaymentPaid ? Color.FromArgb("#15803D") : Color.FromArgb("#D97706");

    public string ConfirmedRecipientName => ConfirmedOrder?.ShippingRecipientName ?? (string.IsNullOrWhiteSpace(ShipRecipient) ? FullName : ShipRecipient);
    public string ConfirmedPhone => ConfirmedOrder?.ShippingPhone ?? (string.IsNullOrWhiteSpace(ShipPhone) ? ContactPhone : ShipPhone);

    public string ConfirmedAddress
    {
        get
        {
            if (ConfirmedOrder is not null)
            {
                var parts = new[]
                {
                    ConfirmedOrder.ShippingAddressLine,
                    ConfirmedOrder.ShippingBarangay,
                    string.Join(" ", new[] { ConfirmedOrder.ShippingCity, ConfirmedOrder.ShippingProvince, ConfirmedOrder.ShippingPostalCode }.Where(v => !string.IsNullOrWhiteSpace(v)))
                };
                var formatted = string.Join(", ", parts.Where(v => !string.IsNullOrWhiteSpace(v)));
                if (!string.IsNullOrWhiteSpace(formatted))
                    return formatted;
            }

            var fallback = new[]
            {
                ShipAddressLine,
                ShipBarangay,
                string.Join(" ", new[] { ShipCity, ShipProvince, ShipPostal }.Where(v => !string.IsNullOrWhiteSpace(v)))
            };
            var result = string.Join(", ", fallback.Where(v => !string.IsNullOrWhiteSpace(v)));
            return string.IsNullOrWhiteSpace(result) ? "NU Lipa Campus Merchandise Desk" : result;
        }
    }

    // Commands
    public ICommand PlaceOrderCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand GoCartCommand { get; }
    public ICommand GoShopCommand { get; }
    public ICommand GoOrdersCommand { get; }
    public ICommand TrackOrderCommand { get; }
    public ICommand GoLoginCommand { get; }
    public ICommand GoRegisterCommand { get; }
    public ICommand SelectPickupCommand { get; }
    public ICommand SelectDeliveryCommand { get; }
    public ICommand SelectPaymentMethodCommand { get; }
    public ICommand ApplyPromoCommand { get; }
    public ICommand ToggleTermsCommand { get; }
    public ICommand SelectSavedAddressModeCommand { get; }
    public ICommand SelectNewAddressModeCommand { get; }
    public ICommand ToggleSaveAddressCommand { get; }

    public void Refresh()
    {
        IsConfirmation = false;
        ConfirmedOrder = null;
        ConfirmedItems.Clear();

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

            _ = LoadSavedAddressesAsync();
        }
        else
        {
            SavedAddresses.Clear();
            HasSavedAddresses = false;
            IsUseSavedAddress = false;
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
        OnPropertyChanged(nameof(ShowSavedAddressSection));
        OnPropertyChanged(nameof(ShowNewAddressSection));
        NotifyPaymentProperties();
        ((Command)PlaceOrderCommand).ChangeCanExecute();
    }

    public void SelectSavedAddressMode()
    {
        IsUseSavedAddress = true;
        if (SelectedSavedAddress is not null)
        {
            ApplySavedAddress(SelectedSavedAddress);
        }
        else if (SavedAddresses.Count > 0)
        {
            SelectedSavedAddress = SavedAddresses[0];
            ApplySavedAddress(SelectedSavedAddress);
        }
    }

    public void SelectNewAddressMode()
    {
        IsUseSavedAddress = false;
        if (string.IsNullOrWhiteSpace(ShipRecipient))
            ShipRecipient = FullName;
        if (string.IsNullOrWhiteSpace(ShipPhone))
            ShipPhone = ContactPhone;
    }

    private void ApplySavedAddress(SavedAddressItemModel item)
    {
        ShipRecipient = item.RecipientName;
        ShipPhone = item.PhoneNumber;
        ShipAddressLine = item.AddressLine;
        ShipBarangay = item.Barangay;
        ShipCity = item.City;
        ShipProvince = string.IsNullOrWhiteSpace(item.Province) ? "Batangas" : item.Province;
        ShipPostal = item.PostalCode;
    }

    public async Task LoadSavedAddressesAsync()
    {
        if (!_auth.IsLoggedIn || _auth.CurrentUser is null)
        {
            SavedAddresses.Clear();
            HasSavedAddresses = false;
            IsUseSavedAddress = false;
            return;
        }

        var userId = _auth.CurrentUser.Id;
        if (userId == Guid.Empty)
        {
            SavedAddresses.Clear();
            HasSavedAddresses = false;
            IsUseSavedAddress = false;
            return;
        }

        try
        {
            var addresses = await _db.GetUserAddressesAsync(userId);
            SavedAddresses.Clear();
            foreach (var addr in addresses)
            {
                SavedAddresses.Add(new SavedAddressItemModel(addr));
            }

            HasSavedAddresses = SavedAddresses.Count > 0;
            if (HasSavedAddresses)
            {
                var preferred = SavedAddresses.FirstOrDefault(a => a.Address.IsDefault) ?? SavedAddresses[0];
                SelectedSavedAddress = preferred;
                IsUseSavedAddress = true;
                ApplySavedAddress(preferred);
            }
            else
            {
                IsUseSavedAddress = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load saved addresses: {ex.Message}");
            SavedAddresses.Clear();
            HasSavedAddresses = false;
            IsUseSavedAddress = false;
        }
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
        if (method is "Cash" or "Cash on Pickup" or "Cash on Delivery")
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

    private void PopulateConfirmedItems(AdminOrder order)
    {
        ConfirmedItems.Clear();
        if (order.Items != null && order.Items.Count > 0)
        {
            foreach (var item in order.Items)
            {
                ConfirmedItems.Add(new ConfirmedOrderItemViewModel
                {
                    ProductId = item.ProductId,
                    Name = item.Name,
                    ImageUrl = item.ImageUrl,
                    Quantity = item.Quantity,
                    Price = item.Price,
                    Size = item.Size
                });
            }
        }
        else
        {
            foreach (var cartItem in _cart.Items)
            {
                ConfirmedItems.Add(new ConfirmedOrderItemViewModel
                {
                    ProductId = cartItem.Product.Id,
                    Name = cartItem.Product.Name,
                    ImageUrl = cartItem.Product.ImageUrl,
                    Quantity = cartItem.Quantity,
                    Price = cartItem.Product.Price,
                    Size = cartItem.SelectedSize
                });
            }
        }
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
        foreach (var item in _cart.Items)
        {
            if (item.AvailableStock <= 0)
            {
                StatusMessage = $"{item.Product.Name} is out of stock. Please return to your cart and remove it.";
                HasError = true;
                return;
            }

            if (item.Quantity > item.AvailableStock)
            {
                StatusMessage = $"Only {item.AvailableStock} pieces of {item.Product.Name} available. Please reduce quantity in cart.";
                HasError = true;
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(ContactPhone))
        {
            StatusMessage = "Please provide your contact number.";
            HasError = true;
            return;
        }

        if (IsDelivery)
        {
            if (ShowSavedAddressSection)
            {
                if (SelectedSavedAddress is null)
                {
                    StatusMessage = "Please select a saved shipping address.";
                    HasError = true;
                    return;
                }
                ApplySavedAddress(SelectedSavedAddress);
            }
            else
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
            var customer = _customers.EnsureCustomer(
                string.IsNullOrWhiteSpace(FullName) ? user.Name : FullName.Trim(),
                string.IsNullOrWhiteSpace(Email) ? user.Email : Email.Trim(),
                ContactPhone.Trim(),
                user.UserId);
            var promoCode = _cart.AppliedPromoCode;
            var isDelivery = IsDelivery;

            var draft = new AdminOrder
            {
                Id = string.Empty,
                CustomerId = user.UserId > 0 ? user.UserId.ToString() : (customer?.Id ?? user.UserId.ToString()),
                CustomerName = string.IsNullOrWhiteSpace(FullName) ? user.Name : FullName.Trim(),
                CustomerEmail = string.IsNullOrWhiteSpace(Email) ? user.Email : Email.Trim(),
                CustomerPhone = ContactPhone.Trim(),
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
                ShippingRecipientName = isDelivery ? (string.IsNullOrWhiteSpace(ShipRecipient) ? FullName.Trim() : ShipRecipient.Trim()) : null,
                ShippingPhone = isDelivery ? (string.IsNullOrWhiteSpace(ShipPhone) ? ContactPhone.Trim() : ShipPhone.Trim()) : null,
                ShippingAddressLine = isDelivery ? ShipAddressLine?.Trim() : null,
                ShippingBarangay = isDelivery ? ShipBarangay?.Trim() : null,
                ShippingCity = isDelivery ? ShipCity?.Trim() : null,
                ShippingProvince = isDelivery ? ShipProvince?.Trim() : null,
                ShippingPostalCode = isDelivery ? ShipPostal?.Trim() : null,
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

            AdminOrder order;
            try
            {
                order = await _adminOrders.AddAsync(draft, promoCode, Discount);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during AddAsync: {ex}");
                throw;
            }

            // Confirmed state
            ConfirmedOrder = order;
            PopulateConfirmedItems(order);
            IsConfirmation = true;

            // Secondary tasks (isolated so checkout confirmation is never blocked)
            try { _orders.PlaceOrder(order); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try { _catalog.ApplyPurchase(order.Items); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            try
            {
                await _notifications.AddAsync(new MockNotification
                {
                    Title = "Order Placed",
                    Message = $"Your order {order.Id} has been placed and will be confirmed soon.",
                    TimeAgo = OrderFlow.FormatNotificationTime(DateTime.Now),
                    Icon = "check-circle",
                    Tone = "green",
                    IsRead = false,
                    RelatedId = order.Id,
                    RelatedHref = "/orders"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }

            try
            {
                _toast.Show($"Order {order.Id} placed successfully!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }

            try
            {
                _cart.Clear();
                await _cart.PersistAsync(user.Email);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }

            if (isDelivery && SaveAddress && ShowNewAddressSection && user.Id != Guid.Empty)
            {
                try
                {
                    await _db.UpsertUserAddressAsync(new UserAddress
                    {
                        UserId = user.Id,
                        Label = "Home",
                        RecipientName = (string.IsNullOrWhiteSpace(ShipRecipient) ? FullName : ShipRecipient).Trim(),
                        PhoneNumber = (string.IsNullOrWhiteSpace(ShipPhone) ? ContactPhone : ShipPhone).Trim(),
                        AddressLine = ShipAddressLine?.Trim() ?? string.Empty,
                        Barangay = ShipBarangay?.Trim() ?? string.Empty,
                        City = ShipCity?.Trim() ?? string.Empty,
                        Province = (string.IsNullOrWhiteSpace(ShipProvince) ? "Batangas" : ShipProvince).Trim(),
                        PostalCode = ShipPostal?.Trim() ?? string.Empty,
                        IsDefault = SavedAddresses.Count == 0
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to persist user address: {ex}");
                }
            }

            MobileCheckoutIntent.Clear();
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

