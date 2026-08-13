using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminCustomerService
{
    private readonly List<AdminCustomer> _customers;
    private readonly AdminOrderService _orders;

    public event Action? OnChange;

    public AdminCustomerService(AdminOrderService orders)
    {
        _orders = orders;
        _customers =
        [
            Customer("C-001", "Maria Santos", "maria.santos@example.com",
                "+63 912 345 6789", new DateTime(2026, 1, 15), "Active", 8, 6840),
            Customer("C-002", "Juan dela Cruz", "juan.delacruz@example.com",
                "+63 917 234 5678", new DateTime(2026, 2, 3), "Active", 5, 3540),
            Customer("C-003", "Ana Reyes", "ana.reyes@example.com",
                "+63 918 345 6789", new DateTime(2026, 3, 22), "Active", 3, 1848),
            Customer("C-004", "Carlo Mendoza", "carlo.mendoza@example.com",
                "+63 919 456 7890", new DateTime(2026, 4, 10), "Active", 12, 9240),
            Customer("C-005", "Sofia Lim", "sofia.lim@example.com",
                "+63 920 567 8901", new DateTime(2026, 5, 5), "Active", 2, 828),
            Customer("C-006", "Mark Torres", "mark.torres@example.com",
                "+63 921 678 9012", new DateTime(2026, 6, 18), "Inactive", 1, 299)
        ];

        // Link existing admin orders to customer IDs by email.
        foreach (var order in _orders.All)
        {
            var customer = _customers.FirstOrDefault(c =>
                c.Email.Equals(order.CustomerEmail, StringComparison.OrdinalIgnoreCase));
            if (customer is not null)
                order.CustomerId = customer.Id;
        }

        _orders.OnChange += () => OnChange?.Invoke();
    }

    public IReadOnlyList<AdminCustomer> All => _customers;

    public int TotalCustomers => _customers.Count;

    public int ActiveCustomers => _customers.Count(c => c.IsActive);

    public decimal TotalRevenue => _customers.Sum(c => c.TotalSpent);

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

    public bool SetStatus(string id, string status)
    {
        var customer = GetById(id);
        if (customer is null) return false;

        if (!status.Equals("Active", StringComparison.OrdinalIgnoreCase) &&
            !status.Equals("Inactive", StringComparison.OrdinalIgnoreCase) &&
            !status.Equals("Suspended", StringComparison.OrdinalIgnoreCase))
            return false;

        customer.Status = status switch
        {
            var s when s.Equals("Active", StringComparison.OrdinalIgnoreCase) => "Active",
            var s when s.Equals("Suspended", StringComparison.OrdinalIgnoreCase) => "Suspended",
            _ => "Inactive"
        };

        OnChange?.Invoke();
        return true;
    }

    private static AdminCustomer Customer(
        string id,
        string name,
        string email,
        string contact,
        DateTime joined,
        string status,
        int orders,
        decimal spent) =>
        new()
        {
            Id = id,
            Name = name,
            Email = email,
            Contact = contact,
            DateJoined = joined,
            Status = status,
            TotalOrders = orders,
            TotalSpent = spent
        };
}
