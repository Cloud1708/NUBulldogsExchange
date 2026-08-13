using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminOrderService
{
    public static readonly string[] StatusTabs =
    [
        "All Orders",
        "Pending",
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

    private readonly List<AdminOrder> _orders;

    public event Action? OnChange;

    public AdminOrderService()
    {
        _orders =
        [
            Order("NUBE-1025", "Maria Santos", "maria.santos@example.com",
                new DateTime(2026, 8, 14), 1797, "Paid", "Campus Pickup", "Pending",
                Item(3, 2), Item(2, 1)),
            Order("NUBE-1024", "Maria Santos", "maria.santos@example.com",
                new DateTime(2026, 8, 5), 1797, "Paid", "Campus Pickup", "Ready for Pickup",
                Item(3, 2), Item(2, 1)),
            Order("NUBE-1023", "Juan dela Cruz", "juan.delacruz@example.com",
                new DateTime(2026, 8, 4), 999, "Paid", "Campus Pickup", "Processing",
                Item(6, 1)),
            Order("NUBE-1022", "Ana Reyes", "ana.reyes@example.com",
                new DateTime(2026, 8, 3), 848, "Pending", "Delivery", "Pending",
                Item(7, 1), Item(1, 1)),
            Order("NUBE-1021", "Carlo Mendoza", "carlo.mendoza@example.com",
                new DateTime(2026, 8, 2), 2246, "Paid", "Campus Pickup", "Completed",
                Item(9, 1), Item(3, 1), Item(2, 1), Item(1, 1)),
            Order("NUBE-1020", "Sofia Lim", "sofia.lim@example.com",
                new DateTime(2026, 8, 1), 828, "Paid", "Campus Pickup", "Completed",
                Item(8, 1), Item(5, 1)),
            Order("NUBE-1019", "Mark Torres", "mark.torres@example.com",
                new DateTime(2026, 7, 30), 299, "Paid", "Campus Pickup", "Cancelled",
                Item(2, 1)),
            Order("NUBE-1018", "Bianca Garcia", "bianca.garcia@example.com",
                new DateTime(2026, 7, 29), 1348, "Paid", "Delivery", "Completed",
                Item(7, 1), Item(3, 1)),
            Order("NUBE-1017", "Diego Villanueva", "diego.villanueva@example.com",
                new DateTime(2026, 7, 28), 947, "Pending", "Campus Pickup", "Confirmed",
                Item(4, 2), Item(1, 1))
        ];
    }

    public IReadOnlyList<AdminOrder> All => _orders;

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

    public bool UpdateStatus(string id, string status)
    {
        var order = GetById(id);
        if (order is null) return false;
        order.Status = status;
        OnChange?.Invoke();
        return true;
    }

    private static AdminOrder Order(
        string id,
        string customer,
        string email,
        DateTime date,
        decimal total,
        string payment,
        string fulfillment,
        string status,
        params AdminOrderItem[] items) =>
        new()
        {
            Id = id,
            CustomerName = customer,
            CustomerEmail = email,
            Date = date,
            Total = total,
            PaymentStatus = payment,
            Fulfillment = fulfillment,
            Status = status,
            Items = [.. items]
        };

    private static AdminOrderItem Item(int productId, int quantity)
    {
        var product = MockData.Products.First(p => p.Id == productId);
        return new AdminOrderItem
        {
            ProductId = product.Id,
            Name = product.Name,
            ImageUrl = product.ImageUrl,
            Quantity = quantity,
            Price = product.Price
        };
    }
}
