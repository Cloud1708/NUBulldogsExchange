using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminOrderService
{
    public static readonly string[] StatusTabs =
    [
        "All Orders",
        "Pending",
        "Confirmed",
        "Preparing",
        "Processing",
        "Ready for Pickup",
        "Completed",
        "Cancelled"
    ];

    public static readonly string[] PaymentFilters =
    [
        "All Payments",
        "Paid",
        "Pending",
        "Failed",
        "Refunded"
    ];

    public static readonly string[] FulfillmentFilters =
    [
        "All Fulfillment",
        "Campus Pickup",
        "Delivery"
    ];

    private readonly IAppDatabase _db;
    private readonly List<AdminOrder> _orders = [];
    private bool _loaded;
    private bool _loading;

    public event Action? OnChange;

    public AdminOrderService(IAppDatabase db)
    {
        _db = db;
    }

    public IReadOnlyList<AdminOrder> All => _orders;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded || _loading) return;
        _loading = true;
        try
        {
            _orders.Clear();
            _orders.AddRange(await _db.GetOrdersAsync());
            _loaded = true;
            OnChange?.Invoke();
        }
        finally
        {
            _loading = false;
        }
    }

    public async Task ReloadAsync()
    {
        if (_loading) return;
        if (!_loaded)
        {
            await EnsureLoadedAsync();
            return;
        }

        _loading = true;
        try
        {
            _orders.Clear();
            _orders.AddRange(await _db.GetOrdersAsync());
            OnChange?.Invoke();
        }
        finally
        {
            _loading = false;
        }
    }

    public int CountByStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status) || status == "All Orders")
            return _orders.Count;

        return _orders.Count(o => o.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
    }

    public AdminOrder? GetById(string id) =>
        _orders.FirstOrDefault(o => o.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<AdminOrder> Filter(string statusTab, string? search, string payment, string fulfillment)
    {
        IEnumerable<AdminOrder> query = _orders.OrderByDescending(o => o.Date).ThenByDescending(o => o.Id);

        if (!string.IsNullOrWhiteSpace(statusTab) &&
            !statusTab.Equals("All Orders", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(o => o.Status.Equals(statusTab, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o =>
                o.Id.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                o.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                o.CustomerEmail.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(payment) &&
            !payment.Equals("All Payments", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(o => o.PaymentStatus.Equals(payment, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(fulfillment) &&
            !fulfillment.Equals("All Fulfillment", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(o => o.Fulfillment.Equals(fulfillment, StringComparison.OrdinalIgnoreCase));
        }

        return query;
    }

    public async Task<bool> UpdateStatusAsync(string id, string status)
    {
        var order = GetById(id);
        if (order is null) return false;
        order.Status = status;
        await _db.UpsertOrderAsync(order);
        OnChange?.Invoke();
        return true;
    }

    public async Task<AdminOrder> AddAsync(AdminOrder order, string? promoCode = null, decimal discountAmount = 0)
    {
        if (string.IsNullOrWhiteSpace(order.Id))
            order.Id = string.Empty;

        var requestedPaymentStatus = order.PaymentStatus;
        await _db.PlaceCheckoutOrderAsync(order, promoCode, discountAmount, order.CustomerEmail);

        _orders.RemoveAll(o => o.Id.Equals(order.Id, StringComparison.OrdinalIgnoreCase));
        var saved = await _db.GetOrderByIdAsync(order.Id) ?? order;
        if (string.Equals(requestedPaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            saved.PaymentStatus = "Paid";
        _orders.Add(saved);
        OnChange?.Invoke();
        return saved;
    }
}
