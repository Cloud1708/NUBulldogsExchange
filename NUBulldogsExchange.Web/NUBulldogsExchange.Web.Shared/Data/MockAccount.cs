namespace NUBulldogsExchange.Web.Shared.Data;

public class MockOrderItem
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal Price { get; set; }
}

public class MockOrder
{
    public string Id { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Status { get; set; } = "Pending";
    public decimal Total { get; set; }
    public List<MockOrderItem> Items { get; set; } = [];

    public int ItemCount => Items.Sum(i => i.Quantity);
    public string DateLabel => Date.ToString("yyyy-MM-dd");
    public string ItemCountLabel => ItemCount == 1 ? "1 item" : $"{ItemCount} items";
    public bool IsActive => Status is "Pending" or "Processing" or "Ready for Pickup";
    public bool CanCancel => Status == "Pending";

    public string StatusKey => Status switch
    {
        "Ready for Pickup" => "ready",
        "Processing" => "processing",
        "Pending" => "pending",
        "Completed" => "completed",
        "Cancelled" => "cancelled",
        _ => "pending"
    };
}

public class MockNotification
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TimeAgo { get; set; } = string.Empty;
    public string Icon { get; set; } = "bell";
    public string Tone { get; set; } = "blue";
    public bool IsRead { get; set; }
}

public static class MockAccountData
{
    public static List<MockOrder> CreateOrders() =>
    [
        Order("NUBE-1024", new DateTime(2026, 8, 5), "Ready for Pickup", 1797,
            Item(3, 2), Item(2, 1)),
        Order("NUBE-1023", new DateTime(2026, 8, 4), "Processing", 999,
            Item(6, 1)),
        Order("NUBE-1022", new DateTime(2026, 8, 3), "Pending", 848,
            Item(7, 1), Item(1, 1)),
        Order("NUBE-1021", new DateTime(2026, 7, 28), "Ready for Pickup", 1499,
            Item(9, 1)),
        Order("NUBE-1020", new DateTime(2026, 7, 20), "Completed", 899,
            Item(10, 1)),
        Order("NUBE-1019", new DateTime(2026, 7, 12), "Completed", 798,
            Item(3, 1), Item(2, 1)),
        Order("NUBE-1018", new DateTime(2026, 7, 1), "Completed", 598,
            Item(8, 1), Item(5, 1)),
        Order("NUBE-1017", new DateTime(2026, 6, 15), "Cancelled", 357,
            Item(4, 2), Item(11, 1))
    ];

    public static List<MockNotification> CreateNotifications() =>
    [
        new()
        {
            Id = "n1",
            Title = "Order Ready for Pickup",
            Message = "Your order #NUBE-1024 is ready for pickup at the NU Merchandise Center.",
            TimeAgo = "2 hours ago",
            Icon = "package",
            Tone = "blue",
            IsRead = false
        },
        new()
        {
            Id = "n2",
            Title = "New Collection Available",
            Message = "New Bulldog Hoodie Collection is now available. Check it out!",
            TimeAgo = "Yesterday",
            Icon = "bell",
            Tone = "gold",
            IsRead = false
        },
        new()
        {
            Id = "n3",
            Title = "Order Confirmed",
            Message = "Your order #NUBE-1023 has been confirmed and is being processed.",
            TimeAgo = "3 days ago",
            Icon = "check-circle",
            Tone = "green",
            IsRead = true
        }
    ];

    private static MockOrder Order(string id, DateTime date, string status, decimal total, params MockOrderItem[] items) =>
        new()
        {
            Id = id,
            Date = date,
            Status = status,
            Total = total,
            Items = [.. items]
        };

    private static MockOrderItem Item(int productId, int quantity)
    {
        var product = MockData.Products.First(p => p.Id == productId);
        return new MockOrderItem
        {
            ProductId = product.Id,
            Name = product.Name,
            ImageUrl = product.ImageUrl,
            Quantity = quantity,
            Price = product.Price
        };
    }
}
