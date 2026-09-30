using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminReviewService
{
    public const int PageSize = 10;

    private readonly IAppDatabase _db;
    private readonly AdminProductService _products;
    private readonly AdminOrderService _orders;
    private readonly ProductCatalogService _catalog;
    private readonly List<ProductReview> _reviews = [];
    private bool _loaded;

    public event Action? OnChange;

    public AdminReviewService(
        IAppDatabase db,
        AdminProductService products,
        AdminOrderService orders,
        ProductCatalogService catalog)
    {
        _db = db;
        _products = products;
        _orders = orders;
        _catalog = catalog;
    }

    public IReadOnlyList<ProductReview> All => _reviews;

    public int TotalReviews => _reviews.Count;

    public double AverageRating =>
        _reviews.Count == 0
            ? 0
            : Math.Round(_reviews.Average(r => r.Rating), 1, MidpointRounding.AwayFromZero);

    public int ProductsReviewed =>
        _reviews.Select(r => r.ProductId).Where(id => id > 0).Distinct().Count();

    public int VisibleCount => _reviews.Count(r => r.IsVisible);
    public int HiddenCount => _reviews.Count(r => !r.IsVisible);

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        await _products.EnsureLoadedAsync();
        await _orders.EnsureLoadedAsync();
        var rows = await _db.GetAllProductReviewsAsync();
        Enrich(rows);
        _reviews.Clear();
        _reviews.AddRange(rows.OrderByDescending(r => r.Date).ThenByDescending(r => r.Id));
        _loaded = true;
        OnChange?.Invoke();
    }

    public IEnumerable<ProductReview> ForProduct(int productId) =>
        _reviews.Where(r => r.ProductId == productId);

    public ProductReview? GetById(long id) =>
        _reviews.FirstOrDefault(r => r.Id == id);

    public IEnumerable<ProductReview> Filter(
        string? search,
        int? rating,
        int? productId,
        string? status)
    {
        IEnumerable<ProductReview> query = _reviews;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            query = query.Where(r =>
                (r.ProductName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || r.PublicAuthor.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (r.Title?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.Comment?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.OrderId?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (rating is >= 1 and <= 5)
            query = query.Where(r => r.Rating == rating);

        if (productId is > 0)
            query = query.Where(r => r.ProductId == productId);

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var visible = status.Equals("Visible", StringComparison.OrdinalIgnoreCase);
            query = query.Where(r => r.IsVisible == visible);
        }

        return query.OrderByDescending(r => r.Date).ThenByDescending(r => r.Id);
    }

    public async Task<ProductReview> SetVisibilityAsync(long reviewId, bool isVisible)
    {
        var updated = await _db.SetProductReviewVisibilityAsync(reviewId, isVisible);
        var existing = _reviews.FirstOrDefault(r => r.Id == reviewId);
        if (existing is not null)
        {
            existing.IsVisible = updated.IsVisible;
            if (existing.ProductId <= 0)
                existing.ProductId = updated.ProductId;
        }
        else
        {
            Enrich([updated]);
            _reviews.Insert(0, updated);
        }

        var productId = existing?.ProductId > 0 ? existing.ProductId : updated.ProductId;
        await RefreshProductStatsAsync(productId);
        OnChange?.Invoke();
        return existing ?? updated;
    }

    private async Task RefreshProductStatsAsync(int productId)
    {
        if (productId <= 0) return;

        try
        {
            var reviews = await _db.GetProductReviewsAsync(productId);
            var visible = reviews.Where(r => r.IsVisible).ToList();
            var rating = visible.Count == 0
                ? 0
                : Math.Round(visible.Average(r => r.Rating), 1, MidpointRounding.AwayFromZero);

            var adminProduct = _products.GetById(productId);
            if (adminProduct is not null)
            {
                adminProduct.Rating = rating;
                adminProduct.Reviews = visible.Count;
            }

            var catalogProduct = _catalog.GetById(productId);
            if (catalogProduct is not null)
                ProductReviewStats.Apply(catalogProduct, reviews);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }

    private void Enrich(IEnumerable<ProductReview> reviews)
    {
        var productMap = _products.All.ToDictionary(p => p.Id);
        var itemMap = _orders.All
            .SelectMany(o => o.Items.Select(i => (OrderId: o.Id, Item: i)))
            .Where(x => x.Item.Id > 0)
            .GroupBy(x => x.Item.Id)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var review in reviews)
        {
            if (productMap.TryGetValue(review.ProductId, out var product))
            {
                review.ProductName = product.Name;
                review.ProductImageUrl = product.ImageUrl;
                review.ProductCategory = product.Category;
            }
            else
            {
                review.ProductName ??= review.ProductId > 0 ? $"Product #{review.ProductId}" : "Unknown product";
                review.ProductImageUrl ??= CatalogHelpers.PlaceholderImage;
                review.ProductCategory ??= "—";
            }

            if (review.OrderItemId > 0 && itemMap.TryGetValue(review.OrderItemId, out var match))
            {
                review.PurchasedColor = match.Item.ColorName;
                review.PurchasedSize = match.Item.Size;
                if (string.IsNullOrWhiteSpace(review.OrderId))
                    review.OrderId = match.OrderId;
            }
        }
    }
}
