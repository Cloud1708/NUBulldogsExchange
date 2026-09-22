using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminNotificationService
{
    public static readonly (string Key, string Label)[] Tabs =
    [
        ("all", "All"),
        ("order", "Orders"),
        ("stock", "Stock Alerts"),
        ("system", "System")
    ];

    private readonly IAppDatabase _db;
    private readonly List<AdminNotificationItem> _items = [];
    private bool _loaded;

    public event Action? OnChange;

    public AdminNotificationService(IAppDatabase db)
    {
        _db = db;
    }

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _items.Clear();
        _items.AddRange(await _db.GetAdminNotificationsAsync());
        _loaded = true;
        OnChange?.Invoke();
    }

    public IReadOnlyList<AdminNotificationItem> All =>
        _items.OrderByDescending(n => n.Timestamp).ToList();

    public int UnreadCount => _items.Count(n => !n.Read);
    public int OrderCount => _items.Count(n => n.Type == "order");
    public int StockCount => _items.Count(n => n.Type == "stock");
    public int SystemCount => _items.Count(n => n.Type == "system");
    public int TotalCount => _items.Count;

    public int CountByTab(string tab) => tab switch
    {
        "order" => OrderCount,
        "stock" => StockCount,
        "system" => SystemCount,
        _ => TotalCount
    };

    public IEnumerable<AdminNotificationItem> Filter(string tab)
    {
        IEnumerable<AdminNotificationItem> query = All;
        if (!string.IsNullOrWhiteSpace(tab) && tab != "all")
            query = query.Where(n => n.Type.Equals(tab, StringComparison.OrdinalIgnoreCase));
        return query;
    }

    public void MarkRead(string id)
    {
        var item = _items.FirstOrDefault(n => n.Id == id);
        if (item is null || item.Read) return;
        item.Read = true;
        Persist();
        OnChange?.Invoke();
    }

    public void MarkAllRead()
    {
        var changed = false;
        foreach (var item in _items.Where(n => !n.Read))
        {
            item.Read = true;
            changed = true;
        }

        if (changed)
        {
            Persist();
            OnChange?.Invoke();
        }
    }

    public int ClearRead()
    {
        var removed = _items.RemoveAll(n => n.Read);
        if (removed > 0)
        {
            Persist();
            OnChange?.Invoke();
        }

        return removed;
    }

    public void Add(AdminNotificationItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
            item.Id = $"NOTIF-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        _items.Insert(0, item);
        Persist();
        OnChange?.Invoke();
    }

    public async Task AddAsync(AdminNotificationItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
            item.Id = $"NOTIF-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        _items.Insert(0, item);
        await _db.SaveAdminNotificationsAsync(_items);
        OnChange?.Invoke();
    }

    private void Persist() =>
        _db.SaveAdminNotificationsAsync(_items).GetAwaiter().GetResult();
}
