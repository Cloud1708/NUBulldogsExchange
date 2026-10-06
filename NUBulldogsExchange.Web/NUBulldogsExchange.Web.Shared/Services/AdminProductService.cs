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
            if (product.HasVariants)
            {
                product.Stock = product.Variants.Sum(v => Math.Max(0, v.StockQuantity));
                product.Sizes = product.Variants
                    .Where(v => v.IsActive)
                    .Select(v => v.Size)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                product.Colors = product.Variants
                    .Where(v => v.IsActive && !string.IsNullOrWhiteSpace(v.ColorName))
                    .Select(v => v.ColorName!.Trim())
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

    public void ApplyPurchase(IEnumerable<AdminOrderItem> items) =>
        ApplyOrderStock(items, restore: false);

    public void ApplyCancellation(IEnumerable<AdminOrderItem> items) =>
        ApplyOrderStock(items, restore: true);

    private void ApplyOrderStock(IEnumerable<AdminOrderItem> items, bool restore)
    {
        if (!_loaded) return;

        foreach (var item in items)
        {
            var product = GetById(item.ProductId);
            if (product is null) continue;
            ProductVariantLogic.ApplyOrderItemStock(product, item, restore);
        }

        OnChange?.Invoke();
    }

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
        try
        {
            await _db.ReplaceProductVariantsAsync(product.Id, product.HasVariants ? product.Variants : []);
            if (product.HasVariants)
            {
                product.Variants = await _db.GetProductVariantsAsync(product.Id);
                NormalizeVariantState(product);
            }
        }
        catch
        {
            // Product row is already saved; variants can be edited later.
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
        // Price changes go through AdjustPriceAsync — preserve current pricing on edit.
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
        existing.SizeGuideId = string.IsNullOrWhiteSpace(product.SizeGuideId) ? null : product.SizeGuideId.Trim();
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
        if (GetById(id) is null || !await _db.DeleteProductAsync(id))
            return false;

        _products.RemoveAll(p => p.Id == id);
        RemoveFromStorefront(id);
        OnChange?.Invoke();
        return true;
    }

    /// <summary>Deletes products one by one; returns the ids the database actually removed.</summary>
    public async Task<List<int>> DeleteManyAsync(IEnumerable<int> ids)
    {
        var set = ids.ToHashSet();
        var deleted = new List<int>();
        foreach (var id in _products.Where(p => set.Contains(p.Id)).Select(p => p.Id).ToList())
        {
            if (!await _db.DeleteProductAsync(id))
                continue;
            _products.RemoveAll(p => p.Id == id);
            RemoveFromStorefront(id);
            deleted.Add(id);
        }

        if (deleted.Count > 0)
            OnChange?.Invoke();
        return deleted;
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
        if (product.HasVariants)
            return false;

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

    public async Task<bool> SetVariantStockAsync(int productId, int variantId, int stock)
    {
        var product = GetById(productId);
        if (product is null) return false;

        var variant = product.Variants.FirstOrDefault(v => v.Id == variantId);
        if (variant is null) return false;

        var next = Math.Max(0, stock);
        await _db.UpdateProductVariantStockAsync(productId, variantId, next);

        variant.StockQuantity = next;
        product.Stock = product.Variants.Sum(v => Math.Max(0, v.StockQuantity));
        await PersistAsync(product);

        try
        {
            product.Variants = await _db.GetProductVariantsAsync(productId);
            if (product.HasVariants)
                product.Stock = product.Variants.Sum(v => Math.Max(0, v.StockQuantity));
        }
        catch
        {
            // Keep in-memory values if reload fails.
        }

        SyncStorefront(product);
        OnChange?.Invoke();
        return true;
    }

    public static readonly string[] PriceAdjustmentReasons =
    [
        "Price Update",
        "Supplier Cost Change",
        "Seasonal Adjustment",
        "Promo Adjustment",
        "Inventory Clearance",
        "Correction",
        "Other"
    ];

    /// <summary>
    /// Updates product and/or variant pricing.
    /// Same-price products: updates products.price and clears price_adjustment.
    /// Different-price / specific variant: sets that variant's price_adjustment
    /// so effective price = products.price + price_adjustment.
    /// </summary>
    public async Task<(bool Success, string Message)> AdjustPriceAsync(
        int productId,
        decimal newPrice,
        decimal? promoPrice,
        DateTime effectiveDate,
        string reason,
        string? notes,
        string adminName,
        int? variantId = null,
        bool applyToAllVariants = true)
    {
        var product = GetById(productId);
        if (product is null)
            return (false, "Product not found.");

        if (newPrice <= 0)
            return (false, "New price must be greater than 0.");

        if (promoPrice is decimal promo)
        {
            if (promo <= 0)
                return (false, "Promo price must be greater than 0.");
            if (promo >= newPrice)
                return (false, "Promo price must be less than the new price.");
        }

        if (string.IsNullOrWhiteSpace(reason))
            return (false, "Select a reason.");

        if (effectiveDate.Date > DateTime.Today)
            return (false, "Scheduled future pricing is not supported yet. Use today's date to apply immediately.");

        if (effectiveDate.Date < DateTime.Today.AddDays(-1))
            return (false, "Effective date cannot be more than one day in the past.");

        ProductVariant? targetVariant = null;
        if (product.HasVariants && !applyToAllVariants)
        {
            if (variantId is not int vid || vid <= 0)
                return (false, "Select a variant.");

            targetVariant = product.Variants.FirstOrDefault(v => v.Id == vid);
            if (targetVariant is null)
                return (false, "Selected variant was not found.");
        }

        var salePrice = promoPrice is decimal p
            ? decimal.Round(p, 2, MidpointRounding.AwayFromZero)
            : decimal.Round(newPrice, 2, MidpointRounding.AwayFromZero);
        var listPrice = promoPrice is decimal
            ? decimal.Round(newPrice, 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;

        var previousPrice = targetVariant is not null
            ? ProductVariantLogic.ResolvePrice(product.Price, targetVariant)
            : product.Price;
        var previousOriginal = product.OriginalPrice;
        var previousAdjustments = product.Variants
            .Select(v => (v.Id, v.PriceAdjustment))
            .ToDictionary(x => x.Id, x => x.PriceAdjustment);

        try
        {
            if (targetVariant is not null)
            {
                // Specific variant: keep products.price as base; store delta on the variant.
                ProductVariantLogic.SetAbsolutePrice(targetVariant, product.Price, salePrice);
                if (listPrice is decimal compareAt)
                    product.OriginalPrice = compareAt;
                else if (promoPrice is null)
                    product.OriginalPrice = null;
            }
            else
            {
                // Product / all variants: shared products.price, clear deltas.
                product.Price = salePrice;
                product.OriginalPrice = listPrice;
                ProductVariantLogic.ApplySamePriceMode(product.Variants);
            }

            await PersistAsync(product);
            if (product.HasVariants)
                await _db.ReplaceProductVariantsAsync(product.Id, product.Variants);
            SyncStorefront(product);

            var entry = new ProductPriceHistoryEntry
            {
                ProductId = product.Id,
                VariantId = targetVariant?.Id,
                PreviousPrice = previousPrice,
                NewPrice = decimal.Round(newPrice, 2, MidpointRounding.AwayFromZero),
                PromoPrice = promoPrice is decimal pp
                    ? decimal.Round(pp, 2, MidpointRounding.AwayFromZero)
                    : null,
                Reason = reason.Trim(),
                Notes = notes?.Trim() ?? string.Empty,
                UpdatedBy = string.IsNullOrWhiteSpace(adminName) ? "Admin" : adminName.Trim(),
                CreatedAt = DateTime.UtcNow,
                VariantLabel = targetVariant is not null
                    ? InventoryRow.FormatVariantLabel(targetVariant)
                    : product.HasVariants ? "All Variants" : "—"
            };

            if (previousOriginal is decimal oldOrig)
                entry.Notes = string.IsNullOrWhiteSpace(entry.Notes)
                    ? $"Previous compare-at: ₱{oldOrig:N2}"
                    : $"{entry.Notes}\nPrevious compare-at: ₱{oldOrig:N2}";

            try
            {
                await _db.AddProductPriceHistoryAsync(entry);
            }
            catch
            {
                // Price already saved. History requires docs/sql/019_product_price_history.sql.
            }

            OnChange?.Invoke();
            return (true, "Price updated successfully.");
        }
        catch (Exception ex)
        {
            product.Price = targetVariant is null
                ? previousPrice
                : product.Price;
            product.OriginalPrice = previousOriginal;
            foreach (var variant in product.Variants)
            {
                if (previousAdjustments.TryGetValue(variant.Id, out var adj))
                    variant.PriceAdjustment = adj;
            }

            return (false, string.IsNullOrWhiteSpace(ex.Message)
                ? "Unable to update price."
                : ex.Message);
        }
    }

    public Task<List<ProductPriceHistoryEntry>> GetPriceHistoryAsync(int productId) =>
        _db.GetProductPriceHistoryAsync(productId);

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
        target.OriginalPrice = product.OriginalPrice;
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
        target.Colors = [.. product.Colors];
        target.Sizes = [.. product.Sizes];
        target.Variants = product.Variants.Select(v => v.Clone()).ToList();
        target.SizeGuideId = string.IsNullOrWhiteSpace(product.SizeGuideId) ? null : product.SizeGuideId.Trim();
        target.Section = ResolveSection(product.Category);
    }

    private static void NormalizeVariantState(AdminProduct product)
    {
        product.Variants ??= [];
        product.Variants = product.Variants
            .Where(v => !string.IsNullOrWhiteSpace(v.Size) || !string.IsNullOrWhiteSpace(v.ColorName))
            .Select(v =>
            {
                v.Size = v.Size?.Trim() ?? string.Empty;
                v.ColorName = string.IsNullOrWhiteSpace(v.ColorName) ? null : v.ColorName.Trim();
                v.ColorHex = ProductVariantLogic.NormalizeHex(v.ColorHex)
                             ?? (v.ColorName is null ? null : ProductVariantLogic.DefaultHex(v.ColorName));
                v.Sku = string.IsNullOrWhiteSpace(v.Sku) ? null : v.Sku.Trim().ToUpperInvariant();
                v.StockQuantity = Math.Max(0, v.StockQuantity);
                v.Status = string.IsNullOrWhiteSpace(v.Status) ? "Active" : v.Status.Trim();
                // Preserve price_adjustment so same-price (0) and different-price deltas both work.
                return v;
            })
            .ToList();

        if (product.HasVariants)
        {
            product.Stock = product.Variants.Sum(v => v.StockQuantity);
            product.Sizes = product.Variants
                .Where(v => v.IsActive && !string.IsNullOrWhiteSpace(v.Size))
                .Select(v => v.Size)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            product.Colors = product.Variants
                .Where(v => v.IsActive && !string.IsNullOrWhiteSpace(v.ColorName))
                .Select(v => v.ColorName!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (!product.HasSizeVariants)
                product.SizeGuideId = null;
        }
        else
        {
            product.SizeGuideId = null;
        }
    }

    private static Product ToStoreProduct(AdminProduct product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Category = product.Category,
        Sku = product.Sku,
        Price = product.Price,
        OriginalPrice = product.OriginalPrice,
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
        Colors = product.HasColorVariants
            ? product.Variants
                .Where(v => v.IsActive && !string.IsNullOrWhiteSpace(v.ColorName))
                .Select(v => v.ColorName!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [],
        Sizes = product.HasSizeVariants
            ? product.Variants.Where(v => v.IsActive && !string.IsNullOrWhiteSpace(v.Size)).Select(v => v.Size).ToList()
            : [],
        Variants = product.Variants.Select(v => v.Clone()).ToList(),
        SizeGuideId = string.IsNullOrWhiteSpace(product.SizeGuideId) ? null : product.SizeGuideId.Trim(),
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
