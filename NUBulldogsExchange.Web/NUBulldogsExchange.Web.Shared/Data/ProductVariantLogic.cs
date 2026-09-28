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

    public static string? ValidateUnified(IReadOnlyList<ProductVariant> variants)
    {
        if (variants.Count == 0)
            return "Add at least one variant.";

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variant in variants)
        {
            if (string.IsNullOrWhiteSpace(variant.ColorName) && string.IsNullOrWhiteSpace(variant.Size))
                return "Each variant needs a Color or a Size.";
            if (variant.StockQuantity < 0)
                return "Variant stock cannot be negative.";

            var key = $"{(variant.ColorName ?? string.Empty).Trim()}|{(variant.Size ?? string.Empty).Trim()}";
            if (!seen.Add(key))
            {
                var color = string.IsNullOrWhiteSpace(variant.ColorName) ? "(none)" : variant.ColorName.Trim();
                var size = string.IsNullOrWhiteSpace(variant.Size) ? "(none)" : variant.Size.Trim();
                return $"Duplicate variant: {color} / {size}";
            }
        }

        return null;
    }

    public static string? Validate(bool hasSize, bool hasColor, IReadOnlyList<ProductVariant> variants) =>
        !hasSize && !hasColor ? null : ValidateUnified(variants);

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

    public static string FormatInsufficientStock(string? productName, string? colorName, string? size)
    {
        var name = string.IsNullOrWhiteSpace(productName) ? "Selected item" : productName.Trim();
        var hasColor = !string.IsNullOrWhiteSpace(colorName);
        var hasSize = !string.IsNullOrWhiteSpace(size)
            && !size.Equals("Free Size", StringComparison.OrdinalIgnoreCase);

        if (hasColor && hasSize)
            return $"{name} - {colorName!.Trim()}/{size!.Trim()} is no longer available.";
        if (hasSize)
            return $"{name} - Size {size!.Trim()} is no longer available.";
        if (hasColor)
            return $"{name} - {colorName!.Trim()} is no longer available.";
        return "Insufficient stock for the selected item.";
    }

    public static void ApplyOrderItemStock(Product product, AdminOrderItem item, bool restore)
    {
        var qty = Math.Max(0, item.Quantity);
        if (qty == 0) return;

        var delta = restore ? qty : -qty;
        if (item.VariantId is int vid && product.Variants.Count > 0)
        {
            var variant = product.Variants.FirstOrDefault(v => v.Id == vid);
            if (variant is not null)
                variant.StockQuantity = Math.Max(0, variant.StockQuantity + delta);
            product.Stock = product.Variants.Sum(v => Math.Max(0, v.StockQuantity));
        }
        else
        {
            product.Stock = Math.Max(0, product.Stock + delta);
        }

        product.Sold = Math.Max(0, product.Sold + (restore ? -qty : qty));
        product.InStock = product.Stock > 0;
    }

    public static void ApplyOrderItemStock(AdminProduct product, AdminOrderItem item, bool restore)
    {
        var qty = Math.Max(0, item.Quantity);
        if (qty == 0) return;

        var delta = restore ? qty : -qty;
        if (item.VariantId is int vid && product.Variants.Count > 0)
        {
            var variant = product.Variants.FirstOrDefault(v => v.Id == vid);
            if (variant is not null)
                variant.StockQuantity = Math.Max(0, variant.StockQuantity + delta);
            product.Stock = product.Variants.Sum(v => Math.Max(0, v.StockQuantity));
        }
        else
        {
            product.Stock = Math.Max(0, product.Stock + delta);
        }

        product.Sold = Math.Max(0, product.Sold + (restore ? -qty : qty));
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
