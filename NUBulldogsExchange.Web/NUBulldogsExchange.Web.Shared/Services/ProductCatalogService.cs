using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

/// <summary>
/// In-memory storefront catalog backed by <see cref="IAppDatabase"/>.
/// Replaces the former static MockData product list.
/// </summary>
public class ProductCatalogService
{
    public static readonly string[] ApparelSizes = ["XS", "S", "M", "L", "XL", "XXL"];
    public static readonly string[] OneSize = ["One Size"];

    private readonly IAppDatabase _db;
    private readonly List<Product> _products = [];
    private readonly List<CategoryItem> _categories = [];
    private bool _loaded;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyDictionary<int, int>? _fulfilledUnits;

    public event Action? OnChange;

    public ProductCatalogService(IAppDatabase db)
    {
        _db = db;
    }

    public IReadOnlyList<Product> Products => _products;
    public IReadOnlyList<CategoryItem> Categories => _categories;

    /// <summary>Fulfilled units (Completed pickup / Delivered delivery), loaded once per catalog session.</summary>
    public async Task EnsureFulfilledSalesAsync()
    {
        if (_fulfilledUnits is not null) return;
        _fulfilledUnits = await _db.GetFulfilledUnitsSoldAsync() ?? new Dictionary<int, int>();
    }

    public int FulfilledUnits(int productId) =>
        _fulfilledUnits is not null && _fulfilledUnits.TryGetValue(productId, out var units) ? units : 0;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await _gate.WaitAsync();
        try
        {
            if (_loaded) return;
            await ReloadAsync();
            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ReloadAsync()
    {
        var products = await _db.GetPublishedProductsAsync();
        var categories = await _db.GetCategoriesAsync();

        _products.Clear();
        _products.AddRange(products
            .Where(p => p.IsPublished && (string.IsNullOrEmpty(p.Status) || p.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)))
            .Select(NormalizeProduct));

        if (_products.Count == 0)
        {
            var allProds = await _db.GetProductsAsync();
            _products.AddRange(allProds
                .Where(p => string.IsNullOrEmpty(p.Status) || !p.Status.Equals("Archived", StringComparison.OrdinalIgnoreCase))
                .Select(NormalizeProduct));
        }

        await AttachVariantsAsync(_products);

        _categories.Clear();
        foreach (var c in categories.Where(c => c.IsActive))
        {
            var count = _products.Count(p => p.Category.Equals(c.Name, StringComparison.OrdinalIgnoreCase));
            _categories.Add(new CategoryItem
            {
                Name = c.Name,
                Slug = c.Slug,
                ImageUrl = string.IsNullOrWhiteSpace(c.ImageUrl) ? CatalogHelpers.PlaceholderImage : c.ImageUrl,
                Count = count,
                ItemCount = count == 1 ? "1 item" : $"{count} items"
            });
        }

        OnChange?.Invoke();
    }

    private async Task AttachVariantsAsync(List<Product> products)
    {
        if (products.Count == 0) return;

        try
        {
            var rows = await _db.GetProductVariantsByProductIdsAsync(products.Select(p => p.Id));
            var byProduct = rows.GroupBy(v => v.ProductId)
                .ToDictionary(g => g.Key, g => g.Select(v => v.Clone()).ToList());

            foreach (var product in products)
            {
                if (!byProduct.TryGetValue(product.Id, out var variants))
                    variants = [];

                product.Variants = variants;
                if (product.HasVariants)
                {
                    product.Sizes = product.Variants
                        .Where(v => v.IsActive && !string.IsNullOrWhiteSpace(v.Size))
                        .Select(v => v.Size.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    product.Colors = product.Variants
                        .Where(v => v.IsActive && !string.IsNullOrWhiteSpace(v.ColorName))
                        .Select(v => v.ColorName!.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    product.Stock = product.Variants.Where(v => v.IsActive).Sum(v => Math.Max(0, v.StockQuantity));
                    product.InStock = product.Stock > 0;
                }
            }
        }
        catch
        {
            // Table may not exist yet before migration; catalog still works without variants.
        }
    }

    public void ApplyPurchase(IEnumerable<AdminOrderItem> items) =>
        ApplyOrderStock(items, restore: false);

    public void ApplyCancellation(IEnumerable<AdminOrderItem> items) =>
        ApplyOrderStock(items, restore: true);

    private void ApplyOrderStock(IEnumerable<AdminOrderItem> items, bool restore)
    {
        foreach (var item in items)
        {
            var product = GetById(item.ProductId);
            if (product is null) continue;
            ProductVariantLogic.ApplyOrderItemStock(product, item, restore);
        }

        OnChange?.Invoke();
    }

    public Product? GetById(int id) => _products.FirstOrDefault(p => p.Id == id);

    public void ReplaceAll(IEnumerable<Product> products)
    {
        _products.Clear();
        _products.AddRange(products.Select(NormalizeProduct));
        OnChange?.Invoke();
    }

    public void Upsert(Product product)
    {
        var normalized = NormalizeProduct(product);
        var index = _products.FindIndex(p => p.Id == normalized.Id);
        if (index >= 0)
            _products[index] = normalized;
        else
            _products.Add(normalized);
        OnChange?.Invoke();
    }

    public void Remove(int id)
    {
        _products.RemoveAll(p => p.Id == id);
        OnChange?.Invoke();
    }

    public IEnumerable<Product> Search(IEnumerable<Product> source, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return source;

        var q = query.Trim();
        return source.Where(p => Matches(p, q));
    }

    public IEnumerable<Product> Featured
    {
        get
        {
            var items = _products.Where(p => p.IsFeatured).Take(8).ToList();
            if (items.Count > 0)
                return items;
            return _products.Take(8);
        }
    }

    public IEnumerable<Product> FreshDrops
    {
        get
        {
            var items = _products.Where(p => p.IsFreshDrop || p.IsNewArrival || p.Badge == "New").Take(4).ToList();
            if (items.Count > 0)
                return items;
            return _products.Count > 4 ? _products.Skip(4).Take(4) : _products.Take(4);
        }
    }

    public IEnumerable<Product> Favorites
    {
        get
        {
            var items = _products.Where(p => p.IsFavorite || p.IsBestSeller || p.Rating >= 4.5).Take(4).ToList();
            if (items.Count > 0)
                return items;
            return _products.OrderByDescending(p => p.Rating).ThenByDescending(p => p.Sold).Take(4);
        }
    }

    public IEnumerable<Product> BestSellers
    {
        get
        {
            var items = _products.Where(p => p.IsBestSeller || p.Badge == "Best Seller").ToList();
            if (items.Count > 0)
                return items;
            return _products.OrderByDescending(p => p.Sold).ThenByDescending(p => p.Rating);
        }
    }

    public IEnumerable<Product> NewArrivals
    {
        get
        {
            var items = _products.Where(p => p.IsNewArrival || p.Badge == "New" || p.IsFreshDrop).ToList();
            if (items.Count > 0)
                return items;
            return _products.OrderByDescending(p => p.Id);
        }
    }

    public IEnumerable<Product> Apparel => _products.Where(p =>
        p.Section == "apparel" ||
        p.Category is "T-Shirts" or "Polo Shirts" or "Hoodies" or "Jackets");

    public IEnumerable<Product> Accessories => _products.Where(p =>
        p.Section == "accessories" ||
        p.Category is "Accessories" or "Caps" or "Bags" or "Tumblers" or "School Supplies");

    private static readonly string[] SchoolEssentialsNames = ["School Essentials", "School Supplies"];

    /// <summary>Published Active products only (excludes the empty-catalog fallback rows).</summary>
    public IEnumerable<Product> StorefrontProducts => _products.Where(IsStorefrontActive);

    /// <summary>School Essentials category when it exists and has active products; drives optional nav links.</summary>
    public CategoryItem? SchoolEssentialsCategory => GetShopCategories()
        .FirstOrDefault(c => SchoolEssentialsNames.Contains(c.Name, StringComparer.OrdinalIgnoreCase));

    /// <summary>Active categories that have at least one active product, with a representative image.</summary>
    public List<CategoryItem> GetShopCategories()
    {
        var byCategory = StorefrontProducts
            .GroupBy(p => p.Category.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var result = new List<CategoryItem>();
        foreach (var category in _categories)
        {
            if (!byCategory.TryGetValue(category.Name.Trim(), out var products) || products.Count == 0)
                continue;

            var image = HasRealImage(category.ImageUrl)
                ? category.ImageUrl
                : products.Select(p => p.ImageUrl).FirstOrDefault(HasRealImage) ?? CatalogHelpers.PlaceholderImage;

            result.Add(new CategoryItem
            {
                Name = category.Name,
                Slug = category.Slug,
                ImageUrl = image,
                Count = products.Count,
                ItemCount = products.Count == 1 ? "1 item" : $"{products.Count} items"
            });
        }

        return result;
    }

    /// <summary>Newest active products by created_at, then published_at, then id.</summary>
    public List<Product> GetLatest(int take) => StorefrontProducts
        .OrderByDescending(p => p.CreatedAt ?? p.PublishedAt ?? DateTime.MinValue)
        .ThenByDescending(p => p.Id)
        .Take(take)
        .ToList();

    public async Task<StorefrontHome> LoadHomeAsync(int productCount = 5, int reviewCount = 3)
    {
        await EnsureLoadedAsync();

        var salesTask = _db.GetFulfilledUnitsSoldAsync();
        var reviewsTask = _db.GetPublicReviewsAsync(reviewCount * 4);
        var promoTask = _db.GetStorefrontPromotionAsync();
        await Task.WhenAll(
            salesTask.ContinueWith(_ => { }),
            reviewsTask.ContinueWith(_ => { }),
            promoTask.ContinueWith(_ => { }));

        var unitsSold = Result(salesTask, null);
        var reviews = Result(reviewsTask, []);
        var promotion = Result(promoTask, null);

        var bestSellers = unitsSold is null
            ? []
            : StorefrontProducts
                .Where(p => unitsSold.TryGetValue(p.Id, out var units) && units > 0)
                .OrderByDescending(p => unitsSold[p.Id])
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Take(productCount)
                .Select((p, index) => new RankedProduct { Product = p, Rank = index + 1, UnitsSold = unitsSold[p.Id] })
                .ToList();

        var freshDrops = GetLatest(productCount);
        var (heroMain, heroSide) = PickHeroProducts(bestSellers.Select(b => b.Product).Concat(freshDrops));

        return new StorefrontHome
        {
            Categories = GetShopCategories().Take(6).ToList(),
            FreshDrops = freshDrops,
            BestSellers = bestSellers,
            UnitsSold = unitsSold,
            Reviews = BuildHomeReviews(reviews, reviewCount),
            Promotion = BuildHomePromotion(promotion, bestSellers.Select(b => b.Product).Concat(freshDrops)),
            HeroMain = heroMain,
            HeroSide = heroSide
        };
    }

    private (Product? Main, List<Product> Side) PickHeroProducts(IEnumerable<Product> preferred)
    {
        var pool = preferred
            .Concat(StorefrontProducts.Where(p => p.IsFeatured))
            .Concat(StorefrontProducts)
            .Where(p => HasRealImage(p.ImageUrl))
            .DistinctBy(p => p.Id)
            .ToList();

        if (pool.Count == 0)
            return (null, []);

        var hoodie = pool.FirstOrDefault(p => MentionsAny(p, "hoodie"));
        var cap = pool.FirstOrDefault(p => MentionsAny(p, "cap", "caps", "hat"));
        var main = pool.FirstOrDefault(p => p != hoodie && p != cap) ?? pool[0];

        var side = new List<Product>();
        foreach (var candidate in new[] { hoodie, cap }.Concat(pool))
        {
            if (side.Count == 2) break;
            if (candidate is null || candidate.Id == main.Id || side.Any(s => s.Id == candidate.Id)) continue;
            side.Add(candidate);
        }

        return (main, side);
    }

    private List<HomeReview> BuildHomeReviews(IEnumerable<ProductReview> reviews, int take) => reviews
        .Where(r => r.IsVisible && !string.IsNullOrWhiteSpace(r.Comment))
        .OrderByDescending(r => r.IsVerifiedPurchase)
        .ThenByDescending(r => r.Date)
        .Take(take)
        .Select(r =>
        {
            var parts = r.PublicAuthor.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var display = parts.Length > 1 ? $"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}." : r.PublicAuthor;
            var initials = parts.Length > 1
                ? $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}"
                : char.ToUpperInvariant(r.PublicAuthor[0]).ToString();
            var comment = r.Comment.Trim();
            var title = r.Title?.Trim();
            var text = string.IsNullOrWhiteSpace(title) || comment.StartsWith(title, StringComparison.OrdinalIgnoreCase)
                ? comment
                : $"{title} — {comment}";

            return new HomeReview
            {
                Id = r.Id,
                DisplayName = display,
                Initials = initials,
                Rating = Math.Clamp(r.Rating, 1, 5),
                Text = text.Length <= 160 ? text : text[..157].TrimEnd() + "…",
                IsVerifiedPurchase = r.IsVerifiedPurchase,
                ProductId = r.ProductId,
                ProductName = GetById(r.ProductId) is { } product && IsStorefrontActive(product) ? product.Name : null
            };
        })
        .ToList();

    private HomePromotion? BuildHomePromotion(AdminPromotion? promotion, IEnumerable<Product> fallback)
    {
        if (promotion is null)
            return null;

        var linked = promotion.ProductIds
            .Select(GetById)
            .Where(p => p is not null && IsStorefrontActive(p) && HasRealImage(p.ImageUrl))
            .Cast<Product>()
            .ToList();

        var products = (linked.Count > 0 ? linked : fallback.Where(p => HasRealImage(p.ImageUrl)))
            .DistinctBy(p => p.Id)
            .Take(3)
            .ToList();

        return new HomePromotion
        {
            Promotion = promotion,
            Products = products,
            Href = linked.Count == 1 ? $"/product/{linked[0].Id}" : "/shop"
        };
    }

    private static T Result<T>(Task<T> task, T fallback)
    {
        if (task.IsCompletedSuccessfully)
            return task.Result;
        if (task.Exception is not null)
            Console.Error.WriteLine(task.Exception.GetBaseException());
        return fallback;
    }

    private static bool IsStorefrontActive(Product p) =>
        p.IsPublished && (string.IsNullOrEmpty(p.Status) || p.Status.Equals("Active", StringComparison.OrdinalIgnoreCase));

    public static bool HasRealImage(string? url) =>
        !string.IsNullOrWhiteSpace(url) && !string.Equals(url, CatalogHelpers.PlaceholderImage, StringComparison.Ordinal);

    private static bool MentionsAny(Product p, params string[] words)
    {
        var tokens = $"{p.Name} {p.Category}"
            .Split([' ', '-', '/', '&', ','], StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(t => words.Any(w => t.Equals(w, StringComparison.OrdinalIgnoreCase)
                                             || t.Equals(w + "s", StringComparison.OrdinalIgnoreCase)));
    }

    public IEnumerable<Product> GetRelated(Product product, int take = 4)
    {
        var related = _products
            .Where(p => p.Id != product.Id &&
                        (p.Category == product.Category || p.Section == product.Section))
            .Take(take)
            .ToList();

        if (related.Count >= take)
            return related;

        var fillers = _products
            .Where(p => p.Id != product.Id &&
                        related.All(r => r.Id != p.Id) &&
                        (p.IsFeatured || p.IsBestSeller || p.IsNewArrival))
            .OrderByDescending(p => p.Sold)
            .Take(take - related.Count);

        related.AddRange(fillers);
        return related;
    }

    private static Product NormalizeProduct(Product product)
    {
        product.Variants ??= [];

        if (product.HasVariants)
        {
            product.Sizes = product.Variants
                .Where(v => v.IsActive && !string.IsNullOrWhiteSpace(v.Size))
                .Select(v => v.Size.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            product.Colors = product.Variants
                .Where(v => v.IsActive && !string.IsNullOrWhiteSpace(v.ColorName))
                .Select(v => v.ColorName!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            product.Stock = product.Variants.Where(v => v.IsActive).Sum(v => Math.Max(0, v.StockQuantity));
        }
        else
        {
            product.Sizes = [];
        }

        if (product.Images.Count == 0 && !string.IsNullOrWhiteSpace(product.ImageUrl))
            product.Images = [product.ImageUrl];

        if (string.IsNullOrWhiteSpace(product.ImageUrl))
            product.ImageUrl = CatalogHelpers.PlaceholderImage;

        if (string.IsNullOrWhiteSpace(product.FullDescription))
            product.FullDescription = product.Description;

        product.InStock = product.Stock > 0;
        return product;
    }

    private static bool Matches(Product product, string query)
    {
        if (product.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;
        if (product.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;
        if (product.Sku.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!string.IsNullOrWhiteSpace(product.Description) &&
            product.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(product.Section)
            && product.Section.Contains(query, StringComparison.OrdinalIgnoreCase)
            && query.Length >= 4)
            return true;

        foreach (var word in product.Category.Split([' ', '-', '/', '&'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                return true;

            if (query.Length >= 4 && word.Contains(query, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
