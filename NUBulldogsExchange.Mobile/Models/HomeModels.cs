using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Mobile.Models;

public sealed class CategoryChip
{
    public string Name { get; init; } = string.Empty;
    public string Icon { get; init; } = "🏷️";
    public Color Background { get; init; } = Colors.LightGray;
    public string? Slug { get; init; }
}

public sealed class HeroBannerItem
{
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string ButtonText { get; init; } = "Shop Now";
    public string ImageUrl { get; init; } = string.Empty;
    public string Route { get; init; } = "shop";
}

public sealed class ProductPair
{
    public Product Left { get; init; } = null!;
    public Product? Right { get; init; }
    public bool HasRight => Right is not null;
}
