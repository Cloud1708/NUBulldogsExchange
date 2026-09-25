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
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public string PaymentStatus { get; set; } = "Pending";
    public string PaymentMethod { get; set; } = string.Empty;
    public string Fulfillment { get; set; } = OrderFlow.CampusPickup;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? ShippingRecipientName { get; set; }
    public string? ShippingPhone { get; set; }
    public string? ShippingAddressLine { get; set; }
    public string? ShippingBarangay { get; set; }
    public string? ShippingCity { get; set; }
    public string? ShippingProvince { get; set; }
    public string? ShippingPostalCode { get; set; }
    public List<MockOrderItem> Items { get; set; } = [];

    public int ItemCount => Items.Sum(i => i.Quantity);
    public string DateLabel => Date.ToString("yyyy-MM-dd");
    public string FormattedDate => Date.ToString("MMM d, yyyy");
    public string ItemCountLabel => ItemCount == 1 ? "1 item" : $"{ItemCount} items";
    public bool IsDelivery => OrderFlow.IsDelivery(Fulfillment);
    public bool IsActive => Status is "Pending" or "Processing" or "Ready for Pickup" or "Confirmed"
        or "Preparing" or "Out for Delivery" or "Shipped";
    public bool CanCancel => OrderFlow.CanCustomerCancel(this);
    public string CustomerCategory => OrderFlow.GetCustomerOrderCategory(this);
    public string CustomerStatusKey => OrderFlow.CustomerStatusKey(CustomerCategory);
    public string FulfillmentLocationLabel =>
        IsDelivery
            ? OrderFlow.FormatShippingShort(ShippingBarangay, ShippingCity, ShippingProvince)
            : OrderFlow.PickupLocation;
    public string ShippingAddressLabel => OrderFlow.FormatShippingAddress(this);
    public string RecipientName =>
        string.IsNullOrWhiteSpace(ShippingRecipientName) ? CustomerName : ShippingRecipientName;
    public string RecipientPhone =>
        string.IsNullOrWhiteSpace(ShippingPhone) ? CustomerPhone : ShippingPhone;

    public string StatusKey => OrderFlow.OperationalStatusKey(Status);

    public static MockOrder FromAdmin(AdminOrder order) => new()
    {
        Id = order.Id,
        Date = order.Date,
        Status = order.Status,
        Total = order.Total,
        Subtotal = order.Subtotal > 0 ? order.Subtotal : Math.Max(0, order.Total - order.ShippingFee + order.DiscountAmount),
        DiscountAmount = order.DiscountAmount,
        ShippingFee = order.ShippingFee,
        PaymentStatus = string.IsNullOrWhiteSpace(order.PaymentStatus) ? "Pending" : order.PaymentStatus,
        PaymentMethod = order.PaymentMethod ?? string.Empty,
        Fulfillment = string.IsNullOrWhiteSpace(order.Fulfillment) ? OrderFlow.CampusPickup : order.Fulfillment,
        CustomerName = order.CustomerName ?? string.Empty,
        CustomerPhone = order.CustomerPhone ?? string.Empty,
        ShippingRecipientName = order.ShippingRecipientName,
        ShippingPhone = order.ShippingPhone,
        ShippingAddressLine = order.ShippingAddressLine,
        ShippingBarangay = order.ShippingBarangay,
        ShippingCity = order.ShippingCity,
        ShippingProvince = order.ShippingProvince,
        ShippingPostalCode = order.ShippingPostalCode,
        Items = order.Items.Select(i => new MockOrderItem
        {
            ProductId = i.ProductId,
            Name = i.Name,
            ImageUrl = i.ImageUrl,
            Quantity = i.Quantity,
            Price = i.Price,
            Size = string.IsNullOrWhiteSpace(i.Size) ? "Free Size" : i.Size
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
    public static string StatusKey(string status) => OrderFlow.OperationalStatusKey(status);
}
