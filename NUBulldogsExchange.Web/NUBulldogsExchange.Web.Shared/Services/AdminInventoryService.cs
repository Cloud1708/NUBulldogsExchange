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

    private readonly AdminProductService _products;
    private readonly AdminSettingsService _settings;
    private readonly Dictionary<int, int> _reserved = new();
    private readonly Dictionary<int, int> _lowStockLevels = new();
    private readonly List<InventoryHistoryEntry> _history = [];

    public event Action? OnChange;

    public AdminInventoryService(AdminProductService products, AdminSettingsService settings)
    {
        _products = products;
        _settings = settings;
        SeedFromScreenshot();
        _products.OnChange += () => OnChange?.Invoke();
        _settings.OnChange += () => OnChange?.Invoke();
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

    public (bool Success, string Message) Adjust(
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

        _products.SetStock(productId, next);

        _history.Insert(0, new InventoryHistoryEntry
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
            AdminName = string.IsNullOrWhiteSpace(adminName) ? "Admin User" : adminName
        });

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

    private void SeedFromScreenshot()
    {
        // Align available stock + reserved with inventory screenshot totals (835 / 9 / 1 / 0).
        var seed = new (string Match, string? Rename, string Sku, int Available, int Reserved)[]
        {
            ("Lanyard", null, "NUBE-AC-001", 200, 6),
            ("Cap", null, "NUBE-CP-001", 120, 5),
            ("Classic Shirt", null, "NUBE-TS-001", 85, 8),
            ("Notebook", null, "NUBE-SS-001", 150, 6),
            ("Tumbler", null, "NUBE-TB-001", 55, 7),
            ("Hoodie", null, "NUBE-HD-001", 42, 8),
            ("Polo", null, "NUBE-PL-001", 63, 2),
            ("Tote", null, "NUBE-BG-001", 70, 1),
            ("Backpack", null, "NUBE-BG-002", 32, 0),
            ("Jacket", "NU Bulldogs Varsity Jacket", "NUBE-JK-001", 18, 5)
        };

        foreach (var (match, rename, sku, available, reserved) in seed)
        {
            var product = _products.All.FirstOrDefault(p =>
                match == "Cap"
                    ? p.Name.EndsWith(" Cap", StringComparison.OrdinalIgnoreCase) ||
                      p.Name.Equals("NU Bulldogs Cap", StringComparison.OrdinalIgnoreCase)
                    : p.Name.Contains(match, StringComparison.OrdinalIgnoreCase));
            if (product is null) continue;

            if (!string.IsNullOrWhiteSpace(rename))
                product.Name = rename;
            product.Sku = sku;
            product.Stock = available;
            _reserved[product.Id] = reserved;
            _lowStockLevels[product.Id] = _settings.LowStockThreshold;

            var storeProduct = MockData.GetById(product.Id);
            if (storeProduct is not null)
            {
                if (!string.IsNullOrWhiteSpace(rename))
                    storeProduct.Name = rename;
                storeProduct.Sku = sku;
                storeProduct.Stock = available;
                storeProduct.InStock = available > 0;
            }
        }

        // Seed a couple history entries for demo.
        var lanyard = _products.All.FirstOrDefault(p => p.Name.Contains("Lanyard", StringComparison.OrdinalIgnoreCase));
        if (lanyard is not null)
        {
            _history.Add(new InventoryHistoryEntry
            {
                ProductId = lanyard.Id,
                ProductName = lanyard.Name,
                Type = "Add Stock",
                Quantity = 50,
                PreviousStock = 150,
                NewStock = 200,
                Reason = "Restock",
                Date = new DateTime(2026, 8, 14, 10, 0, 0),
                AdminName = "Admin User"
            });
            _history.Add(new InventoryHistoryEntry
            {
                ProductId = lanyard.Id,
                ProductName = lanyard.Name,
                Type = "Remove Stock",
                Quantity = 3,
                PreviousStock = 153,
                NewStock = 150,
                Reason = "Order Allocation",
                Notes = "Reserved for pending order",
                Date = new DateTime(2026, 8, 10, 15, 30, 0),
                AdminName = "Admin User"
            });
        }

        _products.NotifyChanged();
    }
}
