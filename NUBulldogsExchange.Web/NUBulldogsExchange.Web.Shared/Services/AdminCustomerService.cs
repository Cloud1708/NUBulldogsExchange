using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminCustomerService
{
    private readonly IAppDatabase _db;
    private readonly AdminOrderService _orders;
    private readonly List<AdminCustomer> _customers = [];
    private decimal _completedRevenue;

    private bool _loading;

    public event Action? OnChange;

    public AdminCustomerService(IAppDatabase db, AdminOrderService orders)
    {
        _db = db;
        _orders = orders;
        _orders.OnChange += () => OnChange?.Invoke();
    }

    public async Task EnsureLoadedAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            await _orders.EnsureLoadedAsync();
            _customers.Clear();
            _customers.AddRange(await _db.GetCustomersAsync());
            try
            {
                _completedRevenue = await _db.GetCompletedOrderRevenueAsync();
            }
            catch
            {
                _completedRevenue = _orders.All
                    .Where(o => o.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
                    .Sum(o => o.Total);
            }

            OnChange?.Invoke();
        }
        finally
        {
            _loading = false;
        }
    }

    public IReadOnlyList<AdminCustomer> All => _customers;

    public int TotalCustomers => _customers.Count;
    public int ActiveCustomers => _customers.Count(c => c.IsActive);
    public decimal TotalRevenue => _completedRevenue;
    public string TotalRevenueLabel => $"₱{TotalRevenue:N0}";

    public AdminCustomer? GetById(string id) =>
        _customers.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<AdminCustomer> Search(string? query)
    {
        IEnumerable<AdminCustomer> result = _customers.OrderBy(c => c.Id);

        if (string.IsNullOrWhiteSpace(query))
            return result;

        var term = query.Trim();
        return result.Where(c =>
            c.Id.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            c.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            c.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            c.Contact.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<AdminOrder> OrdersForCustomer(string customerId)
    {
        var customer = GetById(customerId);
        if (customer is null)
            return [];

        return _orders.All
            .Where(o =>
                (!string.IsNullOrWhiteSpace(o.CustomerId) &&
                 o.CustomerId.Equals(customer.Id, StringComparison.OrdinalIgnoreCase)) ||
                o.CustomerEmail.Equals(customer.Email, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(o => o.Date)
            .ThenByDescending(o => o.Id)
            .ToList();
    }

    public int ActiveOrderCount(string customerId) =>
        OrdersForCustomer(customerId).Count(o =>
            !o.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) &&
            !o.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase));

    public int CompletedOrderCount(string customerId) =>
        OrdersForCustomer(customerId).Count(o =>
            o.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase));

    public async Task<bool> SetStatusAsync(string id, string status, int? actorUserId = null)
    {
        var customer = GetById(id);
        if (customer is null) return false;

        if (!status.Equals("Active", StringComparison.OrdinalIgnoreCase) &&
            !status.Equals("Inactive", StringComparison.OrdinalIgnoreCase) &&
            !status.Equals("Suspended", StringComparison.OrdinalIgnoreCase))
            return false;

        var normalized = status switch
        {
            var s when s.Equals("Active", StringComparison.OrdinalIgnoreCase) => "Active",
            var s when s.Equals("Suspended", StringComparison.OrdinalIgnoreCase) => "Suspended",
            _ => "Inactive"
        };

        var updated = await _db.SetCustomerStatusAsync(id, normalized, actorUserId);
        if (!updated)
            return false;

        customer.Status = normalized;
        OnChange?.Invoke();
        return true;
    }

    public AdminCustomer EnsureCustomer(string name, string email, string? contact = null, int userId = 0)
    {
        var existing = _customers.FirstOrDefault(c =>
            c.Email.Equals(email, StringComparison.OrdinalIgnoreCase) ||
            (userId > 0 && c.Id == userId.ToString()));
        if (existing is not null)
            return existing;

        return new AdminCustomer
        {
            Id = userId > 0 ? userId.ToString() : email.Trim().ToLowerInvariant(),
            Name = name,
            Email = email.Trim(),
            Contact = contact ?? string.Empty,
            DateJoined = DateTime.UtcNow,
            Status = "Active",
            TotalOrders = 0,
            TotalSpent = 0
        };
    }

    public async Task RecordPurchaseAsync(string email, decimal amount)
    {
        var customer = _customers.FirstOrDefault(c =>
            c.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        if (customer is null) return;
        customer.TotalOrders += 1;
        customer.TotalSpent += amount;
        await _db.UpsertCustomerAsync(customer);
        OnChange?.Invoke();
    }
}
