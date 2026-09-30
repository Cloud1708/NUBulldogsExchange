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

    /// <summary>Display-only; persisted inside Notes when the DB has no variant column.</summary>
    public string? VariantLabel { get; set; }

    /// <summary>Display-only; persisted inside Notes when the DB has no reference column.</summary>
    public string? Reference { get; set; }

    public string QuantityLabel => Type switch
    {
        "Remove Stock" => $"-{Quantity}",
        "Set Stock" => $"→ {NewStock}",
        _ => $"+{Quantity}"
    };

    public string ChangeLabel
    {
        get
        {
            var delta = NewStock - PreviousStock;
            if (delta > 0) return $"+{delta}";
            if (delta < 0) return delta.ToString();
            return "0";
        }
    }

    public string DateLabel => Date.ToString("MMM d, yyyy");
    public string DateTimeLabel => Date.ToString("MMM d, yyyy h:mm tt");

    public static string BuildNotes(string? variantLabel, string? reference, string? notes)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(variantLabel))
            parts.Add($"Variant: {variantLabel.Trim()}");
        if (!string.IsNullOrWhiteSpace(reference))
            parts.Add($"Ref: {reference.Trim()}");
        if (!string.IsNullOrWhiteSpace(notes))
            parts.Add(notes.Trim());
        return string.Join('\n', parts);
    }

    public void HydrateMetaFromNotes()
    {
        if (string.IsNullOrWhiteSpace(Notes))
            return;

        var lines = Notes.Replace("\r\n", "\n").Split('\n');
        var rest = new List<string>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("Variant:", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(VariantLabel))
            {
                VariantLabel = line["Variant:".Length..].Trim();
                continue;
            }

            if (line.StartsWith("Ref:", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(Reference))
            {
                Reference = line["Ref:".Length..].Trim();
                continue;
            }

            rest.Add(raw);
        }

        Notes = string.Join('\n', rest).Trim();
    }
}

public class InventoryRow
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int TotalStock { get; set; }
    public int LowStockLevel { get; set; } = 20;
    public List<ProductVariant> Variants { get; set; } = [];

    public bool HasVariants => Variants.Count > 0;
    public bool HasColors => Variants.Any(v => !string.IsNullOrWhiteSpace(v.ColorName));
    public bool HasSizes => Variants.Any(v => !string.IsNullOrWhiteSpace(v.Size));

    public string VariantMode =>
        HasColors && HasSizes ? "Color + Size"
        : HasColors ? "Color"
        : HasSizes ? "Size"
        : "None";

    public int OutOfStockVariantCount =>
        Variants.Count(v => v.IsActive && v.StockQuantity <= 0);

    public int LowStockVariantCount =>
        Variants.Count(v =>
            v.IsActive &&
            v.StockQuantity > 0 &&
            v.StockQuantity <= LowStockLevel);

    public IEnumerable<string> StockAlertLines
    {
        get
        {
            if (!HasVariants)
            {
                if (TotalStock <= 0)
                {
                    yield return "Out of Stock";
                    yield break;
                }

                if (TotalStock <= LowStockLevel)
                    yield return "Low Stock";
                yield break;
            }

            if (OutOfStockVariantCount > 0)
                yield return $"{OutOfStockVariantCount} Out of Stock";
            if (LowStockVariantCount > 0)
                yield return $"{LowStockVariantCount} Low Stock";
        }
    }

    public string StockAlertsLabel
    {
        get
        {
            var lines = StockAlertLines.ToList();
            return lines.Count == 0 ? "None" : string.Join("\n", lines);
        }
    }

    public bool HasStockAlerts => StockAlertLines.Any();

    public string StockState
    {
        get
        {
            if (!HasVariants)
            {
                if (TotalStock <= 0) return "Out of Stock";
                if (TotalStock <= LowStockLevel) return "Low Stock";
                return "In Stock";
            }

            if (TotalStock <= 0 || Variants.Where(v => v.IsActive).All(v => v.StockQuantity <= 0))
                return "Out of Stock";

            if (OutOfStockVariantCount > 0 || LowStockVariantCount > 0)
                return "Attention";

            return "In Stock";
        }
    }

    public string StockStateKey => StockState switch
    {
        "Out of Stock" => "out",
        "Low Stock" => "low",
        "Attention" => "attention",
        _ => "in"
    };

    public bool IsLowOrOut => StockStateKey is "out" or "low" or "attention";

    public static string VariantStatus(int stock, int lowStockLevel) => stock switch
    {
        <= 0 => "Out of Stock",
        _ when stock <= lowStockLevel => "Low Stock",
        _ => "In Stock"
    };

    public static string VariantStatusKey(int stock, int lowStockLevel) => stock switch
    {
        <= 0 => "out",
        _ when stock <= lowStockLevel => "low",
        _ => "in"
    };

    public static string FormatVariantLabel(ProductVariant variant)
    {
        var color = variant.ColorName?.Trim();
        var size = variant.Size?.Trim();
        var hasSize = !string.IsNullOrWhiteSpace(size) &&
                      !size.Equals("Free Size", StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(color) && hasSize)
            return $"{color} / {size}";
        if (!string.IsNullOrWhiteSpace(color))
            return color!;
        if (hasSize)
            return size!;
        return "Default";
    }

    public static int SizeSortRank(string? size)
    {
        var key = (size ?? string.Empty).Trim().ToUpperInvariant();
        return key switch
        {
            "XS" => 0,
            "S" => 1,
            "M" => 2,
            "L" => 3,
            "XL" => 4,
            "XXL" => 5,
            "2XL" => 5,
            "3XL" => 6,
            _ => 50 + key.Length
        };
    }

    // Back-compat aliases used by older call sites
    public int Available => TotalStock;
    public int Reserved => 0;
}
