namespace NUBulldogsExchange.Web.Shared.Data;

public class InventoryHistoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int PreviousStock { get; set; }
    public int NewStock { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Now;
    public string AdminName { get; set; } = "Admin User";

    public string QuantityLabel => Type switch
    {
        "Remove Stock" => $"-{Quantity}",
        "Set Stock" => $"→ {NewStock}",
        _ => $"+{Quantity}"
    };

    public string DateLabel => Date.ToString("MMM d, yyyy");
}

public class InventoryRow
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int Available { get; set; }
    public int Reserved { get; set; }
    public int LowStockLevel { get; set; } = 20;

    public string StockState => Available switch
    {
        <= 0 => "Out of Stock",
        _ when Available <= LowStockLevel => "Low Stock",
        _ => "In Stock"
    };

    public string StockStateKey => Available switch
    {
        <= 0 => "out",
        _ when Available <= LowStockLevel => "low",
        _ => "in"
    };

    public bool IsLowOrOut => Available <= LowStockLevel;
}
