using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class ReportKpi
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Change { get; set; } = string.Empty;
    public bool IsPositive { get; set; } = true;
}

public class ReportMonthPoint
{
    public string Month { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int Orders { get; set; }
    public decimal AvgOrderValue { get; set; }
}

public class ReportCategoryBar
{
    public string Category { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class ReportBestSeller
{
    public int Rank { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
    public int StockLeft { get; set; }
}

public class ReportTopProduct
{
    public int Rank { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
    public double ProgressPct { get; set; }
}

public class ReportTopVariant
{
    public int Rank { get; set; }
    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string VariantLabel { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
}

public class ReportProductPerformance
{
    public int Rank { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
    public decimal AvgPrice { get; set; }
    public int Orders { get; set; }
    public int CurrentStock { get; set; }
    public string Performance { get; set; } = "No Sales";
    public string PerformanceKey { get; set; } = "none";
}

public class ReportSnapshot
{
    public string RangeKey { get; set; } = "30days";
    public string RangeLabel { get; set; } = "Last 30 Days";
    public List<ReportKpi> Kpis { get; set; } = [];
    public List<ReportMonthPoint> SalesTrend { get; set; } = [];
    public List<AdminStatusSlice> OrderDistribution { get; set; } = [];
    public List<ReportCategoryBar> RevenueByCategory { get; set; } = [];
    public List<ReportBestSeller> BestSellers { get; set; } = [];
    public List<ReportTopProduct> TopSellingProducts { get; set; } = [];
    public List<ReportTopVariant> TopSellingVariants { get; set; } = [];
    public List<ReportTopProduct> TopProductsByRevenue { get; set; } = [];
    public List<ReportProductPerformance> ProductPerformance { get; set; } = [];
    public bool HasData { get; set; } = true;
}

public class AdminReportService
{
    public static readonly (string Key, string Label)[] Ranges =
    [
        ("today", "Today"),
        ("7days", "7 Days"),
        ("30days", "30 Days"),
        ("year", "This Year"),
        ("custom", "Custom")
    ];

    private readonly AdminProductService _products;
    private readonly AdminOrderService _orders;

    public event Action? OnChange;

    public string SelectedRange { get; private set; } = "30days";
    public DateTime? CustomStart { get; private set; }
    public DateTime? CustomEnd { get; private set; }

    public AdminReportService(AdminProductService products, AdminOrderService orders)
    {
        _products = products;
        _orders = orders;
        _products.OnChange += () => OnChange?.Invoke();
        _orders.OnChange += () => OnChange?.Invoke();
    }

    public void SetRange(string key)
    {
        if (Ranges.All(r => r.Key != key))
            return;
        SelectedRange = key;
        OnChange?.Invoke();
    }

    public (bool Success, string Message) ApplyCustomRange(DateTime start, DateTime end)
    {
        if (end.Date < start.Date)
            return (false, "End date must be on or after the start date.");

        CustomStart = start.Date;
        CustomEnd = end.Date;
        SelectedRange = "custom";
        OnChange?.Invoke();
        return (true, "Custom range applied.");
    }

    public ReportSnapshot GetSnapshot()
    {
        var (start, end) = ResolveRange();
        var rangeLabel = FormatRangeLabel(start, end);
        var orders = _orders.All
            .Where(o => o.Date.Date >= start && o.Date.Date <= end)
            .ToList();

        if (orders.Count == 0 && _products.All.Count == 0)
        {
            return new ReportSnapshot
            {
                RangeKey = SelectedRange,
                RangeLabel = rangeLabel,
                HasData = false
            };
        }

        // Existing summary / trend / category analytics: non-cancelled orders in range.
        var activeOrders = orders
            .Where(o => !o.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Product-level sales analytics: successfully fulfilled only.
        var fulfilledOrders = orders.Where(IsFulfilledSale).ToList();

        var gross = activeOrders.Sum(o => o.Total);
        var items = activeOrders.Sum(o => o.ItemCount);
        var aov = activeOrders.Count == 0 ? 0 : Math.Round(gross / activeOrders.Count, 2);

        var colors = ColorMap();
        var distribution = orders
            .GroupBy(o => NormalizeStatus(o.Status))
            .Select(g => new AdminStatusSlice(g.Key, g.Count(), colors.GetValueOrDefault(g.Key, "#94A3B8")))
            .OrderByDescending(s => s.Count)
            .ToList();

        var trend = BuildTrend(activeOrders);
        var categories = BuildCategories(fulfilledOrders);
        var productStats = BuildProductStats(fulfilledOrders);
        var bestSellers = BuildBestSellers(productStats);
        var topByUnits = BuildTopProductsByUnits(productStats);
        var topByRevenue = BuildTopProductsByRevenue(productStats);
        var topVariants = BuildTopVariants(fulfilledOrders);
        var performance = BuildProductPerformance(productStats);

        return new ReportSnapshot
        {
            RangeKey = SelectedRange,
            RangeLabel = rangeLabel,
            HasData = orders.Count > 0 || _products.All.Count > 0,
            Kpis =
            [
                new ReportKpi { Label = "Gross Sales", Value = $"₱{gross:N0}", Change = "From recorded orders", IsPositive = true },
                new ReportKpi { Label = "Net Sales", Value = $"₱{gross:N0}", Change = "From recorded orders", IsPositive = true },
                new ReportKpi { Label = "Total Orders", Value = orders.Count.ToString("N0"), Change = "In selected range", IsPositive = true },
                new ReportKpi { Label = "Items Sold", Value = items.ToString("N0"), Change = "In selected range", IsPositive = true },
                new ReportKpi { Label = "Avg. Order Value", Value = $"₱{aov:N2}", Change = "In selected range", IsPositive = true }
            ],
            SalesTrend = trend,
            OrderDistribution = distribution,
            RevenueByCategory = categories,
            BestSellers = bestSellers,
            TopSellingProducts = topByUnits,
            TopSellingVariants = topVariants,
            TopProductsByRevenue = topByRevenue,
            ProductPerformance = performance
        };
    }

    public string BuildCsv(ReportSnapshot snapshot)
    {
        var lines = new List<string>
        {
            "NU Bulldogs Exchange Report",
            $"Range,{Escape(snapshot.RangeLabel)}",
            $"Generated,{DateTime.Now:yyyy-MM-dd HH:mm}",
            "",
            "Metric,Value,Change"
        };

        foreach (var kpi in snapshot.Kpis)
            lines.Add($"{Escape(kpi.Label)},{Escape(kpi.Value)},{Escape(kpi.Change)}");

        lines.Add("");
        lines.Add("Top Selling Products");
        lines.Add("Rank,Product,Units Sold,Revenue");
        foreach (var row in snapshot.TopSellingProducts)
            lines.Add($"{row.Rank},{Escape(row.Name)},{row.UnitsSold},{row.Revenue:0.00}");

        lines.Add("");
        lines.Add("Top Selling Variants");
        lines.Add("Rank,Product,Variant,Units Sold,Revenue");
        foreach (var row in snapshot.TopSellingVariants)
            lines.Add($"{row.Rank},{Escape(row.ProductName)},{Escape(row.VariantLabel)},{row.UnitsSold},{row.Revenue:0.00}");

        lines.Add("");
        lines.Add("Top Products by Revenue");
        lines.Add("Rank,Product,Revenue,Units Sold");
        foreach (var row in snapshot.TopProductsByRevenue)
            lines.Add($"{row.Rank},{Escape(row.Name)},{row.Revenue:0.00},{row.UnitsSold}");

        lines.Add("");
        lines.Add("Product Performance");
        lines.Add("Rank,Product,Category,SKU,Units Sold,Revenue,Avg Price,Orders,Current Stock,Performance");
        foreach (var row in snapshot.ProductPerformance)
        {
            lines.Add(
                $"{row.Rank},{Escape(row.Name)},{Escape(row.Category)},{Escape(row.Sku)}," +
                $"{row.UnitsSold},{row.Revenue:0.00},{row.AvgPrice:0.00},{row.Orders},{row.CurrentStock},{Escape(row.Performance)}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Successfully fulfilled sale: Campus Pickup → Completed; Delivery → Delivered.
    /// Aligns with <see cref="OrderFlow.CanWriteReview"/>.
    /// </summary>
    public static bool IsFulfilledSale(AdminOrder order) =>
        OrderFlow.CanWriteReview(order.Fulfillment, order.Status);

    private (DateTime Start, DateTime End) ResolveRange()
    {
        var today = DateTime.Today;
        return SelectedRange switch
        {
            "today" => (today, today),
            "7days" => (today.AddDays(-6), today),
            "year" => (new DateTime(today.Year, 1, 1), today),
            "custom" when CustomStart is not null && CustomEnd is not null => (CustomStart.Value, CustomEnd.Value),
            _ => (today.AddDays(-29), today)
        };
    }

    private string FormatRangeLabel(DateTime start, DateTime end) => SelectedRange switch
    {
        "today" => "Today",
        "7days" => "Last 7 Days",
        "30days" => "Last 30 Days",
        "year" => "This Year",
        "custom" => start.Date == end.Date
            ? start.ToString("MMM d, yyyy")
            : $"{start:MMM d, yyyy} – {end:MMM d, yyyy}",
        _ => "Selected Period"
    };

    private static List<ReportMonthPoint> BuildTrend(List<AdminOrder> orders)
    {
        return Enumerable.Range(0, 8)
            .Select(i =>
            {
                var month = DateTime.Today.AddMonths(i - 7);
                var monthOrders = orders
                    .Where(o => o.Date.Year == month.Year && o.Date.Month == month.Month)
                    .ToList();
                var revenue = monthOrders.Sum(o => o.Total);
                var count = monthOrders.Count;
                return new ReportMonthPoint
                {
                    Month = month.ToString("MMM"),
                    Revenue = revenue,
                    Orders = count,
                    AvgOrderValue = count == 0 ? 0 : Math.Round(revenue / count, 0)
                };
            })
            .ToList();
    }

    private List<ReportCategoryBar> BuildCategories(List<AdminOrder> orders)
    {
        var groups = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var order in orders)
        {
            foreach (var item in order.Items)
            {
                var product = _products.GetById(item.ProductId);
                var bucket = MapCategory(product?.Category ?? "Other");
                // Use saved order-item price snapshot (not live product price).
                groups[bucket] = groups.GetValueOrDefault(bucket) + item.Price * item.Quantity;
            }
        }

        return groups
            .Select(kv => new ReportCategoryBar { Category = kv.Key, Revenue = Math.Round(kv.Value, 0) })
            .OrderByDescending(c => c.Revenue)
            .ToList();
    }

    private sealed class ProductSaleAgg
    {
        public int ProductId { get; init; }
        public int Units { get; set; }
        public decimal Revenue { get; set; }
        public HashSet<string> OrderIds { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<int, ProductSaleAgg> BuildProductStats(List<AdminOrder> fulfilledOrders)
    {
        var stats = new Dictionary<int, ProductSaleAgg>();

        foreach (var order in fulfilledOrders)
        {
            foreach (var item in order.Items)
            {
                if (!stats.TryGetValue(item.ProductId, out var agg))
                {
                    agg = new ProductSaleAgg { ProductId = item.ProductId };
                    stats[item.ProductId] = agg;
                }

                agg.Units += item.Quantity;
                agg.Revenue += item.Price * item.Quantity;
                agg.OrderIds.Add(order.Id);
            }
        }

        return stats;
    }

    private List<ReportBestSeller> BuildBestSellers(Dictionary<int, ProductSaleAgg> stats)
    {
        return stats.Values
            .OrderByDescending(s => s.Units)
            .ThenByDescending(s => s.Revenue)
            .Select((s, i) =>
            {
                var product = _products.GetById(s.ProductId);
                return new ReportBestSeller
                {
                    Rank = i + 1,
                    ProductId = s.ProductId,
                    Name = product?.Name ?? $"Product #{s.ProductId}",
                    Category = product?.Category ?? "",
                    ImageUrl = product?.ImageUrl ?? CatalogHelpers.PlaceholderImage,
                    UnitsSold = s.Units,
                    Revenue = s.Revenue,
                    StockLeft = ResolveCurrentStock(product)
                };
            })
            .Take(10)
            .ToList();
    }

    private List<ReportTopProduct> BuildTopProductsByUnits(Dictionary<int, ProductSaleAgg> stats)
    {
        var ranked = stats.Values
            .Where(s => s.Units > 0)
            .OrderByDescending(s => s.Units)
            .ThenByDescending(s => s.Revenue)
            .Take(5)
            .ToList();

        var maxUnits = ranked.Count == 0 ? 1 : Math.Max(1, ranked.Max(s => s.Units));

        return ranked.Select((s, i) =>
        {
            var product = _products.GetById(s.ProductId);
            return new ReportTopProduct
            {
                Rank = i + 1,
                ProductId = s.ProductId,
                Name = product?.Name ?? $"Product #{s.ProductId}",
                ImageUrl = product?.ImageUrl ?? CatalogHelpers.PlaceholderImage,
                UnitsSold = s.Units,
                Revenue = s.Revenue,
                ProgressPct = s.Units * 100.0 / maxUnits
            };
        }).ToList();
    }

    private List<ReportTopProduct> BuildTopProductsByRevenue(Dictionary<int, ProductSaleAgg> stats)
    {
        return stats.Values
            .Where(s => s.Revenue > 0)
            .OrderByDescending(s => s.Revenue)
            .ThenByDescending(s => s.Units)
            .Take(5)
            .Select((s, i) =>
            {
                var product = _products.GetById(s.ProductId);
                return new ReportTopProduct
                {
                    Rank = i + 1,
                    ProductId = s.ProductId,
                    Name = product?.Name ?? $"Product #{s.ProductId}",
                    ImageUrl = product?.ImageUrl ?? CatalogHelpers.PlaceholderImage,
                    UnitsSold = s.Units,
                    Revenue = s.Revenue,
                    ProgressPct = 0
                };
            })
            .ToList();
    }

    private List<ReportTopVariant> BuildTopVariants(List<AdminOrder> fulfilledOrders)
    {
        var groups = new Dictionary<string, (int ProductId, int? VariantId, string ProductName, string Label, string Image, string? Sku, int Units, decimal Revenue)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var order in fulfilledOrders)
        {
            foreach (var item in order.Items)
            {
                var label = FormatSalesVariantLabel(item.ColorName, item.Size);
                if (string.IsNullOrWhiteSpace(label))
                    continue; // Prefer only items that actually have variants.

                var key = item.VariantId is int vid
                    ? $"v:{vid}"
                    : $"p:{item.ProductId}|{NormalizePart(item.ColorName)}|{NormalizePart(item.Size)}";

                if (!groups.TryGetValue(key, out var current))
                {
                    var product = _products.GetById(item.ProductId);
                    current = (
                        item.ProductId,
                        item.VariantId,
                        product?.Name ?? item.Name,
                        label,
                        !string.IsNullOrWhiteSpace(item.ImageUrl)
                            ? item.ImageUrl
                            : product?.ImageUrl ?? CatalogHelpers.PlaceholderImage,
                        item.VariantSku,
                        0,
                        0m);
                }

                current.Units += item.Quantity;
                current.Revenue += item.Price * item.Quantity;
                if (string.IsNullOrWhiteSpace(current.Sku) && !string.IsNullOrWhiteSpace(item.VariantSku))
                    current.Sku = item.VariantSku;
                groups[key] = current;
            }
        }

        return groups.Values
            .OrderByDescending(g => g.Units)
            .ThenByDescending(g => g.Revenue)
            .Take(5)
            .Select((g, i) => new ReportTopVariant
            {
                Rank = i + 1,
                ProductId = g.ProductId,
                VariantId = g.VariantId,
                ProductName = g.ProductName,
                VariantLabel = g.Label,
                ImageUrl = g.Image,
                Sku = g.Sku,
                UnitsSold = g.Units,
                Revenue = g.Revenue
            })
            .ToList();
    }

    private List<ReportProductPerformance> BuildProductPerformance(Dictionary<int, ProductSaleAgg> stats)
    {
        var withSales = stats.Values
            .Where(s => s.Units > 0)
            .OrderByDescending(s => s.Units)
            .ThenByDescending(s => s.Revenue)
            .ToList();

        var soldCount = withSales.Count;
        var performanceByProduct = new Dictionary<int, (string Label, string Key)>();

        for (var i = 0; i < soldCount; i++)
        {
            var percentile = soldCount == 1 ? 0.0 : i / (double)(soldCount - 1);
            // Top 20% by units → Best Seller; next 30% → Fast Moving; bottom with sales → Low Sales; else Normal.
            string label;
            string key;
            if (percentile <= 0.20)
            {
                label = "Best Seller";
                key = "best";
            }
            else if (percentile <= 0.50)
            {
                label = "Fast Moving";
                key = "fast";
            }
            else if (percentile >= 0.80)
            {
                label = "Low Sales";
                key = "low";
            }
            else
            {
                label = "Normal";
                key = "normal";
            }

            performanceByProduct[withSales[i].ProductId] = (label, key);
        }

        // Single-sale edge: if only a few sellers, still assign sensible labels by rank buckets.
        if (soldCount is > 0 and <= 4)
        {
            for (var i = 0; i < soldCount; i++)
            {
                var (label, key) = i switch
                {
                    0 => ("Best Seller", "best"),
                    1 => ("Fast Moving", "fast"),
                    _ when i == soldCount - 1 && soldCount >= 3 => ("Low Sales", "low"),
                    _ => ("Normal", "normal")
                };
                performanceByProduct[withSales[i].ProductId] = (label, key);
            }
        }

        var rows = new List<ReportProductPerformance>();

        foreach (var product in _products.All.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            stats.TryGetValue(product.Id, out var agg);
            var units = agg?.Units ?? 0;
            var revenue = agg?.Revenue ?? 0m;
            var orders = agg?.OrderIds.Count ?? 0;
            var avg = units <= 0 ? 0m : Math.Round(revenue / units, 2);
            var (perfLabel, perfKey) = units <= 0
                ? ("No Sales", "none")
                : performanceByProduct.GetValueOrDefault(product.Id, ("Normal", "normal"));

            rows.Add(new ReportProductPerformance
            {
                ProductId = product.Id,
                Name = product.Name,
                Category = product.Category,
                Sku = product.Sku ?? string.Empty,
                ImageUrl = product.ImageUrl ?? CatalogHelpers.PlaceholderImage,
                UnitsSold = units,
                Revenue = revenue,
                AvgPrice = avg,
                Orders = orders,
                CurrentStock = ResolveCurrentStock(product),
                Performance = perfLabel,
                PerformanceKey = perfKey
            });
        }

        // Include orphan product IDs that sold but are no longer in catalog.
        foreach (var orphan in stats.Values.Where(s => _products.GetById(s.ProductId) is null))
        {
            var (perfLabel, perfKey) = orphan.Units <= 0
                ? ("No Sales", "none")
                : performanceByProduct.GetValueOrDefault(orphan.ProductId, ("Normal", "normal"));

            rows.Add(new ReportProductPerformance
            {
                ProductId = orphan.ProductId,
                Name = $"Product #{orphan.ProductId}",
                Category = "",
                Sku = "",
                ImageUrl = CatalogHelpers.PlaceholderImage,
                UnitsSold = orphan.Units,
                Revenue = orphan.Revenue,
                AvgPrice = orphan.Units <= 0 ? 0m : Math.Round(orphan.Revenue / orphan.Units, 2),
                Orders = orphan.OrderIds.Count,
                CurrentStock = 0,
                Performance = perfLabel,
                PerformanceKey = perfKey
            });
        }

        return rows
            .OrderByDescending(r => r.UnitsSold)
            .ThenByDescending(r => r.Revenue)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select((r, i) =>
            {
                r.Rank = i + 1;
                return r;
            })
            .ToList();
    }

    private static int ResolveCurrentStock(AdminProduct? product)
    {
        if (product is null)
            return 0;

        if (product.Variants.Count > 0)
            return product.Variants.Sum(v => Math.Max(0, v.StockQuantity));

        return Math.Max(0, product.Stock);
    }

    /// <summary>Display label: "Navy / M", "Navy", or "M". Empty when no variant.</summary>
    private static string FormatSalesVariantLabel(string? colorName, string? size)
    {
        var color = colorName?.Trim();
        var sizePart = size?.Trim();
        var hasColor = !string.IsNullOrWhiteSpace(color)
            && !color.Equals("n/a", StringComparison.OrdinalIgnoreCase)
            && color != "—";
        var hasSize = !string.IsNullOrWhiteSpace(sizePart)
            && !sizePart.Equals("Free Size", StringComparison.OrdinalIgnoreCase)
            && !sizePart.Equals("n/a", StringComparison.OrdinalIgnoreCase)
            && sizePart != "—";

        if (hasColor && hasSize)
            return $"{color} / {sizePart}";
        if (hasColor)
            return color!;
        if (hasSize)
            return sizePart!;
        return string.Empty;
    }

    private static string NormalizePart(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();

    private static string MapCategory(string category) => category switch
    {
        "T-Shirts" or "Polo Shirts" or "Hoodies" or "Jackets" => "Apparel",
        "Accessories" or "Caps" or "Tumblers" => "Accessories",
        "Bags" => "Bags",
        "School Supplies" => "Supplies",
        _ => "Other"
    };

    private static string NormalizeStatus(string status) => status;

    private static Dictionary<string, string> ColorMap() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Completed"] = "#22C55E",
        ["Delivered"] = "#22C55E",
        ["Processing"] = "#3B82F6",
        ["Pending"] = "#F9C424",
        ["Confirmed"] = "#0F766E",
        ["Ready for Pickup"] = "#A855F7",
        ["Out for Delivery"] = "#0369A1",
        ["Cancelled"] = "#EF4444"
    };

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
