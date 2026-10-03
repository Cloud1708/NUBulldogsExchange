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
        "Lost Item",
        "Manual Adjustment"
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
        await _settings.EnsureLoadedAsync();
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

    public const int PageSize = 10;

    public int TotalInventory => Rows.Sum(r => Math.Max(0, r.TotalStock));
    public int HealthyProductCount => Rows.Count(r => r.StockStateKey == "healthy");
    public int LowStockRecordCount => Rows.Sum(r => r.LowStockRecordCount);
    public int OutOfStockRecordCount => Rows.Sum(r => r.OutOfStockRecordCount);

    public int InStockCount => HealthyProductCount;
    public int LowStockCount => LowStockRecordCount;
    public int OutOfStockCount => OutOfStockRecordCount;

    public IReadOnlyList<InventoryHistoryEntry> History =>
        _history.OrderByDescending(h => h.Date).ToList();

    public IEnumerable<InventoryRow> Rows =>
        _products.All.OrderBy(p => p.Id).Select(ToRow);

    public InventoryRow? GetRow(int productId)
    {
        var product = _products.GetById(productId);
        return product is null ? null : ToRow(product);
    }

    public IEnumerable<InventoryRow> Filter(string? search, string category, string stockStatus, bool needsAttention)
    {
        IEnumerable<InventoryRow> query = Rows;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r =>
                r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Sku.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Category.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Variants.Any(v =>
                    !string.IsNullOrWhiteSpace(v.Sku) &&
                    v.Sku.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(category) &&
            !category.Equals("All Categories", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => r.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        query = stockStatus switch
        {
            "Healthy" => query.Where(r => r.StockStateKey == "healthy"),
            "Attention" => query.Where(r => r.StockStateKey == "attention"),
            "Low Stock" => query.Where(r => r.LowStockRecordCount > 0),
            "Out of Stock" => query.Where(r => r.OutOfStockRecordCount > 0),
            _ => query
        };

        if (needsAttention)
            query = query.Where(r => r.IsLowOrOut);

        return query;
    }

    public bool SearchMatchesVariantSku(InventoryRow row, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return false;
        var term = search.Trim();
        return row.Variants.Any(v =>
            !string.IsNullOrWhiteSpace(v.Sku) &&
            v.Sku.Contains(term, StringComparison.OrdinalIgnoreCase));
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
            return (false, type == "Remove Stock"
                ? "Cannot remove more than the current stock."
                : "Stock cannot be lower than 0.");

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
            Reference = InventoryHistoryEntry.ManualReference,
            Notes = InventoryHistoryEntry.BuildNotes(variantLabel, InventoryHistoryEntry.ManualReference, notes),
            Date = DateTime.Now,
            AdminName = string.IsNullOrWhiteSpace(adminName) ? "Admin" : adminName
        };

        await _db.AddInventoryHistoryAsync(entry);
        entry.HydrateMetaFromNotes();
        _history.Insert(0, entry);

        OnChange?.Invoke();
        return (true, "Inventory updated successfully.");
    }

    /// <summary>
    /// Writes one history row per order line for automatic stock changes.
    /// Call before in-memory ApplyPurchase/ApplyCancellation so previous stock is accurate.
    /// Skipped when the database already writes inventory_history on order stock apply.
    /// </summary>
    public async Task LogOrderStockMovementsAsync(AdminOrder order, bool restore)
    {
        if (_db.OrderStockWritesInventoryHistory)
            return;
        if (order.Items.Count == 0)
            return;

        await EnsureLoadedAsync();

        foreach (var item in order.Items)
        {
            var qty = Math.Max(0, item.Quantity);
            if (qty == 0) continue;

            var product = _products.GetById(item.ProductId);
            if (product is null) continue;

            ProductVariant? variant = null;
            string? variantLabel = null;
            int previous;

            if (product.HasVariants)
            {
                if (item.VariantId is int vid)
                    variant = product.Variants.FirstOrDefault(v => v.Id == vid);
                variant ??= ProductVariantLogic.Find(product.Variants, item.ColorName, item.Size);
                previous = variant?.StockQuantity ?? product.Stock;
                variantLabel = variant is not null
                    ? InventoryRow.FormatVariantLabel(variant)
                    : FormatItemVariant(item);
            }
            else
            {
                previous = product.Stock;
            }

            var next = restore
                ? previous + qty
                : Math.Max(0, previous - qty);

            var entry = new InventoryHistoryEntry
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Type = restore ? "Restore Stock" : "Remove Stock",
                Quantity = qty,
                PreviousStock = previous,
                NewStock = next,
                Reason = restore ? "Order Cancellation" : "Customer Order",
                VariantLabel = variantLabel,
                Reference = order.Id,
                Notes = InventoryHistoryEntry.BuildNotes(
                    variantLabel,
                    order.Id,
                    restore
                        ? "Stock restored due to order cancellation."
                        : "Stock automatically deducted due to customer order placement."),
                Date = DateTime.Now,
                AdminName = InventoryHistoryEntry.SystemActor
            };

            try
            {
                await _db.AddInventoryHistoryAsync(entry);
                entry.HydrateMetaFromNotes();
                _history.Insert(0, entry);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }

        OnChange?.Invoke();
    }

    public const int HistoryPageSize = 10;

    public int HistoryTotalMovements => History.Count;
    public int HistoryUnitsAdded => History.Where(h => h.Delta > 0).Sum(h => h.Delta);
    public int HistoryUnitsRemoved => History.Where(h => h.Delta < 0).Sum(h => Math.Abs(h.Delta));
    public int HistoryManualAdjustments => History.Count(h => !h.IsSystem);

    public IEnumerable<InventoryHistoryEntry> FilterHistory(
        string? search,
        string action,
        string reason,
        string performedBy,
        string datePreset,
        DateTime? customFrom,
        DateTime? customTo,
        string sort = "newest")
    {
        IEnumerable<InventoryHistoryEntry> query = History;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(h =>
            {
                var product = _products.GetById(h.ProductId);
                return h.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (product?.Sku.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (h.VariantLabel?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (h.Reference?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || h.PerformedByDisplay.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (product?.Variants.Any(v =>
                        !string.IsNullOrWhiteSpace(v.Sku)
                        && v.Sku.Contains(term, StringComparison.OrdinalIgnoreCase)) ?? false);
            });
        }

        if (!string.IsNullOrWhiteSpace(action) &&
            !action.Equals("All Actions", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(h => h.NormalizedType.Equals(action, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(reason) &&
            !reason.Equals("All Reasons", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(h => h.NormalizedReason.Equals(reason, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(performedBy) &&
            !performedBy.Equals("All Performed By", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(h => h.PerformedByDisplay.Equals(performedBy, StringComparison.OrdinalIgnoreCase));
        }

        var (from, to) = ResolveDateRange(datePreset, customFrom, customTo);
        if (from is DateTime start)
            query = query.Where(h => h.Date.Date >= start.Date);
        if (to is DateTime end)
            query = query.Where(h => h.Date.Date <= end.Date);

        return sort.Equals("oldest", StringComparison.OrdinalIgnoreCase)
            ? query.OrderBy(h => h.Date).ThenBy(h => h.Id)
            : query.OrderByDescending(h => h.Date).ThenByDescending(h => h.Id);
    }

    public IEnumerable<string> HistoryActionOptions =>
        new[] { "All Actions" }
            .Concat(History.Select(h => h.NormalizedType).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            .ToList();

    public IEnumerable<string> HistoryReasonOptions =>
        new[] { "All Reasons" }
            .Concat(History.Select(h => h.NormalizedReason).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            .ToList();

    public IEnumerable<string> HistoryActorOptions =>
        new[] { "All Performed By" }
            .Concat(History.Select(h => h.PerformedByDisplay).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            .ToList();

    private static (DateTime? From, DateTime? To) ResolveDateRange(string preset, DateTime? customFrom, DateTime? customTo)
    {
        DateTime? start = preset switch
        {
            "today" => DateTime.Today,
            "7" => DateTime.Today.AddDays(-6),
            "30" => DateTime.Today.AddDays(-29),
            "month" => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
            "custom" => customFrom?.Date,
            _ => null
        };
        DateTime? end = preset switch
        {
            "today" => DateTime.Today,
            "7" or "30" => DateTime.Today,
            "month" => new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month)),
            "custom" => customTo?.Date,
            _ => null
        };
        if (start is DateTime a && end is DateTime b && a > b)
            return (b, a);
        return (start, end);
    }

    private static string? FormatItemVariant(AdminOrderItem item)
    {
        var color = item.ColorName?.Trim();
        var size = item.Size?.Trim();
        var hasSize = !string.IsNullOrWhiteSpace(size)
                      && !size.Equals("Free Size", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(color) && hasSize)
            return $"{color} / {size}";
        if (!string.IsNullOrWhiteSpace(color))
            return color;
        if (hasSize)
            return size;
        return null;
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
