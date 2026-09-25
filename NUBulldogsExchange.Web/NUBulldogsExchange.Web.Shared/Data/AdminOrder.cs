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
    public string CustomerPhone { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Total { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public string? PromotionId { get; set; }
    public string? PromotionCode { get; set; }
    public string PaymentStatus { get; set; } = "Paid";
    public string PaymentMethod { get; set; } = string.Empty;
    public string Fulfillment { get; set; } = "Campus Pickup";
    public string Status { get; set; } = "Pending";
    public string? AuthUserId { get; set; }
    public string? OrderNotes { get; set; }
    public string? AdminRemarks { get; set; }
    public string? ShippingRecipientName { get; set; }
    public string? ShippingPhone { get; set; }
    public string? ShippingAddressLine { get; set; }
    public string? ShippingBarangay { get; set; }
    public string? ShippingCity { get; set; }
    public string? ShippingProvince { get; set; }
    public string? ShippingPostalCode { get; set; }
    public List<AdminOrderItem> Items { get; set; } = [];

    public string DateLabel => Date.ToString("yyyy-MM-dd");
    public int ItemCount => Items.Sum(i => i.Quantity);
    public string ItemCountLabel => ItemCount == 1 ? "1 item" : $"{ItemCount} items";

    public bool IsDelivery => OrderFlow.IsDelivery(Fulfillment);
    public string ShippingAddressLabel => OrderFlow.FormatShippingAddress(this);
    public string RecipientName =>
        string.IsNullOrWhiteSpace(ShippingRecipientName) ? CustomerName : ShippingRecipientName;
    public string RecipientPhone =>
        string.IsNullOrWhiteSpace(ShippingPhone) ? CustomerPhone : ShippingPhone;

    public string StatusKey => OrderFlow.OperationalStatusKey(Status);

    public string PaymentKey => PaymentStatus switch
    {
        "Paid" => "paid",
        "Pending" => "pending",
        "Failed" => "failed",
        "Refunded" => "refunded",
        "Cancelled" => "failed",
        _ => "pending"
    };
}
