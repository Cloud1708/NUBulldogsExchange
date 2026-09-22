using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.ViewModels;

public sealed class OrderStatusChip : INotifyPropertyChanged
{
    private bool _isSelected;

    public string Key { get; init; } = "All";
    public string Label { get; init; } = "All";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundColor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextColor)));
        }
    }

    public Color BackgroundColor => IsSelected
        ? Color.FromArgb("#00205B")
        : Color.FromArgb("#F1F5F9");

    public Color TextColor => IsSelected
        ? Colors.White
        : Color.FromArgb("#64748B");

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>UI projection for an order card with status colors and action flags.</summary>
public sealed class OrderCardModel
{
    public required MockOrder Order { get; init; }
    public string DisplayId => Order.Id.StartsWith('#') ? Order.Id : $"#{Order.Id}";
    public string DateText => Order.FormattedDate;
    public string Status => Order.Status;
    public string TotalText => $"₱{Order.Total:N0}";
    public IReadOnlyList<MockOrderItem> Items => Order.Items;

    public Color BadgeBackground => Order.Status switch
    {
        "Pending" => Color.FromArgb("#FEF3C7"),
        "Confirmed" => Color.FromArgb("#DBEAFE"),
        "Processing" => Color.FromArgb("#EDE9FE"),
        "Ready for Pickup" => Color.FromArgb("#E0F2FE"),
        "Completed" => Color.FromArgb("#DCFCE7"),
        "Cancelled" => Color.FromArgb("#FEE2E2"),
        _ => Color.FromArgb("#F1F5F9")
    };

    public Color BadgeTextColor => Order.Status switch
    {
        "Pending" => Color.FromArgb("#B45309"),
        "Confirmed" => Color.FromArgb("#1D4ED8"),
        "Processing" => Color.FromArgb("#6D28D9"),
        "Ready for Pickup" => Color.FromArgb("#0369A1"),
        "Completed" => Color.FromArgb("#15803D"),
        "Cancelled" => Color.FromArgb("#B91C1C"),
        _ => Color.FromArgb("#475569")
    };

    public bool ShowTrack => Order.Status is "Processing" or "Confirmed" or "Ready for Pickup" or "Pending";
    public bool ShowBuyAgain => Order.Status == "Completed";
    public bool ShowReview => Order.Status == "Completed";
    public bool ShowDetails => true;
}

public sealed class OrdersViewModel : INotifyPropertyChanged
{
    private readonly OrderService _orders;
    private readonly AuthService _auth;
    private readonly CartService _cart;
    private readonly ProductCatalogService _catalog;
    private readonly ToastService _toast;

    private string _selectedStatus = "All";
    private bool _hasOrders;
    private bool _isBusy;
    private Page? _host;

    public OrdersViewModel(
        OrderService orders,
        AuthService auth,
        CartService cart,
        ProductCatalogService catalog,
        ToastService toast)
    {
        _orders = orders;
        _auth = auth;
        _cart = cart;
        _catalog = catalog;
        _toast = toast;

        SelectStatusCommand = new Command<OrderStatusChip>(OnSelectStatus);
        StartShoppingCommand = new Command(async () => await GoAsync("//shop"));
        DetailsCommand = new Command<OrderCardModel>(async o => await OnDetailsAsync(o));
        TrackCommand = new Command<OrderCardModel>(async o => await OnTrackAsync(o));
        BuyAgainCommand = new Command<OrderCardModel>(async o => await OnBuyAgainAsync(o));
        ReviewCommand = new Command<OrderCardModel>(async o => await OnReviewAsync(o));

        _orders.OnChange += () => MainThread.BeginInvokeOnMainThread(ApplyFilter);
        BuildStatusChips();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Page? HostPage
    {
        get => _host;
        set => _host = value;
    }

    public ObservableCollection<OrderStatusChip> StatusChips { get; } = [];
    public ObservableCollection<OrderCardModel> FilteredOrders { get; } = [];

    public bool HasOrders
    {
        get => _hasOrders;
        private set
        {
            if (SetField(ref _hasOrders, value))
                OnPropertyChanged(nameof(HasNoOrders));
        }
    }

    public bool HasNoOrders => !HasOrders;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public ICommand SelectStatusCommand { get; }
    public ICommand StartShoppingCommand { get; }
    public ICommand DetailsCommand { get; }
    public ICommand TrackCommand { get; }
    public ICommand BuyAgainCommand { get; }
    public ICommand ReviewCommand { get; }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            // Force refresh so returning to the page picks up new orders.
            await ForceReloadAsync();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ForceReloadAsync()
    {
        // OrderService caches by email; call EnsureLoadedAsync which reloads when email changes.
        // For same email, still refresh by reading Filter after EnsureLoaded.
        await _orders.EnsureLoadedAsync(_auth.Email);

        // If still empty and guest, load all orders for preview (Windows/dev).
        if (_orders.Orders.Count == 0 && string.IsNullOrWhiteSpace(_auth.Email))
            await _orders.EnsureLoadedAsync(null);
    }

    private void BuildStatusChips()
    {
        var statuses = new[]
        {
            "All",
            "Pending",
            "Confirmed",
            "Processing",
            "Ready for Pickup",
            "Completed",
            "Cancelled"
        };

        StatusChips.Clear();
        foreach (var status in statuses)
        {
            StatusChips.Add(new OrderStatusChip
            {
                Key = status,
                Label = status,
                IsSelected = string.Equals(status, _selectedStatus, StringComparison.OrdinalIgnoreCase)
            });
        }
    }

    private void OnSelectStatus(OrderStatusChip? chip)
    {
        if (chip is null) return;
        _selectedStatus = chip.Key;
        foreach (var c in StatusChips)
            c.IsSelected = string.Equals(c.Key, chip.Key, StringComparison.OrdinalIgnoreCase);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filtered = _orders.Filter(_selectedStatus)
            .Select(o => new OrderCardModel { Order = o })
            .ToList();

        FilteredOrders.Clear();
        foreach (var card in filtered)
            FilteredOrders.Add(card);

        HasOrders = FilteredOrders.Count > 0;
    }

    private async Task OnDetailsAsync(OrderCardModel? card)
    {
        if (card is null) return;
        var page = HostPage ?? Shell.Current;
        var lines = string.Join("\n", card.Items.Select(i =>
            $"• {i.Name} (Size: {i.Size} · Qty: {i.Quantity})"));
        await page.DisplayAlertAsync(
            $"Order {card.DisplayId}",
            $"Status: {card.Status}\nDate: {card.DateText}\nTotal: {card.TotalText}\n\nItems:\n{lines}",
            "OK");
    }

    private async Task OnTrackAsync(OrderCardModel? card)
    {
        if (card is null) return;
        var page = HostPage ?? Shell.Current;
        await page.DisplayAlertAsync(
            "Track Order",
            $"{card.DisplayId} is currently: {card.Status}.",
            "OK");
    }

    private async Task OnBuyAgainAsync(OrderCardModel? card)
    {
        if (card is null) return;
        await _catalog.EnsureLoadedAsync();
        var added = 0;
        foreach (var item in card.Items)
        {
            var product = _catalog.GetById(item.ProductId)
                          ?? _catalog.Products.FirstOrDefault(p =>
                              p.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
            if (product is null) continue;
            _cart.Add(product, Math.Max(1, item.Quantity), null, item.Size);
            added++;
        }

        if (added > 0)
        {
            await _cart.PersistAsync(_auth.Email);
            _toast.Show($"Added {added} item(s) to cart.");
        }
        else
        {
            _toast.Show("Unable to add items to cart.");
        }
    }

    private async Task OnReviewAsync(OrderCardModel? card)
    {
        if (card is null) return;
        var page = HostPage ?? Shell.Current;
        await page.DisplayAlertAsync(
            "Review",
            $"Thanks for shopping! Review for {card.DisplayId} is coming soon.",
            "OK");
    }

    private static async Task GoAsync(string route)
    {
        try
        {
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
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
