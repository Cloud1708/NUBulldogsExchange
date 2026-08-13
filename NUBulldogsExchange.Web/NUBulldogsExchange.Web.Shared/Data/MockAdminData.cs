namespace NUBulldogsExchange.Web.Shared.Data;

public record AdminStatCard(
    string Label,
    string Value,
    string Change,
    bool IsPositive,
    string Icon,
    string Tone);

public record AdminOrderRow(
    string Id,
    string Customer,
    string Fulfillment,
    string Date,
    decimal Total,
    string Status);

public record AdminTopProduct(
    int Rank,
    string Name,
    string ImageUrl,
    int Sold,
    decimal Revenue,
    string ShortLabel);

public record AdminStatusSlice(string Label, int Count, string Color);

public static class MockAdminData
{
    public static readonly AdminStatCard[] Stats =
    [
        new("Total Sales", "₱128,450", "+12.5%", true, "dollar-sign", "blue"),
        new("Total Orders", "387", "+8.2%", true, "cart", "green"),
        new("Pending Orders", "23", "-3.1%", false, "alert-triangle", "gold"),
        new("Completed Orders", "342", "+10.7%", true, "check-circle", "teal"),
        new("Total Products", "48", "+4", true, "package", "purple"),
        new("Low Stock", "7", "-2", false, "alert-triangle", "red")
    ];

    public static readonly double[] SalesByMonth = [28, 42, 38, 55, 68, 72, 95, 118];
    public static readonly string[] SalesMonths = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug"];

    public static readonly AdminStatusSlice[] OrderStatus =
    [
        new("Completed", 342, "#22C55E"),
        new("Processing", 23, "#3B82F6"),
        new("Pending", 12, "#F9C424"),
        new("Ready for Pickup", 8, "#A855F7"),
        new("Cancelled", 2, "#EF4444")
    ];

    public static readonly AdminOrderRow[] RecentOrders =
    [
        new("NUBE-1024", "Maria Santos", "Campus Pickup", "2026-08-05", 1797, "Ready for Pickup"),
        new("NUBE-1023", "Juan dela Cruz", "Campus Pickup", "2026-08-04", 999, "Processing"),
        new("NUBE-1022", "Ana Reyes", "Delivery", "2026-08-03", 848, "Pending"),
        new("NUBE-1021", "Carlo Mendoza", "Campus Pickup", "2026-08-02", 2246, "Completed"),
        new("NUBE-1020", "Sofia Lim", "Campus Pickup", "2026-08-01", 828, "Completed"),
        new("NUBE-1019", "Mark Torres", "Campus Pickup", "2026-07-30", 299, "Cancelled")
    ];

    public static List<AdminTopProduct> TopSelling()
    {
        var products = MockData.Products;
        return
        [
            Make(1, products.First(p => p.Id == 1), 891, 132759, "Lanyard"),
            Make(2, products.First(p => p.Id == 2), 543, 162357, "Cap"),
            Make(3, products.First(p => p.Id == 3), 342, 170658, "Classic Shirt"),
            Make(4, products.First(p => p.Id == 4), 310, 39990, "Notebook"),
            Make(5, products.First(p => p.Id == 5), 215, 75035, "Tumbler")
        ];
    }

    private static AdminTopProduct Make(int rank, Product product, int sold, decimal revenue, string shortLabel) =>
        new(rank, product.Name, product.ImageUrl, sold, revenue, shortLabel);

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
