namespace NUBulldogsExchange.Web.Shared.Data;

public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Size { get; set; } = string.Empty;
    public string? ColorName { get; set; }
    public string? ColorHex { get; set; }
    public string? Sku { get; set; }
    public int StockQuantity { get; set; }
    public decimal PriceAdjustment { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool IsActive =>
        string.IsNullOrWhiteSpace(Status) ||
        Status.Equals("Active", StringComparison.OrdinalIgnoreCase);

    public bool IsInStock => StockQuantity > 0;
    public bool HasSize => !string.IsNullOrWhiteSpace(Size);
    public bool HasColor => !string.IsNullOrWhiteSpace(ColorName);

    public ProductVariant Clone() => new()
    {
        Id = Id,
        ProductId = ProductId,
        Size = Size,
        ColorName = ColorName,
        ColorHex = ColorHex,
        Sku = Sku,
        StockQuantity = StockQuantity,
        PriceAdjustment = PriceAdjustment,
        Status = Status,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt
    };
}
