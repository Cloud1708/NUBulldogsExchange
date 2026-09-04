namespace NUBulldogsExchange.Web.Shared.Data;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public List<string> Images { get; set; } = [];
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public double Rating { get; set; }
    public int Reviews { get; set; }
    public int Sold { get; set; }
    public int Stock { get; set; } = 100;
    public string? Badge { get; set; }
    public List<string> Colors { get; set; } = [];
    public List<string> Sizes { get; set; } = [];
    public string Material { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public bool InStock { get; set; } = true;
    public bool IsFeatured { get; set; }
    public bool IsFreshDrop { get; set; }
    public bool IsBestSeller { get; set; }
    public bool IsNewArrival { get; set; }
    public bool IsFavorite { get; set; }
    public string Description { get; set; } = string.Empty;
    public string FullDescription { get; set; } = string.Empty;
    public List<string> Features { get; set; } = [];
    public List<ProductReview> ProductReviews { get; set; } = [];
    /// <summary>Percentages for 5★ → 1★ rating distribution.</summary>
    public int[] RatingBreakdown { get; set; } = [40, 30, 15, 10, 5];
    public string Section { get; set; } = "apparel"; // apparel | accessories
    public string Status { get; set; } = "Active"; // Active|Draft|Inactive
    public bool IsPublished { get; set; } = true;
    public DateTime? PublishedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public IReadOnlyList<string> GalleryImages =>
        Images.Count > 0 ? Images : string.IsNullOrWhiteSpace(ImageUrl) ? [] : [ImageUrl];
}

public class ProductReview
{
    public string Author { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public int Rating { get; set; } = 5;
    public DateTime Date { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public class CategoryItem
{
    public string Name { get; set; } = string.Empty;
    public string ItemCount { get; set; } = string.Empty;
    public int Count { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public class CartItem
{
    public string Key { get; set; } = Guid.NewGuid().ToString("N");
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; } = 1;
    public string? SelectedColor { get; set; }
    public string? SelectedSize { get; set; }
}
