namespace NUBulldogsExchange.Web.Shared.Data;

public class AdminOrderItem
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal Price { get; set; }
    public int? VariantId { get; set; }
    public string? Size { get; set; }
    public string? VariantSku { get; set; }
}

public class AdminOrder
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Total { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? PromotionId { get; set; }
    public string? PromotionCode { get; set; }
    public string PaymentStatus { get; set; } = "Paid";
    public string Fulfillment { get; set; } = "Campus Pickup";
    public string Status { get; set; } = "Pending";
    public List<AdminOrderItem> Items { get; set; } = [];

    public string DateLabel => Date.ToString("yyyy-MM-dd");
    public int ItemCount => Items.Sum(i => i.Quantity);
    public string ItemCountLabel => ItemCount == 1 ? "1 item" : $"{ItemCount} items";

    public string StatusKey => Status switch
    {
        "Ready for Pickup" => "ready",
        "Processing" => "processing",
        "Pending" => "pending",
        "Completed" => "completed",
        "Cancelled" => "cancelled",
        "Confirmed" => "confirmed",
        _ => "pending"
    };

    public string PaymentKey => PaymentStatus switch
    {
        "Paid" => "paid",
        "Pending" => "pending",
        "Failed" => "failed",
        "Refunded" => "refunded",
        _ => "pending"
    };
}
