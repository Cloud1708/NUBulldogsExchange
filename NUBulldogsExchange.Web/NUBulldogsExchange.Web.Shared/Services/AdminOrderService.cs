using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminOrderService
{
    public static readonly string[] FulfillmentTabs =
    [
        "All",
        OrderFlow.CampusPickup,
        OrderFlow.Delivery
    ];

    public static readonly string[] StatusFilters = OrderFlow.AdminStatusFilters;

    public static readonly string[] PaymentFilters =
    [
        "All Payments",
        "Paid",
        "Pending",
        "Failed",
        "Refunded",
        "Cancelled"
    ];

    public static readonly string[] PaymentMethodFilters = OrderFlow.PaymentMethodFilters;

    public static readonly string[] FulfillmentFilters =
    [
        "All Fulfillment",
        OrderFlow.CampusPickup,
        OrderFlow.Delivery
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

    public int CountByFulfillment(string fulfillment)
    {
        if (string.IsNullOrWhiteSpace(fulfillment) || fulfillment.Equals("All", StringComparison.OrdinalIgnoreCase))
            return _orders.Count;

        return _orders.Count(o => o.Fulfillment.Equals(fulfillment, StringComparison.OrdinalIgnoreCase));
    }

    public AdminOrder? GetById(string id) =>
        _orders.FirstOrDefault(o => o.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<AdminOrder> Filter(
        string fulfillmentTab,
        string? search,
        string payment,
        string paymentMethod,
        string status)
    {
        IEnumerable<AdminOrder> query = _orders.OrderByDescending(o => o.Date).ThenByDescending(o => o.Id);

        if (!string.IsNullOrWhiteSpace(fulfillmentTab) &&
            !fulfillmentTab.Equals("All", StringComparison.OrdinalIgnoreCase) &&
            !fulfillmentTab.Equals("All Fulfillment", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(o => o.Fulfillment.Equals(fulfillmentTab, StringComparison.OrdinalIgnoreCase));
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

        if (!string.IsNullOrWhiteSpace(paymentMethod) &&
            !paymentMethod.Equals("All Methods", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(o => o.PaymentMethod.Equals(paymentMethod, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            !status.Equals("All Statuses", StringComparison.OrdinalIgnoreCase) &&
            !status.Equals("All Orders", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(o => o.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        return query;
    }

    public IEnumerable<string> StatusOptionsFor(AdminOrder order) =>
        OrderFlow.AdminStatusOptions(order.Fulfillment, order.Status);

    public IEnumerable<string> PaymentStatusOptionsFor(AdminOrder order) =>
        OrderFlow.PaymentStatusChoices(order.PaymentStatus);

    public async Task<bool> UpdateOrderAsync(
        string id,
        string status,
        string paymentStatus,
        string? adminRemarks,
        bool notifyCustomer)
    {
        var order = GetById(id);
        if (order is null) return false;

        var allowedStatuses = StatusOptionsFor(order);
        if (!allowedStatuses.Any(s => s.Equals(status, StringComparison.OrdinalIgnoreCase)))
            return false;

        var allowedPayments = PaymentStatusOptionsFor(order);
        if (!allowedPayments.Any(s => s.Equals(paymentStatus, StringComparison.OrdinalIgnoreCase)))
            return false;

        var oldStatus = order.Status;
        var statusChanged = !oldStatus.Equals(status, StringComparison.OrdinalIgnoreCase);
        var paymentChanged = !order.PaymentStatus.Equals(paymentStatus, StringComparison.OrdinalIgnoreCase);
        var remarksValue = string.IsNullOrWhiteSpace(adminRemarks) ? null : adminRemarks.Trim();
        var remarksChanged = !string.Equals(
            order.AdminRemarks?.Trim(),
            remarksValue,
            StringComparison.Ordinal);

        if (!statusChanged && !paymentChanged && !remarksChanged)
            return true;

        await _db.UpdateAdminOrderAsync(order.Id, status, paymentStatus, remarksValue);
        order.Status = status;
        order.PaymentStatus = paymentStatus;
        order.AdminRemarks = remarksValue;

        if (statusChanged)
        {
            try
            {
                await _db.AppendOrderStatusHistoryAsync(order.Id, oldStatus, status, remarksValue, null);
            }
            catch
            {
                // History is best-effort; the order update still stands.
            }
        }

        if (notifyCustomer && statusChanged)
            await TryNotifyCustomerAsync(order, status);

        OnChange?.Invoke();
        return true;
    }

    public async Task<bool> UpdateStatusAsync(string id, string status)
    {
        var order = GetById(id);
        if (order is null) return false;

        var allowed = OrderFlow.AdminStatusOptions(order.Fulfillment, order.Status);
        if (!allowed.Any(s => s.Equals(status, StringComparison.OrdinalIgnoreCase)))
            return false;

        var oldStatus = order.Status;
        if (oldStatus.Equals(status, StringComparison.OrdinalIgnoreCase))
            return true;

        await _db.UpdateOrderStatusAsync(order.Id, status);
        order.Status = status;

        try
        {
            await _db.AppendOrderStatusHistoryAsync(order.Id, oldStatus, status, null, null);
        }
        catch
        {
            // History is best-effort; status change still stands.
        }

        await TryNotifyCustomerAsync(order, status);
        OnChange?.Invoke();
        return true;
    }

    public async Task<AdminOrder> AddAsync(AdminOrder order, string? promoCode = null, decimal discountAmount = 0)
    {
        if (string.IsNullOrWhiteSpace(order.Id))
            order.Id = string.Empty;

        await _db.PlaceCheckoutOrderAsync(order, promoCode, discountAmount, order.CustomerEmail);

        _orders.RemoveAll(o => o.Id.Equals(order.Id, StringComparison.OrdinalIgnoreCase));
        var saved = await _db.GetOrderByIdAsync(order.Id) ?? order;
        _orders.Add(saved);
        OnChange?.Invoke();
        return saved;
    }

    public async Task<AdminOrder> ConfirmCheckoutPaymentAsync(
        string orderId,
        string paymentMethod,
        string paymentStatus)
    {
        var saved = await _db.PersistCheckoutPaymentAsync(orderId, paymentMethod, paymentStatus);
        _orders.RemoveAll(o => o.Id.Equals(saved.Id, StringComparison.OrdinalIgnoreCase));
        _orders.Add(saved);
        OnChange?.Invoke();
        return saved;
    }

    private async Task TryNotifyCustomerAsync(AdminOrder order, string newStatus)
    {
        var title = OrderFlow.CustomerNotificationTitle(order.Fulfillment, newStatus);
        var message = OrderFlow.CustomerNotificationMessage(order.Id, order.Fulfillment, newStatus);
        if (title is null || message is null)
            return;

        try
        {
            await _db.AddCustomerNotificationAsync(
                order.CustomerEmail,
                order.AuthUserId ?? order.CustomerId,
                new MockNotification
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = title,
                    Message = message,
                    TimeAgo = "Just now",
                    Icon = "package",
                    Tone = "blue",
                    IsRead = false
                });
        }
        catch
        {
            // Existing notification system is best-effort.
        }
    }
}
