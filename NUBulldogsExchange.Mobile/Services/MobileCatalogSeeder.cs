using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.Services;

/// <summary>
/// Seeds a small demo catalog into the local storefront DB when it is empty
/// so Home sections can render during Mobile development/preview.
/// </summary>
public static class MobileCatalogSeeder
{
    public static async Task EnsureSampleProductsAsync(IAppDatabase db, ProductCatalogService catalog)
    {
        try
        {
            await catalog.EnsureLoadedAsync();
            if (catalog.Products.Count > 0)
                return;

            var samples = BuildSamples();
            foreach (var product in samples)
            {
                product.Id = 0;
                await db.UpsertProductAsync(product);
            }

            await catalog.ReloadAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MobileCatalogSeeder: {ex}");
        }
    }

    private static List<Product> BuildSamples() =>
    [
        Make(1, "Classic NU Bulldogs Tee", "T-Shirts", 499, 599, true, false, true, "Best Seller",
            "https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?w=600&auto=format&fit=crop&q=80", 4.7, 342),
        Make(2, "NU Pride Hoodie", "Hoodies", 1299, 1499, true, true, false, "Sale",
            "https://images.unsplash.com/photo-1556821840-3a63f95609a7?w=600&auto=format&fit=crop&q=80", 4.8, 218),
        Make(3, "Official NU Polo Shirt", "Polo Shirts", 799, null, true, false, false, null,
            "https://images.unsplash.com/photo-1586790170083-2f9ceadc561d?w=600&auto=format&fit=crop&q=80", 4.6, 156),
        Make(4, "Campus Bomber Jacket", "Jackets", 1899, null, true, true, false, "New",
            "https://images.unsplash.com/photo-1551028719-00167b16eac5?w=600&auto=format&fit=crop&q=80", 4.9, 89),
        Make(5, "NU Bulldogs Cap", "Caps", 399, null, false, true, true, null,
            "https://images.unsplash.com/photo-1588850561407-ed78c282e89b?w=600&auto=format&fit=crop&q=80", 4.5, 410),
        Make(6, "Bulldog Tote Bag", "Bags", 549, null, false, true, false, "New",
            "https://images.unsplash.com/photo-1553062407-98eeb64c6a62?w=600&auto=format&fit=crop&q=80", 4.4, 97),
        Make(7, "NU Training Tee", "T-Shirts", 549, null, true, false, false, null,
            "https://images.unsplash.com/photo-1503342217505-b0a15ec3261c?w=600&auto=format&fit=crop&q=80", 4.6, 203),
        Make(8, "Soft Shell NU Jacket", "Jackets", 1599, 1799, false, false, true, "Best Seller",
            "https://images.unsplash.com/photo-1591047139829-d91aecb6caea?w=600&auto=format&fit=crop&q=80", 4.7, 134)
    ];

    private static Product Make(
        int id,
        string name,
        string category,
        decimal price,
        decimal? original,
        bool featured,
        bool fresh,
        bool best,
        string? badge,
        string image,
        double rating,
        int sold) => new()
    {
        Id = id,
        Name = name,
        Category = category,
        Section = category is "Caps" or "Bags" or "Tumblers" or "Accessories" or "School Supplies"
            ? "accessories"
            : "apparel",
        Price = price,
        OriginalPrice = original,
        ImageUrl = image,
        Images = [image],
        Rating = rating,
        Reviews = Math.Max(12, sold / 3),
        Sold = sold,
        Stock = 50,
        Badge = badge,
        Colors = ["navy", "white"],
        Sizes = category is "Caps" or "Bags" ? ["One Size"] : ["S", "M", "L", "XL"],
        Material = "Cotton blend",
        Sku = $"MOB-{id:000}",
        InStock = true,
        IsFeatured = featured,
        IsFreshDrop = fresh,
        IsBestSeller = best,
        IsNewArrival = fresh,
        IsFavorite = best,
        Description = name,
        FullDescription = name,
        Status = "Active",
        IsPublished = true,
        PublishedAt = DateTime.UtcNow.AddDays(-id)
    };
}
