using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NUBulldogsExchange.Mobile.Models;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.ViewModels;

public sealed class NotificationFilterChip : INotifyPropertyChanged
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BorderColor)));
        }
    }

    public Color BackgroundColor => IsSelected ? Color.FromArgb("#00205B") : Colors.White;
    public Color TextColor => IsSelected ? Colors.White : Color.FromArgb("#475569");
    public Color BorderColor => IsSelected ? Color.FromArgb("#00205B") : Color.FromArgb("#CBD5E1");

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class NotificationsViewModel : INotifyPropertyChanged
{
    private readonly NotificationService _notifications;
    private readonly AuthService _auth;
    private readonly OrderService _orders;
    private readonly IAppDatabase _db;
    private readonly ToastService _toast;

    private string _selectedFilter = "All";
    private bool _isRefreshing;
    private bool _hasNotifications;

    public NotificationsViewModel(
        NotificationService notifications,
        AuthService auth,
        OrderService orders,
        IAppDatabase db,
        ToastService toast)
    {
        _notifications = notifications;
        _auth = auth;
        _orders = orders;
        _db = db;
        _toast = toast;

        BackCommand = new Command(async () => await GoBackAsync());
        RefreshCommand = new Command(async () => await LoadAsync());
        SelectFilterCommand = new Command<NotificationFilterChip>(OnSelectFilter);
        MarkAllAsReadCommand = new Command(async () => await OnMarkAllAsReadAsync());
        NotificationTapCommand = new Command<NotificationItemModel>(async n => await OnNotificationTapAsync(n));
        StartShoppingCommand = new Command(async () => await GoAsync("//shop"));
        ViewOrdersCommand = new Command(async () => await GoAsync("//orders"));

        _notifications.OnChange += () => MainThread.BeginInvokeOnMainThread(ApplyFilter);
        BuildFilterChips();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<NotificationFilterChip> FilterChips { get; } = [];
    public ObservableCollection<NotificationItemModel> FilteredNotifications { get; } = [];

    public int TotalCount => _notifications.Items.Count;
    public int UnreadCount => _notifications.UnreadCount;
    public bool HasUnread => UnreadCount > 0;
    public string UnreadCountText => UnreadCount > 0 ? $"{UnreadCount} new" : "0 new";

    public bool HasNotifications
    {
        get => _hasNotifications;
        private set
        {
            if (SetField(ref _hasNotifications, value))
                OnPropertyChanged(nameof(HasNoNotifications));
        }
    }

    public bool HasNoNotifications => !HasNotifications;

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetField(ref _isRefreshing, value);
    }

    public ICommand BackCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SelectFilterCommand { get; }
    public ICommand MarkAllAsReadCommand { get; }
    public ICommand NotificationTapCommand { get; }
    public ICommand StartShoppingCommand { get; }
    public ICommand ViewOrdersCommand { get; }

    public async Task LoadAsync()
    {
        IsRefreshing = true;
        try
        {
            await _orders.EnsureLoadedAsync(_auth.Email);
            await _notifications.EnsureLoadedAsync(_auth.Email);

            // If user has orders but no notifications saved yet, generate notifications from their orders
            if (_notifications.Items.Count == 0 && _orders.Orders.Count > 0)
            {
                foreach (var o in _orders.Orders.Take(5))
                {
                    var isDelivered = o.Status is "Completed" or "Delivered";
                    var isCancelled = o.Status is "Cancelled";
                    var title = isDelivered ? "Order Delivered" : (isCancelled ? "Order Cancelled" : "Order Placed");
                    var message = isDelivered
                        ? $"Your order {o.Id} was fulfilled. Thank you for shopping with NU Bulldogs Exchange!"
                        : (isCancelled
                            ? $"Order {o.Id} was cancelled."
                            : $"Your order {o.Id} ({o.ItemCountLabel}) has been placed and confirmed.");
                    var icon = isDelivered ? "check-circle" : (isCancelled ? "x-circle" : "box");
                    var tone = isDelivered ? "green" : (isCancelled ? "red" : "blue");

                    _notifications.Add(new MockNotification
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Title = title,
                        Message = message,
                        TimeAgo = o.FormattedDate,
                        Icon = icon,
                        Tone = tone,
                        IsRead = isDelivered,
                        RelatedId = o.Id,
                        RelatedHref = "/orders"
                    });
                }
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load notifications: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void BuildFilterChips()
    {
        FilterChips.Clear();
        var filters = new[] { "All", "Unread", "Orders" };
        foreach (var f in filters)
        {
            FilterChips.Add(new NotificationFilterChip
            {
                Key = f,
                Label = f,
                IsSelected = string.Equals(f, _selectedFilter, StringComparison.OrdinalIgnoreCase)
            });
        }
    }

    private void OnSelectFilter(NotificationFilterChip? chip)
    {
        if (chip is null) return;
        _selectedFilter = chip.Key;
        foreach (var c in FilterChips)
            c.IsSelected = string.Equals(c.Key, chip.Key, StringComparison.OrdinalIgnoreCase);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<MockNotification> source = _notifications.Items;

        if (string.Equals(_selectedFilter, "Unread", StringComparison.OrdinalIgnoreCase))
        {
            source = source.Where(n => !n.IsRead);
        }
        else if (string.Equals(_selectedFilter, "Orders", StringComparison.OrdinalIgnoreCase))
        {
            source = source.Where(n => !string.IsNullOrWhiteSpace(n.RelatedId) ||
                                       string.Equals(n.RelatedHref, "/orders", StringComparison.OrdinalIgnoreCase) ||
                                       n.Title.Contains("Order", StringComparison.OrdinalIgnoreCase));
        }

        var list = source.Select(n => new NotificationItemModel { Notification = n }).ToList();

        FilteredNotifications.Clear();
        foreach (var item in list)
            FilteredNotifications.Add(item);

        HasNotifications = FilteredNotifications.Count > 0;
        UpdateCounts();
    }

    private async Task OnMarkAllAsReadAsync()
    {
        if (UnreadCount == 0) return;

        // 1. Mark in-memory notifications as read immediately
        foreach (var item in _notifications.Items)
            item.IsRead = true;

        // 2. Mark filtered view models as read
        foreach (var n in FilteredNotifications)
            n.IsRead = true;

        // 3. Re-apply current filter & update UI counts immediately
        ApplyFilter();
        UpdateCounts();
        _toast.Show("All notifications marked as read.");

        // 4. Notify other ViewModels (Home, Account badges)
        NotifyNotificationsChanged();

        // 5. Persist to database in background safely (non-blocking, won't throw unhandled)
        await Task.Run(async () =>
        {
            try
            {
                await _db.SaveCustomerNotificationsAsync(_notifications.Items, _auth.Email);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to persist notifications as read: {ex.Message}");
            }
        });
    }

    private void NotifyNotificationsChanged()
    {
        try
        {
            var field = typeof(NotificationService).GetField("OnChange", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            (field?.GetValue(_notifications) as Action)?.Invoke();
        }
        catch { }
    }

    private async Task OnNotificationTapAsync(NotificationItemModel? item)
    {
        if (item is null) return;

        if (item.IsUnread)
        {
            item.IsRead = true;
            ApplyFilter();
            UpdateCounts();
            NotifyNotificationsChanged();

            _ = Task.Run(async () =>
            {
                try
                {
                    await _db.SaveCustomerNotificationsAsync(_notifications.Items, _auth.Email);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to persist notification read state: {ex.Message}");
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(item.RelatedId) ||
            string.Equals(item.RelatedHref, "/orders", StringComparison.OrdinalIgnoreCase))
        {
            await GoAsync("//orders");
        }
    }

    private void UpdateCounts()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(UnreadCount));
        OnPropertyChanged(nameof(HasUnread));
        OnPropertyChanged(nameof(UnreadCountText));
    }

    private static async Task GoAsync(string route)
    {
        try { await Shell.Current.GoToAsync(route); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    private static async Task GoBackAsync()
    {
        try
        {
            if (Shell.Current.Navigation.NavigationStack.Count > 1)
                await Shell.Current.GoToAsync("..");
            else
                await Shell.Current.GoToAsync("//home");
        }
        catch
        {
            await Shell.Current.GoToAsync("//home");
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
