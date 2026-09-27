namespace NUBulldogsExchange.Web.Shared.Data;

/// <summary>
/// Shared color/size variant helpers used by Web admin, catalog, cart, and checkout.
/// </summary>
public static class ProductVariantLogic
{
    public static readonly (string Name, string Hex)[] SuggestedColors =
    [
        ("Navy", "#0B1F3A"),
        ("Black", "#111111"),
        ("White", "#FFFFFF"),
        ("Gray", "#6B7280"),
        ("Yellow", "#F5C518"),
        ("Blue", "#1D4ED8"),
        ("Red", "#C81E1E")
    ];

    public static readonly string[] DefaultSizes = ["S", "M", "L", "XL"];

    public static bool HasSize(ProductVariant? variant) =>
        !string.IsNullOrWhiteSpace(variant?.Size);

    public static bool HasColor(ProductVariant? variant) =>
        !string.IsNullOrWhiteSpace(variant?.ColorName);

    public static bool SameText(string? left, string? right) =>
        string.Equals((left ?? string.Empty).Trim(), (right ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

    public static ProductVariant? Find(
        IEnumerable<ProductVariant> variants,
        string? colorName,
        string? size)
    {
        return variants.FirstOrDefault(v =>
            v.IsActive &&
            SameText(v.ColorName, colorName) &&
            SameText(v.Size, size));
    }

    public static IEnumerable<string> DistinctColors(IEnumerable<ProductVariant> variants) =>
        variants
            .Where(HasColor)
            .Select(v => v.ColorName!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<string> DistinctSizes(IEnumerable<ProductVariant> variants) =>
        variants
            .Where(HasSize)
            .Select(v => v.Size.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);

    public static string ColorHex(IEnumerable<ProductVariant> variants, string colorName)
    {
        var hex = variants
            .FirstOrDefault(v => SameText(v.ColorName, colorName) && !string.IsNullOrWhiteSpace(v.ColorHex))
            ?.ColorHex;
        return NormalizeHex(hex) ?? DefaultHex(colorName);
    }

    public static bool ColorIsSoldOut(IEnumerable<ProductVariant> variants, string colorName) =>
        variants
            .Where(v => v.IsActive && SameText(v.ColorName, colorName))
            .All(v => v.StockQuantity <= 0);

    public static string DefaultHex(string? colorName)
    {
        var match = SuggestedColors.FirstOrDefault(c =>
            c.Name.Equals(colorName?.Trim(), StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrEmpty(match.Hex) ? "#123A63" : match.Hex;
    }

    public static string? NormalizeHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return null;

        var value = hex.Trim();
        if (!value.StartsWith('#'))
            value = "#" + value;

        if (value.Length == 4)
            value = $"#{value[1]}{value[1]}{value[2]}{value[2]}{value[3]}{value[3]}";

        return value.Length == 7 ? value.ToUpperInvariant() : value;
    }

    public static string ColorAbbreviation(string? colorName)
    {
        var key = (colorName ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "navy" => "NV",
            "black" => "BK",
            "white" => "WH",
            "gray" or "grey" => "GY",
            "yellow" => "YL",
            "blue" => "BL",
            "red" => "RD",
            _ => Abbreviate(colorName)
        };
    }

    public static string? SuggestSku(string? baseSku, string? colorName, string? size)
    {
        var prefix = (baseSku ?? string.Empty).Trim().TrimEnd('-');
        if (string.IsNullOrWhiteSpace(prefix))
            return null;

        var parts = new List<string> { prefix };
        if (!string.IsNullOrWhiteSpace(colorName))
            parts.Add(ColorAbbreviation(colorName));
        if (!string.IsNullOrWhiteSpace(size))
            parts.Add(size.Trim().ToUpperInvariant().Replace(' ', '-'));

        return parts.Count == 1 ? null : string.Join("-", parts);
    }

    public static bool SkuLooksGenerated(string? sku, string? baseSku, string? colorName, string? size)
    {
        if (string.IsNullOrWhiteSpace(sku))
            return true;

        var suggested = SuggestSku(baseSku, colorName, size);
        return suggested is not null && sku.Equals(suggested, StringComparison.OrdinalIgnoreCase);
    }

    public static void ApplySuggestedSku(ProductVariant variant, string? baseSku, string? previousColor, string? previousSize)
    {
        if (!SkuLooksGenerated(variant.Sku, baseSku, previousColor, previousSize))
            return;

        variant.Sku = SuggestSku(baseSku, variant.ColorName, variant.Size);
    }

    public static string? Validate(bool hasSize, bool hasColor, IReadOnlyList<ProductVariant> variants)
    {
        if (!hasSize && !hasColor)
            return null;

        if (hasColor && !variants.Any(HasColor))
            return "Add at least one color.";

        if (hasSize && !variants.Any(HasSize))
            return "Add at least one size variant.";

        if (hasColor && hasSize)
        {
            foreach (var group in variants.GroupBy(v => (v.ColorName ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(group.Key))
                    return "Each color + size variant needs a color name.";
                if (!group.Any(HasSize))
                    return $"Color {group.Key} needs at least one size.";
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variant in variants)
        {
            if (hasColor && string.IsNullOrWhiteSpace(variant.ColorName))
                return "Each color variant needs a color name.";
            if (hasSize && string.IsNullOrWhiteSpace(variant.Size))
                return "Each size variant needs a size name.";
            if (variant.StockQuantity < 0)
                return "Variant stock cannot be negative.";

            var key = $"{(variant.ColorName ?? string.Empty).Trim()}|{(variant.Size ?? string.Empty).Trim()}";
            if (!seen.Add(key))
            {
                if (hasColor && hasSize)
                    return $"Duplicate variant: {variant.ColorName} / {variant.Size}";
                if (hasColor)
                    return $"Duplicate color: {variant.ColorName}";
                return $"Duplicate product size: {(variant.Size ?? string.Empty).Trim()}";
            }
        }

        return null;
    }

    public static string FormatVariantLabel(string? colorName, string? size)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(colorName))
            parts.Add($"Color: {colorName.Trim()}");
        if (!string.IsNullOrWhiteSpace(size) &&
            !size.Equals("Free Size", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"Size: {size.Trim()}");
        }

        return string.Join(" · ", parts);
    }

    private static string Abbreviate(string? name)
    {
        var letters = new string((name ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
        if (letters.Length >= 2)
            return letters[..2].ToUpperInvariant();
        if (letters.Length == 1)
            return letters.ToUpperInvariant();
        return "XX";
    }
}
