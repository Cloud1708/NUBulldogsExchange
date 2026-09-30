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
    public List<ProductVariant> Variants { get; set; } = [];
    public bool HasVariants => Variants.Count > 0;
    public bool HasSizeVariants => Variants.Any(v => !string.IsNullOrWhiteSpace(v.Size));
    public bool HasColorVariants => Variants.Any(v => !string.IsNullOrWhiteSpace(v.ColorName));
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
    public long Id { get; set; }
    public int ProductId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public int Rating { get; set; } = 5;
    public DateTime Date { get; set; }
    public string? Title { get; set; }
    public string Comment { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string? OrderId { get; set; }
    public long OrderItemId { get; set; }
    public string? AuthUserId { get; set; }

    public bool IsVerifiedPurchase => OrderItemId > 0 && !string.IsNullOrWhiteSpace(OrderId);

    /// <summary>Public display name. Never shows an email address.</summary>
    public string PublicAuthor
    {
        get
        {
            var source = Author?.Trim() ?? string.Empty;
            var at = source.IndexOf('@');
            if (at > 0)
                source = source[..at].Trim();
            return string.IsNullOrWhiteSpace(source) ? "Customer" : source;
        }
    }
}

public static class ProductReviewStats
{
    public const int MinCommentLength = 5;
    public const int MaxCommentLength = 500;
    public const int MaxTitleLength = 100;

    public static readonly string[] FeedbackTags =
    [
        "Good Quality",
        "Comfortable",
        "True to Size",
        "Worth the Price",
        "Nice Design"
    ];

    public static List<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null) return [];
        var allowed = new HashSet<string>(FeedbackTags, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var raw in tags)
        {
            var value = raw?.Trim();
            if (string.IsNullOrWhiteSpace(value)) continue;
            var match = FeedbackTags.FirstOrDefault(t => t.Equals(value, StringComparison.OrdinalIgnoreCase));
            if (match is null || result.Contains(match, StringComparer.OrdinalIgnoreCase))
                continue;
            result.Add(match);
            if (result.Count >= FeedbackTags.Length) break;
        }

        return result;
    }

    public static void Apply(Product product, IReadOnlyList<ProductReview> reviews)
    {
        product.ProductReviews = reviews.ToList();
        product.Reviews = reviews.Count;
        product.Rating = reviews.Count == 0
            ? 0
            : Math.Round(reviews.Average(r => r.Rating), 1, MidpointRounding.AwayFromZero);

        var counts = new int[5];
        foreach (var review in reviews)
        {
            if (review.Rating is >= 1 and <= 5)
                counts[5 - review.Rating]++;
        }

        product.RatingBreakdown = reviews.Count == 0
            ? [0, 0, 0, 0, 0]
            : counts.Select(c => (int)Math.Round(100.0 * c / reviews.Count, MidpointRounding.AwayFromZero)).ToArray();
    }
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
    public int? VariantId { get; set; }

    public int AvailableStock
    {
        get
        {
            if (VariantId is int vid && Product.Variants.Count > 0)
            {
                var variant = Product.Variants.FirstOrDefault(v => v.Id == vid);
                if (variant is not null)
                    return Math.Max(0, variant.StockQuantity);
            }

            return Math.Max(0, Product.Stock);
        }
    }
}
