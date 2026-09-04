namespace NUBulldogsExchange.Web.Shared.Data;

/// <summary>
/// Shared UI helpers (placeholder image, formatting). Not application records.
/// </summary>
public static class CatalogHelpers
{
    public const string PlaceholderImage =
        "data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 width=%22800%22 height=%22800%22 viewBox=%220 0 800 800%22%3E%3Crect fill=%22%23F1F4F8%22 width=%22800%22 height=%22800%22/%3E%3Crect x=%22280%22 y=%22280%22 width=%22240%22 height=%22240%22 rx=%2224%22 fill=%22%23123A63%22/%3E%3Ctext x=%22400%22 y=%22420%22 text-anchor=%22middle%22 fill=%22%23F9C424%22 font-family=%22Arial%22 font-size=%2248%22 font-weight=%22700%22%3ENU%3C/text%3E%3C/svg%3E";

    public static string FormatColorName(string color) =>
        string.IsNullOrWhiteSpace(color)
            ? color
            : char.ToUpperInvariant(color[0]) + color[1..].ToLowerInvariant();
}

/// <summary>
/// Backward-compatible alias so existing Razor bindings to MockData.PlaceholderImage keep compiling
/// while application records are loaded from the database.
/// </summary>
public static class MockData
{
    public const string PlaceholderImage = CatalogHelpers.PlaceholderImage;

    public static string FormatColorName(string color) => CatalogHelpers.FormatColorName(color);

    public static string StatusKey(string status) => status switch
    {
        "Ready for Pickup" => "ready",
        "Processing" => "processing",
        "Pending" => "pending",
        "Completed" => "completed",
        "Cancelled" => "cancelled",
        "Confirmed" => "confirmed",
        _ => "pending"
    };
}
