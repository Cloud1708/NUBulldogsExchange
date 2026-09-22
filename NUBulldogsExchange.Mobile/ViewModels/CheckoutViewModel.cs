using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NUBulldogsExchange.Mobile.Services;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.ViewModels;

/// <summary>
/// Checkout page: guest sees Login-to-Continue gate; authenticated users place orders
/// via AdminOrderService (same flow as Web checkout).
/// </summary>
public sealed class CheckoutViewModel : INotifyPropertyChanged
{
    private readonly CartService _cart;
    private readonly AuthService _auth;
    private readonly ToastService _toast;
    private readonly AdminOrderService _adminOrders;
    private readonly AdminCustomerService _customers;
    private readonly OrderService _orders;
    private readonly ProductCatalogService _catalog;
    private readonly NotificationService _notifications;
    private readonly AdminNotificationService _adminNotifications;
    private Page? _host;

    private bool _isBusy;
    private string _totalText = "₱0";
    private string _subtotalText = "₱0";
    private string _discountText = "—";
    private string _statusMessage = string.Empty;
    private bool _hasError;

    public CheckoutViewModel(
        CartService cart,
        AuthService auth,
        ToastService toast,
        AdminOrderService adminOrders,
        AdminCustomerService customers,
        OrderService orders,
        ProductCatalogService catalog,
        NotificationService notifications,
        AdminNotificationService adminNotifications)
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

        PlaceOrderCommand = new Command(async () => await PlaceOrderAsync(), () => CanPlaceOrder);
        BackCommand = new Command(async () => await GoBackAsync());
        GoCartCommand = new Command(async () => await GoBackAsync());
        GoShopCommand = new Command(async () => await GoAsync("//shop"));
        GoLoginCommand = new Command(async () => await GoLoginAsync());
        GoRegisterCommand = new Command(async () => await GoRegisterAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Page? HostPage
    {
        get => _host;
        set => _host = value;
    }

    public bool IsLoggedIn => _auth.IsLoggedIn;
    public bool IsGuest => !_auth.IsLoggedIn;
    public bool HasItems => _cart.Items.Count > 0;
    public bool IsEmpty => _auth.IsLoggedIn && !HasItems;
    public bool ShowCheckoutForm => _auth.IsLoggedIn && HasItems;
    public bool ShowLoginGate => !_auth.IsLoggedIn;

    private bool CanPlaceOrder => !IsBusy && _auth.IsLoggedIn && _cart.Items.Count > 0;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
                ((Command)PlaceOrderCommand).ChangeCanExecute();
        }
    }

    public string SubtotalText
    {
        get => _subtotalText;
        private set => SetField(ref _subtotalText, value);
    }

    public string DiscountText
    {
        get => _discountText;
        private set => SetField(ref _discountText, value);
    }

    public string TotalText
    {
        get => _totalText;
        private set => SetField(ref _totalText, value);
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

    public ICommand PlaceOrderCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand GoCartCommand { get; }
    public ICommand GoShopCommand { get; }
    public ICommand GoLoginCommand { get; }
    public ICommand GoRegisterCommand { get; }

    public void Refresh()
    {
        if (_auth.IsLoggedIn)
            MobileCheckoutIntent.Clear();

        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsGuest));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ShowCheckoutForm));
        OnPropertyChanged(nameof(ShowLoginGate));
        SubtotalText = $"₱{_cart.Subtotal:N0}";
        DiscountText = _cart.AppliedDiscount > 0 ? $"-₱{_cart.AppliedDiscount:N0}" : "—";
        TotalText = $"₱{_cart.EstimatedTotal:N0}";
        ((Command)PlaceOrderCommand).ChangeCanExecute();
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

        IsBusy = true;
        StatusMessage = string.Empty;
        HasError = false;
        try
        {
            await _adminOrders.EnsureLoadedAsync();
            await _customers.EnsureLoadedAsync();

            var user = _auth.CurrentUser;
            var customer = _customers.EnsureCustomer(user.Name, user.Email, user.Phone, user.UserId);
            var promoCode = _cart.AppliedPromoCode;
            var order = new AdminOrder
            {
                Id = string.Empty,
                CustomerId = user.UserId > 0 ? user.UserId.ToString() : customer.Id,
                CustomerName = user.Name,
                CustomerEmail = user.Email,
                Date = DateTime.Now,
                Total = _cart.EstimatedTotal,
                PaymentStatus = "Pending",
                Fulfillment = "Campus Pickup",
                Status = "Pending",
                Items = _cart.Items.Select(i => new AdminOrderItem
                {
                    ProductId = i.Product.Id,
                    Name = i.Product.Name,
                    ImageUrl = i.Product.ImageUrl,
                    Quantity = i.Quantity,
                    Price = i.Product.Price
                }).ToList()
            };

            order = await _adminOrders.AddAsync(order, promoCode, 0);
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

            _toast.Show($"Order {order.Id} placed.");
            _cart.Clear();
            await _cart.PersistAsync(user.Email);
            MobileCheckoutIntent.Clear();
            await Shell.Current.GoToAsync("//orders");
        }
        catch (InvalidOperationException ex)
        {
            HasError = true;
            StatusMessage = ex.Message;
        }
        catch (Exception)
        {
            HasError = true;
            StatusMessage = "Unable to place order. Please try again.";
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
