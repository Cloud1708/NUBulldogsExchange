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

    private readonly List<AdminNotificationItem> _items;

    public event Action? OnChange;

    public AdminNotificationService()
    {
        var now = DateTime.Now;
        _items =
        [
            Item("NOTIF-001", "order", "New Order Received",
                "Order #NUBE-1025 placed by Maria Santos — ₱1,797. Campus Pickup.",
                "NUBE-1025", "NUBE-1025", "/admin/orders/NUBE-1025",
                now.AddMinutes(-5), false),
            Item("NOTIF-002", "stock", "Low Stock Alert",
                "NU Bulldogs Varsity Jacket is running low — only 18 units remaining.",
                "jacket", "NU Bulldogs Varsity Jacket", "/admin/inventory",
                now.AddHours(-1), false),
            Item("NOTIF-003", "order", "Order Cancelled",
                "Order #NUBE-1019 cancelled by Mark Torres. Reason: Changed mind.",
                "NUBE-1019", "NUBE-1019", "/admin/orders/NUBE-1019",
                now.AddHours(-3), false),
            Item("NOTIF-004", "stock", "Out of Stock",
                "NU Bulldogs Varsity Hoodie (Size S) is now out of stock.",
                "hoodie", "NU Bulldogs Varsity Hoodie", "/admin/inventory",
                now.Date.AddDays(-1).AddHours(16).AddMinutes(30), true),
            Item("NOTIF-005", "order", "Order Ready for Pickup",
                "Order #NUBE-1024 is ready for pickup. Customer notified.",
                "NUBE-1024", "NUBE-1024", "/admin/orders/NUBE-1024",
                now.Date.AddDays(-1).AddHours(14).AddMinutes(15), true),
            Item("NOTIF-006", "system", "New Customer Registered",
                "Diego Villanueva just created an account on NU Bulldogs Exchange.",
                null, null, "/admin/customers",
                now.AddDays(-2), true),
            Item("NOTIF-007", "stock", "Low Stock Alert",
                "NU Bulldogs Backpack has only 32 units left.",
                "backpack", "NU Bulldogs Backpack", "/admin/inventory",
                now.AddDays(-2), true),
            Item("NOTIF-008", "order", "Order Completed",
                "Order #NUBE-1021 marked as completed. ₱2,246 collected.",
                "NUBE-1021", "NUBE-1021", "/admin/orders/NUBE-1021",
                now.AddDays(-3), true)
        ];
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

        if (changed) OnChange?.Invoke();
    }

    public int ClearRead()
    {
        var removed = _items.RemoveAll(n => n.Read);
        if (removed > 0) OnChange?.Invoke();
        return removed;
    }

    private static AdminNotificationItem Item(
        string id,
        string type,
        string title,
        string message,
        string? relatedId,
        string? relatedLabel,
        string? href,
        DateTime timestamp,
        bool read) =>
        new()
        {
            Id = id,
            Type = type,
            Title = title,
            Message = message,
            RelatedId = relatedId,
            RelatedLabel = relatedLabel,
            RelatedHref = href,
            Timestamp = timestamp,
            Read = read
        };
}
