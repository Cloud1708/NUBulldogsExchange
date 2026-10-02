namespace NUBulldogsExchange.Web.Shared.Data;

public class InventoryHistoryEntry
{
    public const string SystemActor = "System";
    public const string ManualReference = "Manual Adjustment";

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

    public int Delta => NewStock - PreviousStock;

    public bool IsSystem =>
        string.Equals(AdminName, SystemActor, StringComparison.OrdinalIgnoreCase)
        || string.Equals(NormalizedReason, "Customer Order", StringComparison.OrdinalIgnoreCase)
        || string.Equals(NormalizedReason, "Order Cancellation", StringComparison.OrdinalIgnoreCase);

    public string NormalizedType
    {
        get
        {
            if (Type.Equals("Sale", StringComparison.OrdinalIgnoreCase)
                || Type.Equals("Remove Stock", StringComparison.OrdinalIgnoreCase))
                return "Remove Stock";

            if (Type.Equals("Restore Stock", StringComparison.OrdinalIgnoreCase))
                return "Restore Stock";

            if (Type.Equals("Restock", StringComparison.OrdinalIgnoreCase)
                && (Reason.Equals("Order", StringComparison.OrdinalIgnoreCase)
                    || Reason.Equals("Order Cancellation", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(AdminName, SystemActor, StringComparison.OrdinalIgnoreCase)))
                return "Restore Stock";

            if (Type.Equals("Restock", StringComparison.OrdinalIgnoreCase)
                || Type.Equals("Add Stock", StringComparison.OrdinalIgnoreCase))
                return "Add Stock";

            if (Type.Equals("Set Stock", StringComparison.OrdinalIgnoreCase)
                || Type.Equals("Set Exact", StringComparison.OrdinalIgnoreCase))
                return "Set Exact";

            if (Delta < 0) return "Remove Stock";
            if (Delta > 0 && string.Equals(AdminName, SystemActor, StringComparison.OrdinalIgnoreCase))
                return "Restore Stock";
            if (Delta > 0) return "Add Stock";
            return string.IsNullOrWhiteSpace(Type) ? "Add Stock" : Type;
        }
    }

    public string NormalizedReason
    {
        get
        {
            if (Reason.Equals("Order", StringComparison.OrdinalIgnoreCase))
            {
                return Type.Equals("Restock", StringComparison.OrdinalIgnoreCase)
                    || Type.Equals("Restore Stock", StringComparison.OrdinalIgnoreCase)
                    ? "Order Cancellation"
                    : "Customer Order";
            }

            return string.IsNullOrWhiteSpace(Reason) ? "Manual Adjustment" : Reason.Trim();
        }
    }

    public string ActionKey => NormalizedType switch
    {
        "Add Stock" => "add",
        "Remove Stock" => "remove",
        "Set Exact" => "set",
        "Restore Stock" => "restore",
        _ => "add"
    };

    public string TypeLabel => NormalizedType;

    public string VariantDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(VariantLabel)
                && !VariantLabel.Equals("—", StringComparison.Ordinal)
                && !VariantLabel.Equals("-", StringComparison.Ordinal)
                && !VariantLabel.Equals("Default", StringComparison.OrdinalIgnoreCase))
                return VariantLabel.Trim();

            return "No Variant";
        }
    }

    public string ReferenceDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Reference))
                return Reference.Trim();
            if (IsSystem)
                return string.Empty;
            return ManualReference;
        }
    }

    public bool HasOrderReference =>
        !string.IsNullOrWhiteSpace(ReferenceDisplay)
        && !ReferenceDisplay.Equals(ManualReference, StringComparison.OrdinalIgnoreCase)
        && ReferenceDisplay.StartsWith("NUBE-", StringComparison.OrdinalIgnoreCase);

    public string QtyDisplay
    {
        get
        {
            if (NormalizedType == "Set Exact")
                return $"= {NewStock}";
            var delta = Delta;
            if (delta > 0) return $"+{delta}";
            if (delta < 0) return delta.ToString();
            return Quantity > 0
                ? (NormalizedType is "Remove Stock" ? $"-{Quantity}" : $"+{Quantity}")
                : "0";
        }
    }

    public string QuantityLabel => QtyDisplay;

    public string ChangeLabel
    {
        get
        {
            var delta = Delta;
            if (delta > 0) return $"+{delta}";
            if (delta < 0) return delta.ToString();
            return "0";
        }
    }

    public string PerformedByDisplay =>
        IsSystem ? SystemActor : (string.IsNullOrWhiteSpace(AdminName) ? "Admin" : AdminName);

    public string DateLabel => Date.ToString("MMM d, yyyy");
    public string DateTimeLabel => Date.ToString("MMM d, yyyy h:mm tt");
    public string DateLine => Date.ToString("MMM d, yyyy");
    public string TimeLine => Date.ToString("h:mm tt");
    public string DetailDateTime => Date.ToString("MMMM d, yyyy • h:mm tt");

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

            if (line.StartsWith("Order ", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(Reference))
            {
                Reference = line["Order ".Length..].Trim();
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

    /// <summary>Variant rows, or the product stock record when there are no variants.</summary>
    public int LowStockRecordCount => HasVariants
        ? LowStockVariantCount
        : TotalStock > 0 && TotalStock <= LowStockLevel ? 1 : 0;

    public int OutOfStockRecordCount => HasVariants
        ? OutOfStockVariantCount
        : TotalStock <= 0 ? 1 : 0;

    public string VariantCountLabel =>
        HasVariants
            ? $"{Variants.Count} {(Variants.Count == 1 ? "variant" : "variants")}"
            : "No variants";

    public IEnumerable<string> StockAlertLines
    {
        get
        {
            yield return $"{LowStockRecordCount} Low Stock";
            yield return $"{OutOfStockRecordCount} Out of Stock";
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

    public bool HasStockAlerts => LowStockRecordCount > 0 || OutOfStockRecordCount > 0;

    public string StockState
    {
        get
        {
            if (OutOfStockRecordCount > 0) return "Critical";
            if (LowStockRecordCount > 0) return "Attention";
            return "Healthy";
        }
    }

    public string StockStateKey => StockState switch
    {
        "Critical" => "critical",
        "Attention" => "attention",
        _ => "healthy"
    };

    public bool IsLowOrOut => StockStateKey is "critical" or "attention";

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
