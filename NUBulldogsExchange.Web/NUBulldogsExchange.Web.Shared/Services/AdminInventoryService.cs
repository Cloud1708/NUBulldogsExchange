using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminInventoryService
{
    public static readonly string[] AdjustmentTypes = ["Add Stock", "Remove Stock", "Set Stock"];

    public static readonly string[] AdjustmentReasons =
    [
        "Restock",
        "Damaged Item",
        "Inventory Correction",
        "Returned Item",
        "Manual Adjustment",
        "Other"
    ];

    private readonly IAppDatabase _db;
    private readonly AdminProductService _products;
    private readonly AdminSettingsService _settings;
    private readonly Dictionary<int, int> _lowStockLevels = new();
    private readonly List<InventoryHistoryEntry> _history = [];
    private bool _loaded;

    public event Action? OnChange;

    public AdminInventoryService(IAppDatabase db, AdminProductService products, AdminSettingsService settings)
    {
        _db = db;
        _products = products;
        _settings = settings;
        _products.OnChange += () => OnChange?.Invoke();
        _settings.OnChange += () => OnChange?.Invoke();
    }

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await _products.EnsureLoadedAsync();
        _history.Clear();
        foreach (var entry in await _db.GetInventoryHistoryAsync())
        {
            entry.HydrateMetaFromNotes();
            _history.Add(entry);
        }

        _lowStockLevels.Clear();
        foreach (var kv in await _db.GetLowStockLevelsAsync())
            _lowStockLevels[kv.Key] = kv.Value;
        _loaded = true;
        OnChange?.Invoke();
    }

    public int TotalInventory => Rows.Sum(r => Math.Max(0, r.TotalStock));
    public int InStockCount => Rows.Count(r => r.StockStateKey == "in");
    public int LowStockCount => Rows.Count(r => r.StockStateKey is "low" or "attention");
    public int OutOfStockCount => Rows.Count(r => r.StockStateKey == "out");

    public IReadOnlyList<InventoryHistoryEntry> History =>
        _history.OrderByDescending(h => h.Date).ToList();

    public IEnumerable<InventoryRow> Rows =>
        _products.All.OrderBy(p => p.Id).Select(ToRow);

    public InventoryRow? GetRow(int productId)
    {
        var product = _products.GetById(productId);
        return product is null ? null : ToRow(product);
    }

    public IEnumerable<InventoryHistoryEntry> HistoryForProduct(int productId) =>
        _history.Where(h => h.ProductId == productId).OrderByDescending(h => h.Date);

    public async Task<(bool Success, string Message)> AdjustAsync(
        int productId,
        string type,
        int quantity,
        string reason,
        string? notes,
        string adminName,
        int? variantId = null)
    {
        var product = _products.GetById(productId);
        if (product is null)
            return (false, "Product not found.");

        if (string.IsNullOrWhiteSpace(type))
            return (false, "Select an adjustment type.");

        if (string.IsNullOrWhiteSpace(reason))
            return (false, "Select a reason.");

        if (type is "Add Stock" or "Remove Stock")
        {
            if (quantity <= 0)
                return (false, "Quantity must be greater than 0.");
        }

        if (type == "Set Stock" && quantity < 0)
            return (false, "Stock cannot be lower than 0.");

        ProductVariant? variant = null;
        string? variantLabel = null;
        if (product.HasVariants)
        {
            if (variantId is null or <= 0)
                return (false, "Select a variant to adjust.");

            variant = product.Variants.FirstOrDefault(v => v.Id == variantId.Value);
            if (variant is null)
                return (false, "Selected variant was not found.");

            variantLabel = InventoryRow.FormatVariantLabel(variant);
        }

        var previous = variant?.StockQuantity ?? product.Stock;
        var next = type switch
        {
            "Add Stock" => previous + quantity,
            "Remove Stock" => previous - quantity,
            "Set Stock" => quantity,
            _ => previous
        };

        if (next < 0)
            return (false, "Stock cannot be lower than 0.");

        if (variant is not null)
        {
            var ok = await _products.SetVariantStockAsync(productId, variant.Id, next);
            if (!ok)
                return (false, "Unable to update variant stock.");
        }
        else
        {
            await _products.SetStockAsync(productId, next);
        }

        var entry = new InventoryHistoryEntry
        {
            ProductId = productId,
            ProductName = product.Name,
            Type = type,
            Quantity = type == "Set Stock" ? Math.Abs(next - previous) : quantity,
            PreviousStock = previous,
            NewStock = next,
            Reason = reason.Trim(),
            VariantLabel = variantLabel,
            Notes = InventoryHistoryEntry.BuildNotes(variantLabel, null, notes),
            Date = DateTime.Now,
            AdminName = string.IsNullOrWhiteSpace(adminName) ? "Admin" : adminName
        };

        await _db.AddInventoryHistoryAsync(entry);
        entry.HydrateMetaFromNotes();
        _history.Insert(0, entry);

        OnChange?.Invoke();
        return (true, "Inventory updated successfully.");
    }

    private InventoryRow ToRow(AdminProduct product)
    {
        var variants = product.Variants
            .Where(v => v.IsActive)
            .Select(v => v.Clone())
            .OrderBy(v => v.ColorName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => InventoryRow.SizeSortRank(v.Size))
            .ThenBy(v => v.Size, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = variants.Count > 0
            ? variants.Sum(v => Math.Max(0, v.StockQuantity))
            : Math.Max(0, product.Stock);

        return new InventoryRow
        {
            ProductId = product.Id,
            Name = product.Name,
            Sku = product.Sku,
            Category = product.Category,
            ImageUrl = product.ImageUrl,
            TotalStock = total,
            LowStockLevel = _lowStockLevels.GetValueOrDefault(product.Id, _settings.LowStockThreshold),
            Variants = variants
        };
    }
}
