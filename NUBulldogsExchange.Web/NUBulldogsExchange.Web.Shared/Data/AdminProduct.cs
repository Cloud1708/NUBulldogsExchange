namespace NUBulldogsExchange.Web.Shared.Data;

public class AdminProduct
{
    public const int LowStockThreshold = 20;

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int Sold { get; set; }
    public string Status { get; set; } = "Active";
    public string ImageUrl { get; set; } = string.Empty;
    public List<string> Images { get; set; } = [];
    public string Description { get; set; } = string.Empty;
    public List<string> Colors { get; set; } = [];
    public List<string> Sizes { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsActive => Status.Equals("Active", StringComparison.OrdinalIgnoreCase);
    public bool IsDraft => Status.Equals("Draft", StringComparison.OrdinalIgnoreCase);
    public bool IsInactive => Status.Equals("Inactive", StringComparison.OrdinalIgnoreCase);

    public string StatusCssClass => IsActive ? "is-active" : IsDraft ? "is-draft" : "is-inactive";

    public string StockState => Stock switch
    {
        <= 0 => "Out of Stock",
        <= LowStockThreshold => "Low Stock",
        _ => "In Stock"
    };

    public string StockStateKey => Stock switch
    {
        <= 0 => "out",
        <= LowStockThreshold => "low",
        _ => "in"
    };

    public AdminProduct Clone() => new()
    {
        Id = Id,
        Name = Name,
        Sku = Sku,
        Category = Category,
        Price = Price,
        Stock = Stock,
        Sold = Sold,
        Status = Status,
        ImageUrl = ImageUrl,
        Images = [.. Images],
        Description = Description,
        Colors = [.. Colors],
        Sizes = [.. Sizes],
        CreatedAt = CreatedAt
    };

    public static AdminProduct FromProduct(Product product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Sku = product.Sku,
        Category = product.Category,
        Price = product.Price,
        Stock = product.Stock,
        Sold = product.Sold,
        Status = product.Stock == 0 && !product.InStock ? "Inactive" : "Active",
        ImageUrl = product.ImageUrl,
        Images = product.Images.Count > 0
            ? [.. product.Images]
            : string.IsNullOrWhiteSpace(product.ImageUrl) ? [] : [product.ImageUrl],
        Description = string.IsNullOrWhiteSpace(product.FullDescription)
            ? product.Description
            : product.FullDescription,
        Colors = [.. product.Colors],
        Sizes = [.. product.Sizes]
    };
}
