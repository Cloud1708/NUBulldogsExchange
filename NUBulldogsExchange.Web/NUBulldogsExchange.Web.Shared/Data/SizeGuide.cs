using System.Globalization;

namespace NUBulldogsExchange.Web.Shared.Data;

public class SizeGuide
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SizingStandard { get; set; } = SizeGuideCatalog.Philippines;
    public string MeasurementUnit { get; set; } = SizeGuideCatalog.Inches;
    public string? Notes { get; set; }
    public List<SizeGuideColumn> Columns { get; set; } = [];
    public List<SizeGuideRow> Rows { get; set; } = [];
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public SizeGuide Clone() => new()
    {
        Id = Id,
        Name = Name,
        SizingStandard = SizingStandard,
        MeasurementUnit = MeasurementUnit,
        Notes = Notes,
        Columns = Columns.Select(c => c.Clone()).ToList(),
        Rows = Rows.Select(r => r.Clone()).ToList(),
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt
    };
}

public class SizeGuideColumn
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;

    public SizeGuideColumn Clone() => new() { Id = Id, Name = Name };
}

public class SizeGuideRow
{
    public string Size { get; set; } = string.Empty;
    public Dictionary<string, decimal?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public SizeGuideRow Clone() => new()
    {
        Size = Size,
        Values = new Dictionary<string, decimal?>(Values, StringComparer.OrdinalIgnoreCase)
    };
}

public static class SizeGuideCatalog
{
    public const string Philippines = "ph";
    public const string Us = "us";
    public const string Eu = "eu";
    public const string Uk = "uk";
    public const string Asia = "asia";
    public const string International = "international";
    public const string Custom = "custom";

    public const string Inches = "in";
    public const string Centimeters = "cm";

    public static readonly (string Key, string Label)[] Standards =
    [
        (Philippines, "Philippines (PH)"),
        (Us, "US"),
        (Eu, "EU"),
        (Uk, "UK"),
        (Asia, "Asia / Asian Fit"),
        (International, "International / Generic"),
        (Custom, "Custom")
    ];

    public static readonly (string Key, string Label)[] Units =
    [
        (Inches, "Inches (in)"),
        (Centimeters, "Centimeters (cm)")
    ];

    public static readonly string[] SuggestedSizes =
        ["XS", "S", "M", "L", "XL", "XXL", "3XL"];

    public static readonly string[] SuggestedColumns =
        ["Chest", "Length", "Shoulder", "Sleeve", "Waist", "Hip", "Inseam", "Rise"];

    public static SizeGuide NewDraft()
    {
        var chest = NewColumn("Chest");
        var length = NewColumn("Length");
        var shoulder = NewColumn("Shoulder");
        var sleeve = NewColumn("Sleeve");
        return new SizeGuide
        {
            SizingStandard = Philippines,
            MeasurementUnit = Inches,
            Notes = "This size guide follows Philippine (PH) sizing. Measurements may vary by 1–2 inches depending on product style and fit.",
            Columns = [chest, length, shoulder, sleeve],
            Rows = SuggestedSizes.Skip(1).Take(5).Select(size => new SizeGuideRow { Size = size }).ToList()
        };
    }

    public static SizeGuideColumn NewColumn(string name) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = name.Trim()
    };

    public static string StandardLabel(string? key)
    {
        var match = Standards.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(match.Label) ? "Custom" : match.Label;
    }

    public static string StandardBadge(string? key) => key?.Trim().ToLowerInvariant() switch
    {
        Philippines => "PH Sizing",
        Us => "US Sizing",
        Eu => "EU Sizing",
        Uk => "UK Sizing",
        Asia => "Asian Fit",
        International => "International Sizing",
        _ => "Custom Sizing"
    };

    public static string StandardCustomerLabel(string? key) => key?.Trim().ToLowerInvariant() switch
    {
        Philippines => "Philippines (PH) Sizing",
        Us => "US Sizing",
        Eu => "EU Sizing",
        Uk => "UK Sizing",
        Asia => "Asian Fit",
        International => "International Sizing",
        _ => "Custom Sizing"
    };

    public static string StandardNote(string? key, string? unit)
    {
        var region = StandardCustomerLabel(key);
        var vary = UnitIsInches(unit)
            ? "Measurements may vary by 1–2 inches depending on the product style, material, and fit."
            : "Measurements may vary depending on the product style, material, and fit.";
        if (key?.Equals(Philippines, StringComparison.OrdinalIgnoreCase) == true)
            return $"This size guide follows Philippine (PH) sizing. {vary}";
        return $"This size guide follows {region}. {vary}";
    }

    public static string UnitLabel(string? key)
    {
        var match = Units.FirstOrDefault(u => u.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(match.Label) ? "Inches (in)" : match.Label;
    }

    public static bool UnitIsInches(string? key) =>
        string.IsNullOrWhiteSpace(key) || key.Equals(Inches, StringComparison.OrdinalIgnoreCase);

    public static string? HowToMeasure(string columnName)
    {
        var key = (columnName ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "chest" => "Measure around the fullest part of your chest.",
            "length" => "Measure from the highest shoulder point downward.",
            "shoulder" => "Measure from shoulder seam to shoulder seam.",
            "sleeve" => "Measure from the shoulder seam to the wrist/end of sleeve.",
            "waist" => "Measure around your natural waistline.",
            "hip" or "hips" => "Measure around the fullest part of your hips.",
            "inseam" => "Measure from the crotch to the bottom of the leg.",
            "rise" => "Measure from the crotch up to the waistband.",
            _ => null
        };
    }

    public static string FormatValue(decimal? value)
    {
        if (value is null) return "—";
        var number = value.Value;
        return number == decimal.Truncate(number)
            ? number.ToString("0", CultureInfo.InvariantCulture)
            : number.ToString("0.##", CultureInfo.InvariantCulture);
    }

    public static string? Validate(SizeGuide guide)
    {
        if (string.IsNullOrWhiteSpace(guide.Name))
            return "Guide name is required.";
        if (string.IsNullOrWhiteSpace(guide.SizingStandard))
            return "Sizing standard is required.";
        if (string.IsNullOrWhiteSpace(guide.MeasurementUnit))
            return "Measurement unit is required.";

        var columns = guide.Columns
            .Select(c => (c.Name ?? string.Empty).Trim())
            .Where(n => n.Length > 0)
            .ToList();
        if (columns.Count == 0)
            return "Add at least one measurement column.";
        if (columns.Count != columns.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            return "Measurement column names must be unique.";

        var sizes = guide.Rows
            .Select(r => (r.Size ?? string.Empty).Trim())
            .Where(s => s.Length > 0)
            .ToList();
        if (sizes.Count == 0)
            return "Add at least one size row.";
        if (sizes.Count != sizes.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            return "Size rows must be unique.";

        foreach (var row in guide.Rows)
        {
            foreach (var value in row.Values.Values)
            {
                if (value is < 0)
                    return "Measurements must be 0 or greater.";
            }
        }

        return null;
    }

    public static IReadOnlyList<string> MissingSizes(SizeGuide guide, IEnumerable<string> productSizes)
    {
        var guideSizes = new HashSet<string>(
            guide.Rows.Select(r => r.Size.Trim()).Where(s => s.Length > 0),
            StringComparer.OrdinalIgnoreCase);
        return productSizes
            .Select(s => s.Trim())
            .Where(s => s.Length > 0 && !guideSizes.Contains(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
