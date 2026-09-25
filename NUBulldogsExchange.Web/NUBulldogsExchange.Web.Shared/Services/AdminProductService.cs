using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminProductService
{
    public const int PageSize = 10;

    public static readonly string[] Categories =
    [
        "T-Shirts",
        "Polo Shirts",
        "Hoodies",
        "Jackets",
        "Caps",
        "Bags",
        "Tumblers",
        "Accessories",
        "School Supplies"
    ];

    public static readonly string[] StatusFilters =
    [
        "All",
        "Active",
        "Inactive",
        "Draft",
        "Low Stock",
        "Out of Stock"
    ];

    private static readonly Dictionary<string, string> CategorySkuPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["T-Shirts"] = "NUBE-TS",
        ["Polo Shirts"] = "NUBE-PL",
        ["Hoodies"] = "NUBE-HD",
        ["Hoodie"] = "NUBE-HD",
        ["Jackets"] = "NUBE-JK",
        ["Caps"] = "NUBE-CP",
        ["Bags"] = "NUBE-BG",
        ["Tumblers"] = "NUBE-TB",
        ["Accessories"] = "NUBE-AC",
        ["School Supplies"] = "NUBE-SS"
    };

    private readonly IAppDatabase _db;
    private readonly ProductCatalogService _catalog;
    private readonly List<AdminProduct> _products = [];
    private readonly Dictionary<int, Product> _hiddenStorefront = new();
    private int _nextId = 1;
    private bool _loaded;

    public event Action? OnChange;

    public AdminProductService(IAppDatabase db, ProductCatalogService catalog)
    {
        _db = db;
        _catalog = catalog;
    }

    public IReadOnlyList<AdminProduct> All => _products;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        var products = await _db.GetProductsAsync();
        _products.Clear();
        _products.AddRange(products.Select(AdminProduct.FromProduct));
        var variantRows = await _db.GetProductVariantsByProductIdsAsync(_products.Select(p => p.Id));
        foreach (var group in variantRows.GroupBy(v => v.ProductId))
        {
            var product = _products.FirstOrDefault(p => p.Id == group.Key);
            if (product is null) continue;
            product.Variants = group.Select(v => v.Clone()).ToList();
            if (product.HasSizeVariants)
            {
                product.Stock = product.Variants.Sum(v => Math.Max(0, v.StockQuantity));
                product.Sizes = product.Variants
                    .Where(v => v.IsActive)
                    .Select(v => v.Size)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        _nextId = _products.Count == 0 ? 1 : _products.Max(p => p.Id) + 1;
        await _catalog.ReloadAsync();
        _loaded = true;
        OnChange?.Invoke();
    }

    public AdminProduct? GetById(int id) =>
        _products.FirstOrDefault(p => p.Id == id);

    public bool SkuExists(string sku, int? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(sku)) return false;
        var normalized = sku.Trim();
        return _products.Any(p =>
            (excludeId is null || p.Id != excludeId) &&
            p.Sku.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public string SuggestSku(string category)
    {
        var prefix = CategorySkuPrefixes.TryGetValue(category ?? string.Empty, out var mapped)
            ? mapped
            : "NUBE-XX";

        var used = _products.Select(p => p.Sku)
            .Where(s => s.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
            .Select(s =>
            {
                var tail = s[(prefix.Length + 1)..];
                return int.TryParse(tail, out var n) ? n : 0;
            })
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}-{(used + 1):000}";
    }

    public IEnumerable<AdminProduct> Filter(string? search, string category, string status)
    {
        IEnumerable<AdminProduct> query = _products;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Sku.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Category.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(category) &&
            !category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = status switch
            {
                "Active" => query.Where(p => p.IsActive),
                "Inactive" => query.Where(p => p.IsInactive),
                "Draft" => query.Where(p => p.IsDraft),
                "Low Stock" => query.Where(p => p.Stock > 0 && p.Stock <= AdminProduct.LowStockThreshold),
                "Out of Stock" => query.Where(p => p.Stock <= 0),
                _ => query
            };
        }

        return query.OrderBy(p => p.Id);
    }

    public async Task<AdminProduct> AddAsync(AdminProduct product)
    {
        product.Id = 0;
        product.Name = product.Name.Trim();
        product.Sku = product.Sku.Trim().ToUpperInvariant();
        product.Category = product.Category.Trim();
        product.Description = product.Description?.Trim() ?? string.Empty;
        product.Status = string.IsNullOrWhiteSpace(product.Status) ? "Active" : product.Status.Trim();
        product.Sold = Math.Max(0, product.Sold);
        product.Stock = Math.Max(0, product.Stock);
        product.CreatedAt = product.CreatedAt == default ? DateTime.Now : product.CreatedAt;
        product.Colors ??= [];
        product.Sizes ??= [];
        product.Images ??= [];

        if (product.Images.Count == 0 && !string.IsNullOrWhiteSpace(product.ImageUrl))
            product.Images = [product.ImageUrl.Trim()];

        if (product.Images.Count > 0)
            product.ImageUrl = product.Images[0];
        else if (string.IsNullOrWhiteSpace(product.ImageUrl))
            product.ImageUrl = CatalogHelpers.PlaceholderImage;

        NormalizeVariantState(product);

        var stored = await PersistAsync(product);
        product.Id = stored.Id;
        if (product.HasSizeVariants)
        {
            try
            {
                await _db.ReplaceProductVariantsAsync(product.Id, product.Variants);
                product.Variants = await _db.GetProductVariantsAsync(product.Id);
                NormalizeVariantState(product);
            }
            catch
            {
                // Product row is already saved; variants can be edited later.
            }
        }

        _products.Add(product);
        SyncStorefront(product);
        OnChange?.Invoke();
        return product;
    }

    public async Task<bool> UpdateAsync(AdminProduct product)
    {
        var existing = GetById(product.Id);
        if (existing is null) return false;

        existing.Name = product.Name.Trim();
        existing.Sku = product.Sku.Trim();
        existing.Category = product.Category.Trim();
        existing.Price = product.Price;
        existing.Stock = product.Stock;
        existing.Sold = product.Sold;
        existing.Status = product.Status;
        existing.Images = product.Images?.Count > 0
            ? [.. product.Images]
            : string.IsNullOrWhiteSpace(product.ImageUrl)
                ? []
                : [product.ImageUrl.Trim()];
        existing.ImageUrl = existing.Images.Count > 0
            ? existing.Images[0]
            : string.IsNullOrWhiteSpace(product.ImageUrl)
                ? CatalogHelpers.PlaceholderImage
                : product.ImageUrl.Trim();
        existing.Description = product.Description?.Trim() ?? string.Empty;
        existing.Colors = [.. product.Colors];
        existing.Sizes = [.. product.Sizes];
        existing.Variants = product.Variants?.Select(v => v.Clone()).ToList() ?? [];
        NormalizeVariantState(existing);
        await PersistAsync(existing);
        await _db.ReplaceProductVariantsAsync(existing.Id, existing.Variants);
        existing.Variants = await _db.GetProductVariantsAsync(existing.Id);
        NormalizeVariantState(existing);
        SyncStorefront(existing);
        OnChange?.Invoke();
        return true;
    }

    public async Task<AdminProduct?> DuplicateAsync(int id)
    {
        var source = GetById(id);
        if (source is null) return null;

        var copy = source.Clone();
        copy.Id = 0;
        copy.Name = $"{source.Name} Copy";
        copy.Sku = $"{source.Sku}-COPY";
        copy.Sold = 0;
        copy.CreatedAt = DateTime.Now;
        foreach (var variant in copy.Variants)
        {
            variant.Id = 0;
            variant.ProductId = 0;
        }

        return await AddAsync(copy);
    }

    public async Task<bool> SetStatusAsync(int id, string status)
    {
        var product = GetById(id);
        if (product is null) return false;
        product.Status = status;
        await PersistAsync(product);
        SyncStorefront(product);
        OnChange?.Invoke();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var removed = _products.RemoveAll(p => p.Id == id) > 0;
        if (removed)
        {
            await _db.DeleteProductAsync(id);
            RemoveFromStorefront(id);
            OnChange?.Invoke();
        }

        return removed;
    }

    public async Task<int> DeleteManyAsync(IEnumerable<int> ids)
    {
        var set = ids.ToHashSet();
        var removedIds = _products.Where(p => set.Contains(p.Id)).Select(p => p.Id).ToList();
        var removed = _products.RemoveAll(p => set.Contains(p.Id));
        if (removed > 0)
        {
            foreach (var id in removedIds)
            {
                await _db.DeleteProductAsync(id);
                RemoveFromStorefront(id);
            }
            OnChange?.Invoke();
        }

        return removed;
    }

    public async Task SetStatusManyAsync(IEnumerable<int> ids, string status)
    {
        var set = ids.ToHashSet();
        var changed = false;
        foreach (var product in _products.Where(p => set.Contains(p.Id)))
        {
            product.Status = status;
            await PersistAsync(product);
            SyncStorefront(product);
            changed = true;
        }

        if (changed) OnChange?.Invoke();
    }

    public async Task<bool> SetStockAsync(int id, int stock)
    {
        var product = GetById(id);
        if (product is null) return false;
        product.Stock = Math.Max(0, stock);
        await PersistAsync(product);

        var storeProduct = _catalog.GetById(id);
        if (storeProduct is not null)
        {
            storeProduct.Stock = product.Stock;
            storeProduct.InStock = product.Stock > 0 && product.IsActive;
            _catalog.Upsert(storeProduct);
        }

        OnChange?.Invoke();
        return true;
    }

    public void NotifyChanged() => OnChange?.Invoke();

    private async Task<Product> PersistAsync(AdminProduct product)
    {
        var store = ToStoreProduct(product);
        if (!product.IsActive)
            store.InStock = false;

        var saved = await _db.UpsertProductAsync(store);
        product.Id = saved.Id;
        return saved;
    }

    private void SyncStorefront(AdminProduct product)
    {
        if (!product.IsActive)
        {
            HideFromStorefront(product.Id);
            return;
        }

        var existing = _catalog.GetById(product.Id);
        if (existing is null && _hiddenStorefront.TryGetValue(product.Id, out var restored))
        {
            existing = restored;
            _catalog.Upsert(restored);
            _hiddenStorefront.Remove(product.Id);
        }

        if (existing is null)
        {
            _catalog.Upsert(ToStoreProduct(product));
            return;
        }

        ApplyAdminFields(existing, product);
        _catalog.Upsert(existing);
        _ = _db.UpsertProductAsync(existing);
    }

    private void HideFromStorefront(int id)
    {
        var existing = _catalog.GetById(id);
        if (existing is null)
        {
            _hiddenStorefront.Remove(id);
            return;
        }

        _hiddenStorefront[id] = existing;
        _catalog.Remove(id);
    }

    private void RemoveFromStorefront(int id)
    {
        HideFromStorefront(id);
        _hiddenStorefront.Remove(id);
    }

    private static void ApplyAdminFields(Product target, AdminProduct product)
    {
        target.Name = product.Name;
        target.Category = product.Category;
        target.Sku = product.Sku;
        target.Price = product.Price;
        target.Stock = product.Stock;
        target.Sold = product.Sold;
        target.InStock = product.Stock > 0;
        target.Status = product.Status;
        target.IsPublished = product.IsActive;
        if (product.IsActive)
            target.PublishedAt ??= DateTime.UtcNow;
        target.UpdatedAt = DateTime.UtcNow;
        target.Description = string.IsNullOrWhiteSpace(product.Description)
            ? target.Description
            : product.Description;
        target.FullDescription = string.IsNullOrWhiteSpace(product.Description)
            ? target.FullDescription
            : product.Description;
        target.ImageUrl = product.ImageUrl;
        target.Images = product.Images.Count > 0 ? [.. product.Images] : [product.ImageUrl];
        if (product.Colors.Count > 0) target.Colors = [.. product.Colors];
        if (product.Sizes.Count > 0) target.Sizes = [.. product.Sizes];
        target.Variants = product.Variants.Select(v => v.Clone()).ToList();
        target.Section = ResolveSection(product.Category);
    }

    private static void NormalizeVariantState(AdminProduct product)
    {
        product.Variants ??= [];
        product.Variants = product.Variants
            .Where(v => !string.IsNullOrWhiteSpace(v.Size))
            .Select(v =>
            {
                v.Size = v.Size.Trim();
                v.Sku = string.IsNullOrWhiteSpace(v.Sku) ? null : v.Sku.Trim().ToUpperInvariant();
                v.StockQuantity = Math.Max(0, v.StockQuantity);
                v.Status = string.IsNullOrWhiteSpace(v.Status) ? "Active" : v.Status.Trim();
                v.PriceAdjustment = 0;
                return v;
            })
            .ToList();

        if (product.HasSizeVariants)
        {
            product.Stock = product.Variants.Sum(v => v.StockQuantity);
            product.Sizes = product.Variants
                .Where(v => v.IsActive)
                .Select(v => v.Size)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    private static Product ToStoreProduct(AdminProduct product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Category = product.Category,
        Sku = product.Sku,
        Price = product.Price,
        Stock = product.Stock,
        Sold = product.Sold,
        InStock = product.Stock > 0 && product.IsActive,
        Status = product.Status,
        IsPublished = product.IsActive,
        PublishedAt = product.IsActive ? DateTime.UtcNow : null,
        UpdatedAt = DateTime.UtcNow,
        Description = product.Description,
        FullDescription = product.Description,
        ImageUrl = product.ImageUrl,
        Images = product.Images.Count > 0 ? [.. product.Images] : [product.ImageUrl],
        Colors = product.Colors.Count > 0 ? [.. product.Colors] : ["navy"],
        Sizes = product.HasSizeVariants
            ? product.Variants.Where(v => v.IsActive).Select(v => v.Size).ToList()
            : product.Sizes.Count > 0 ? [.. product.Sizes] : ["One Size"],
        Variants = product.Variants.Select(v => v.Clone()).ToList(),
        Rating = 0,
        Reviews = 0,
        Section = ResolveSection(product.Category),
        IsNewArrival = true
    };

    private static string ResolveSection(string category) => category switch
    {
        "Accessories" or "Caps" or "Bags" or "Tumblers" or "School Supplies" => "accessories",
        _ => "apparel"
    };
}
