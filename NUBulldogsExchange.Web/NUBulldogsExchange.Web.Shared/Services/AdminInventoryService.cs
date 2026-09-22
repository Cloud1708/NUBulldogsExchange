using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminInventoryService
{
    public static readonly string[] AdjustmentTypes = ["Add Stock", "Remove Stock", "Set Stock"];

    public static readonly string[] AdjustmentReasons =
    [
        "Restock",
        "Inventory Correction",
        "Damaged Items",
        "Returned Items",
        "Lost Items",
        "Manual Adjustment",
        "Other"
    ];

    private readonly IAppDatabase _db;
    private readonly AdminProductService _products;
    private readonly AdminSettingsService _settings;
    private readonly Dictionary<int, int> _reserved = new();
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
        _history.AddRange(await _db.GetInventoryHistoryAsync());
        _reserved.Clear();
        foreach (var kv in await _db.GetReservedStockAsync())
            _reserved[kv.Key] = kv.Value;
        _lowStockLevels.Clear();
        foreach (var kv in await _db.GetLowStockLevelsAsync())
            _lowStockLevels[kv.Key] = kv.Value;
        _loaded = true;
        OnChange?.Invoke();
    }

    public int TotalInventory => _products.All.Sum(p => Math.Max(0, p.Stock));
    public int InStockCount => Rows.Count(r => r.StockStateKey == "in");
    public int LowStockCount => Rows.Count(r => r.StockStateKey == "low");
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
        string adminName)
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

        var previous = product.Stock;
        var next = type switch
        {
            "Add Stock" => previous + quantity,
            "Remove Stock" => previous - quantity,
            "Set Stock" => quantity,
            _ => previous
        };

        if (next < 0)
            return (false, "Stock cannot be lower than 0.");

        await _products.SetStockAsync(productId, next);

        var entry = new InventoryHistoryEntry
        {
            ProductId = productId,
            ProductName = product.Name,
            Type = type,
            Quantity = type == "Set Stock" ? Math.Abs(next - previous) : quantity,
            PreviousStock = previous,
            NewStock = next,
            Reason = reason.Trim(),
            Notes = notes?.Trim() ?? string.Empty,
            Date = DateTime.Now,
            AdminName = string.IsNullOrWhiteSpace(adminName) ? "Admin" : adminName
        };

        await _db.AddInventoryHistoryAsync(entry);
        _history.Insert(0, entry);

        OnChange?.Invoke();
        return (true, "Inventory updated successfully.");
    }

    private InventoryRow ToRow(AdminProduct product) => new()
    {
        ProductId = product.Id,
        Name = product.Name,
        Sku = product.Sku,
        Category = product.Category,
        ImageUrl = product.ImageUrl,
        Available = product.Stock,
        Reserved = _reserved.GetValueOrDefault(product.Id),
        LowStockLevel = _lowStockLevels.GetValueOrDefault(product.Id, _settings.LowStockThreshold)
    };
}
