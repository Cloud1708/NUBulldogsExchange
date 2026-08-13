namespace NUBulldogsExchange.Web.Shared.Data;

public class AdminCategory
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public int ProductCount { get; set; }

    public bool IsActive => Status.Equals("Active", StringComparison.OrdinalIgnoreCase);

    public string ProductCountLabel =>
        ProductCount == 1 ? "1 product" : $"{ProductCount} products";

    public AdminCategory Clone() => new()
    {
        Id = Id,
        Name = Name,
        Slug = Slug,
        ImageUrl = ImageUrl,
        Description = Description,
        Status = Status,
        ProductCount = ProductCount
    };

    public static string ToSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var chars = name.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return slug.Trim('-');
    }
}
