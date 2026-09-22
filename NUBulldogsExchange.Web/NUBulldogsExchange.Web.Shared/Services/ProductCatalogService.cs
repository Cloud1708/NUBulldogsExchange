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

    public event Action? OnChange;

    public ProductCatalogService(IAppDatabase db)
    {
        _db = db;
    }

    public IReadOnlyList<Product> Products => _products;
    public IReadOnlyList<CategoryItem> Categories => _categories;

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
                {
                    product.Variants = [];
                    continue;
                }

                product.Variants = variants;
                if (product.HasSizeVariants)
                {
                    product.Sizes = product.Variants
                        .Where(v => v.IsActive)
                        .Select(v => v.Size)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    product.Stock = product.Variants.Sum(v => Math.Max(0, v.StockQuantity));
                    product.InStock = product.Stock > 0;
                }
            }
        }
        catch
        {
            // Table may not exist yet before migration; catalog still works without variants.
        }
    }

    public void ApplyPurchase(IEnumerable<AdminOrderItem> items)
    {
        foreach (var item in items)
        {
            var product = GetById(item.ProductId);
            if (product is null) continue;

            if (item.VariantId is int variantId && product.Variants.Count > 0)
            {
                var variant = product.Variants.FirstOrDefault(v => v.Id == variantId);
                if (variant is not null)
                    variant.StockQuantity = Math.Max(0, variant.StockQuantity - item.Quantity);
                product.Stock = product.Variants.Sum(v => Math.Max(0, v.StockQuantity));
            }
            else
            {
                product.Stock = Math.Max(0, product.Stock - item.Quantity);
            }

            product.Sold += item.Quantity;
            product.InStock = product.Stock > 0;
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

        if (product.HasSizeVariants)
        {
            product.Sizes = product.Variants
                .Where(v => v.IsActive)
                .Select(v => v.Size)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            product.Stock = product.Variants.Sum(v => Math.Max(0, v.StockQuantity));
        }
        else if (product.Sizes.Count == 0)
        {
            // No invented apparel sizes — empty means no size selector required.
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
