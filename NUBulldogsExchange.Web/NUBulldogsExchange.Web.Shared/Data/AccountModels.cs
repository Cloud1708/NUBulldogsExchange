namespace NUBulldogsExchange.Web.Shared.Data;

public class MockOrderItem
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal Price { get; set; }
    public string Size { get; set; } = "Free Size";
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
    public string FormattedDate => Date.ToString("MMM d, yyyy");
    public string ItemCountLabel => ItemCount == 1 ? "1 item" : $"{ItemCount} items";
    public bool IsActive => Status is "Pending" or "Processing" or "Ready for Pickup" or "Confirmed";
    public bool CanCancel => Status == "Pending";

    public string StatusKey => Status switch
    {
        "Ready for Pickup" => "ready",
        "Processing" => "processing",
        "Pending" => "pending",
        "Confirmed" => "confirmed",
        "Completed" => "completed",
        "Cancelled" => "cancelled",
        _ => "pending"
    };

    public static MockOrder FromAdmin(AdminOrder order) => new()
    {
        Id = order.Id,
        Date = order.Date,
        Status = order.Status,
        Total = order.Total,
        Items = order.Items.Select(i => new MockOrderItem
        {
            ProductId = i.ProductId,
            Name = i.Name,
            ImageUrl = i.ImageUrl,
            Quantity = i.Quantity,
            Price = i.Price,
            Size = "M"
        }).ToList()
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

public record AdminStatCard(
    string Label,
    string Value,
    string Change,
    bool IsPositive,
    string Icon,
    string Tone);

public record AdminOrderRow(
    string Id,
    string Customer,
    string Fulfillment,
    string Date,
    decimal Total,
    string Status);

public record AdminTopProduct(
    int Rank,
    string Name,
    string ImageUrl,
    int Sold,
    decimal Revenue,
    string ShortLabel);

public record AdminStatusSlice(string Label, int Count, string Color);

public static class AdminStatusHelper
{
    public static string StatusKey(string status) => status switch
    {
        "Ready for Pickup" => "ready",
        "Processing" => "processing",
        "Pending" => "pending",
        "Completed" => "completed",
        "Cancelled" => "cancelled",
        "Confirmed" => "confirmed",
        _ => "pending"
    };
}
