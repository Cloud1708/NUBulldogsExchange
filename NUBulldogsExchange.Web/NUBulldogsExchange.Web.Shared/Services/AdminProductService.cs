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
        ["Jackets"] = "NUBE-JK",
        ["Caps"] = "NUBE-CP",
        ["Bags"] = "NUBE-BG",
        ["Tumblers"] = "NUBE-TB",
        ["Accessories"] = "NUBE-AC",
        ["School Supplies"] = "NUBE-SS"
    };

    private readonly List<AdminProduct> _products;
    private readonly Dictionary<int, Product> _hiddenStorefront = new();
    private int _nextId;

    public event Action? OnChange;

    public AdminProductService()
    {
        _products = MockData.Products
            .Take(10)
            .Select(AdminProduct.FromProduct)
            .Select(NormalizeForAdminCatalog)
            .ToList();

        // Match screenshot: Varsity Jacket as low stock example.
        var jacket = _products.FirstOrDefault(p => p.Category == "Jackets");
        if (jacket is not null && jacket.Stock > AdminProduct.LowStockThreshold)
            jacket.Stock = 18;

        var penish = _products.FirstOrDefault(p => p.Stock == 0);
        if (penish is not null)
            penish.Status = "Active";

        var maxAdmin = _products.Count == 0 ? 0 : _products.Max(p => p.Id);
        var maxStore = MockData.Products.Count == 0 ? 0 : MockData.Products.Max(p => p.Id);
        _nextId = Math.Max(maxAdmin, maxStore) + 1;
    }

    public IReadOnlyList<AdminProduct> All => _products;

    public AdminProduct? GetById(int id) =>
        _products.FirstOrDefault(p => p.Id == id);

    public bool SkuExists(string sku, int? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(sku)) return false;
        var normalized = sku.Trim();

        if (_products.Any(p =>
                (excludeId is null || p.Id != excludeId) &&
                p.Sku.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            return true;

        return MockData.Products.Any(p =>
            (excludeId is null || p.Id != excludeId) &&
            p.Sku.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public string SuggestSku(string category)
    {
        var prefix = CategorySkuPrefixes.TryGetValue(category ?? string.Empty, out var mapped)
            ? mapped
            : "NUBE-XX";

        var used = _products.Select(p => p.Sku)
            .Concat(MockData.Products.Select(p => p.Sku))
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

    public AdminProduct Add(AdminProduct product)
    {
        product.Id = _nextId++;
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
            product.ImageUrl = MockData.PlaceholderImage;

        _products.Add(product);
        SyncStorefront(product);
        OnChange?.Invoke();
        return product;
    }

    public bool Update(AdminProduct product)
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
                ? MockData.PlaceholderImage
                : product.ImageUrl.Trim();
        existing.Description = product.Description?.Trim() ?? string.Empty;
        existing.Colors = [.. product.Colors];
        existing.Sizes = [.. product.Sizes];
        SyncStorefront(existing);
        OnChange?.Invoke();
        return true;
    }

    public AdminProduct? Duplicate(int id)
    {
        var source = GetById(id);
        if (source is null) return null;

        var copy = source.Clone();
        copy.Id = _nextId++;
        copy.Name = $"{source.Name} Copy";
        copy.Sku = $"{source.Sku}-COPY";
        copy.Sold = 0;
        copy.CreatedAt = DateTime.Now;
        _products.Add(copy);
        SyncStorefront(copy);
        OnChange?.Invoke();
        return copy;
    }

    public bool SetStatus(int id, string status)
    {
        var product = GetById(id);
        if (product is null) return false;
        product.Status = status;
        SyncStorefront(product);
        OnChange?.Invoke();
        return true;
    }

    public bool Delete(int id)
    {
        var removed = _products.RemoveAll(p => p.Id == id) > 0;
        if (removed)
        {
            RemoveFromStorefront(id);
            OnChange?.Invoke();
        }

        return removed;
    }

    public int DeleteMany(IEnumerable<int> ids)
    {
        var set = ids.ToHashSet();
        var removedIds = _products.Where(p => set.Contains(p.Id)).Select(p => p.Id).ToList();
        var removed = _products.RemoveAll(p => set.Contains(p.Id));
        if (removed > 0)
        {
            foreach (var id in removedIds)
                RemoveFromStorefront(id);
            OnChange?.Invoke();
        }

        return removed;
    }

    public void SetStatusMany(IEnumerable<int> ids, string status)
    {
        var set = ids.ToHashSet();
        var changed = false;
        foreach (var product in _products.Where(p => set.Contains(p.Id)))
        {
            product.Status = status;
            SyncStorefront(product);
            changed = true;
        }

        if (changed) OnChange?.Invoke();
    }

    public bool SetStock(int id, int stock)
    {
        var product = GetById(id);
        if (product is null) return false;
        product.Stock = Math.Max(0, stock);

        // Keep storefront mock catalog in sync for the demo.
        var storeProduct = MockData.GetById(id);
        if (storeProduct is not null)
        {
            storeProduct.Stock = product.Stock;
            storeProduct.InStock = product.Stock > 0 && product.IsActive;
        }

        OnChange?.Invoke();
        return true;
    }

    public void NotifyChanged() => OnChange?.Invoke();

    private void SyncStorefront(AdminProduct product)
    {
        // Only Active products appear on the customer storefront.
        if (!product.IsActive)
        {
            HideFromStorefront(product.Id);
            return;
        }

        var existing = MockData.GetById(product.Id);
        if (existing is null && _hiddenStorefront.TryGetValue(product.Id, out var restored))
        {
            existing = restored;
            MockData.Products.Add(restored);
            _hiddenStorefront.Remove(product.Id);
        }

        if (existing is null)
        {
            MockData.Products.Add(ToStoreProduct(product));
            return;
        }

        ApplyAdminFields(existing, product);
    }

    private void HideFromStorefront(int id)
    {
        var existing = MockData.GetById(id);
        if (existing is null)
        {
            _hiddenStorefront.Remove(id);
            return;
        }

        _hiddenStorefront[id] = existing;
        MockData.Products.RemoveAll(p => p.Id == id);
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
        target.Section = ResolveSection(product.Category);
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
        InStock = product.Stock > 0,
        Description = product.Description,
        FullDescription = product.Description,
        ImageUrl = product.ImageUrl,
        Images = product.Images.Count > 0 ? [.. product.Images] : [product.ImageUrl],
        Colors = product.Colors.Count > 0 ? [.. product.Colors] : ["navy"],
        Sizes = product.Sizes.Count > 0 ? [.. product.Sizes] : ["One Size"],
        Rating = 5,
        Reviews = 0,
        Section = ResolveSection(product.Category),
        IsNewArrival = true
    };

    private static string ResolveSection(string category) => category switch
    {
        "Accessories" or "Caps" or "Bags" or "Tumblers" => "accessories",
        "School Supplies" => "essentials",
        _ => "apparel"
    };

    private static AdminProduct NormalizeForAdminCatalog(AdminProduct product)
    {
        // Keep catalog Active by default for admin demo; stock badges remain separate.
        if (string.IsNullOrWhiteSpace(product.Status))
            product.Status = "Active";
        return product;
    }
}
