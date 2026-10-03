using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminCustomerService
{
    public const int PageSize = 10;

    public static readonly string[] StatusFilters = ["All Status", "Active", "Inactive"];
    public static readonly string[] TypeFilters =
        ["All Customer Types", "New", "Returning", "Frequent Buyer"];
    public static readonly string[] SortOptions =
    [
        "newest",
        "oldest",
        "name-asc",
        "name-desc",
        "orders-desc",
        "spent-desc",
        "spent-asc"
    ];

    private readonly IAppDatabase _db;
    private readonly AdminOrderService _orders;
    private readonly List<AdminCustomer> _customers = [];

    private bool _loading;

    public event Action? OnChange;

    public AdminCustomerService(IAppDatabase db, AdminOrderService orders)
    {
        _db = db;
        _orders = orders;
        _orders.OnChange += () =>
        {
            EnrichFromOrders();
            OnChange?.Invoke();
        };
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
            EnrichFromOrders();
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

    public int NewThisMonth
    {
        get
        {
            var now = DateTime.Now;
            return _customers.Count(c =>
                c.DateJoined.Year == now.Year && c.DateJoined.Month == now.Month);
        }
    }

    public string NewThisMonthCaption
    {
        get
        {
            var now = DateTime.Now;
            return $"Joined in {now:MMMM yyyy}";
        }
    }

    public int ReturningCustomers =>
        _customers.Count(c => c.QualifyingOrderCount >= 2);

    public AdminCustomer? GetById(string id) =>
        _customers.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<AdminCustomer> Filter(
        string? query,
        string status = "All Status",
        string customerType = "All Customer Types",
        string sort = "newest")
    {
        IEnumerable<AdminCustomer> result = _customers;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            var digits = DigitsOnly(term);
            result = result.Where(c =>
                c.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                c.FirstName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                c.LastName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                c.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                c.Contact.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(digits) && DigitsOnly(c.Contact).Contains(digits, StringComparison.Ordinal)) ||
                c.Id.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                c.ShortId.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status)
            && !status.Equals("All Status", StringComparison.OrdinalIgnoreCase))
        {
            if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                result = result.Where(c => c.IsActive);
            else if (status.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
                result = result.Where(c => !c.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(customerType)
            && !customerType.Equals("All Customer Types", StringComparison.OrdinalIgnoreCase))
        {
            result = result.Where(c =>
                c.CustomerType.Equals(customerType, StringComparison.OrdinalIgnoreCase));
        }

        return sort switch
        {
            "oldest" => result.OrderBy(c => c.DateJoined).ThenBy(c => c.Id),
            "name-asc" => result.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Id),
            "name-desc" => result.OrderByDescending(c => c.Name, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Id),
            "orders-desc" => result.OrderByDescending(c => c.TotalOrders).ThenByDescending(c => c.DateJoined),
            "spent-desc" => result.OrderByDescending(c => c.TotalSpent).ThenByDescending(c => c.DateJoined),
            "spent-asc" => result.OrderBy(c => c.TotalSpent).ThenByDescending(c => c.DateJoined),
            _ => result.OrderByDescending(c => c.DateJoined).ThenByDescending(c => c.Id)
        };
    }

    /// <summary>Backward-compatible search used by older call sites. </summary>
    public IEnumerable<AdminCustomer> Search(string? query) =>
        Filter(query);

    public IReadOnlyList<AdminOrder> OrdersForCustomer(string customerId)
    {
        var customer = GetById(customerId);
        if (customer is null)
            return [];

        return MatchOrders(customer)
            .OrderByDescending(o => o.Date)
            .ThenByDescending(o => o.Id)
            .ToList();
    }

    public int ActiveOrderCount(string customerId) =>
        OrdersForCustomer(customerId).Count(IsActiveOrder);

    public int CompletedOrderCount(string customerId) =>
        OrdersForCustomer(customerId).Count(o =>
            OrderFlow.IsFulfilled(o.Fulfillment, o.Status)
            || o.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
            || o.Status.Equals("Delivered", StringComparison.OrdinalIgnoreCase));

    public int CancelledOrderCount(string customerId) =>
        OrdersForCustomer(customerId).Count(o =>
            o.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase));

    public decimal FulfilledSpend(string customerId) =>
        OrdersForCustomer(customerId)
            .Where(o => OrderFlow.IsFulfilled(o.Fulfillment, o.Status))
            .Sum(o => o.Total);

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
            QualifyingOrderCount = 0,
            TotalSpent = 0
        };
    }

    public async Task RecordPurchaseAsync(string email, decimal amount)
    {
        var customer = _customers.FirstOrDefault(c =>
            c.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        if (customer is null) return;
        customer.TotalOrders += 1;
        customer.QualifyingOrderCount += 1;
        customer.TotalSpent += amount;
        customer.LastOrderAt = DateTime.UtcNow;
        await _db.UpsertCustomerAsync(customer);
        OnChange?.Invoke();
    }

    private void EnrichFromOrders()
    {
        if (_customers.Count == 0) return;

        foreach (var customer in _customers)
        {
            var orders = MatchOrders(customer).ToList();
            customer.TotalOrders = orders.Count;
            customer.QualifyingOrderCount = orders.Count(o =>
                !o.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase));
            customer.TotalSpent = orders
                .Where(o => OrderFlow.IsFulfilled(o.Fulfillment, o.Status))
                .Sum(o => o.Total);

            var last = orders
                .Where(o => !o.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(o => o.Date)
                .ThenByDescending(o => o.Id)
                .FirstOrDefault()
                ?? orders.OrderByDescending(o => o.Date).ThenByDescending(o => o.Id).FirstOrDefault();

            customer.LastOrderAt = last?.Date;
        }
    }

    private IEnumerable<AdminOrder> MatchOrders(AdminCustomer customer) =>
        _orders.All.Where(o =>
            (!string.IsNullOrWhiteSpace(o.CustomerId) &&
             o.CustomerId.Equals(customer.Id, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(o.AuthUserId) &&
             o.AuthUserId.Equals(customer.Id, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(customer.Email) &&
             o.CustomerEmail.Equals(customer.Email, StringComparison.OrdinalIgnoreCase)));

    public static bool IsActiveOrder(AdminOrder o)
    {
        if (o.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
            return false;
        if (OrderFlow.IsFulfilled(o.Fulfillment, o.Status))
            return false;
        if (o.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
            || o.Status.Equals("Delivered", StringComparison.OrdinalIgnoreCase))
            return false;

        return o.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase)
            || o.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase)
            || o.Status.Equals("Processing", StringComparison.OrdinalIgnoreCase)
            || o.Status.Equals("Preparing", StringComparison.OrdinalIgnoreCase)
            || o.Status.Equals(OrderFlow.ReadyForPickup, StringComparison.OrdinalIgnoreCase)
            || o.Status.Equals("Out for Delivery", StringComparison.OrdinalIgnoreCase)
            || o.Status.Equals("Shipped", StringComparison.OrdinalIgnoreCase);
    }

    private static string DigitsOnly(string value) =>
        string.Concat((value ?? string.Empty).Where(char.IsDigit));
}
