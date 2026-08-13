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

public class ReportSnapshot
{
    public string RangeKey { get; set; } = "30days";
    public List<ReportKpi> Kpis { get; set; } = [];
    public List<ReportMonthPoint> SalesTrend { get; set; } = [];
    public List<AdminStatusSlice> OrderDistribution { get; set; } = [];
    public List<ReportCategoryBar> RevenueByCategory { get; set; } = [];
    public List<ReportBestSeller> BestSellers { get; set; } = [];
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
        var scale = SelectedRange switch
        {
            "today" => 0.04m,
            "7days" => 0.22m,
            "30days" => 1m,
            "year" => 3.2m,
            "custom" => EstimateCustomScale(),
            _ => 1m
        };

        if (SelectedRange == "custom" && scale <= 0)
        {
            return new ReportSnapshot
            {
                RangeKey = SelectedRange,
                HasData = false
            };
        }

        var trend = BuildTrend(scale);
        var distribution = ScaleDistribution(scale);
        var categories = BuildCategories(scale);
        var bestSellers = BuildBestSellers(scale);
        var kpis = BuildKpis(scale, trend, distribution, bestSellers);

        return new ReportSnapshot
        {
            RangeKey = SelectedRange,
            HasData = true,
            Kpis = kpis,
            SalesTrend = trend,
            OrderDistribution = distribution,
            RevenueByCategory = categories,
            BestSellers = bestSellers
        };
    }

    public string BuildCsv(ReportSnapshot snapshot)
    {
        var lines = new List<string>
        {
            "NU Bulldogs Exchange Report",
            $"Range,{SelectedRange}",
            $"Generated,{DateTime.Now:yyyy-MM-dd HH:mm}",
            "",
            "Metric,Value,Change"
        };

        foreach (var kpi in snapshot.Kpis)
            lines.Add($"{Escape(kpi.Label)},{Escape(kpi.Value)},{Escape(kpi.Change)}");

        lines.Add("");
        lines.Add("Rank,Product,Category,Units Sold,Revenue,Stock Left");
        foreach (var row in snapshot.BestSellers)
        {
            lines.Add($"{row.Rank},{Escape(row.Name)},{Escape(row.Category)},{row.UnitsSold},{row.Revenue:0.##},{row.StockLeft}");
        }

        return string.Join("\n", lines);
    }

    private decimal EstimateCustomScale()
    {
        if (CustomStart is null || CustomEnd is null)
            return 0;

        var days = (CustomEnd.Value - CustomStart.Value).TotalDays + 1;
        if (days <= 0) return 0;
        if (days <= 1) return 0.04m;
        if (days <= 7) return 0.22m;
        if (days <= 31) return 1m;
        if (days <= 120) return 1.8m;
        return 3.2m;
    }

    private List<ReportKpi> BuildKpis(
        decimal scale,
        List<ReportMonthPoint> trend,
        List<AdminStatusSlice> distribution,
        List<ReportBestSeller> bestSellers)
    {
        var gross = Math.Round(128450m * scale, 0);
        var net = Math.Round(115205m * scale, 0);
        var orders = Math.Max(1, (int)Math.Round(387 * (double)scale));
        var items = Math.Max(1, (int)Math.Round(634 * (double)scale));
        var aov = orders == 0 ? 0 : Math.Round(net / orders, 2);

        // Prefer live order count when period is close to "current catalog" demo.
        if (SelectedRange is "30days" or "7days")
        {
            var liveOrders = _orders.All.Count;
            if (liveOrders > 0 && SelectedRange == "7days")
                orders = Math.Max(orders, liveOrders);
        }

        _ = trend;
        _ = distribution;
        _ = bestSellers;

        return
        [
            new ReportKpi { Label = "Gross Sales", Value = $"₱{gross:N0}", Change = "+12.5% vs last period", IsPositive = true },
            new ReportKpi { Label = "Net Sales", Value = $"₱{net:N0}", Change = "+11.8% vs last period", IsPositive = true },
            new ReportKpi { Label = "Total Orders", Value = orders.ToString("N0"), Change = "+8.2% vs last period", IsPositive = true },
            new ReportKpi { Label = "Items Sold", Value = items.ToString("N0"), Change = "+9.4% vs last period", IsPositive = true },
            new ReportKpi { Label = "Avg. Order Value", Value = $"₱{aov:N2}", Change = "+3.6% vs last period", IsPositive = true }
        ];
    }

    private static List<ReportMonthPoint> BuildTrend(decimal scale)
    {
        var baseData = new (string Month, decimal Revenue, int Orders, decimal Aov)[]
        {
            ("Jan", 45000, 80, 500),
            ("Feb", 52000, 95, 500),
            ("Mar", 62000, 120, 498),
            ("Apr", 48000, 90, 495),
            ("May", 75000, 145, 501),
            ("Jun", 91000, 175, 504),
            ("Jul", 96000, 185, 503),
            ("Aug", 128450, 250, 505)
        };

        return baseData.Select(d => new ReportMonthPoint
        {
            Month = d.Month,
            Revenue = Math.Round(d.Revenue * scale, 0),
            Orders = Math.Max(1, (int)Math.Round(d.Orders * (double)scale)),
            AvgOrderValue = Math.Round(d.Aov * Math.Min(scale, 1.05m), 0)
        }).ToList();
    }

    private List<AdminStatusSlice> ScaleDistribution(decimal scale)
    {
        // Prefer live admin order statuses when available; otherwise use dashboard mock mix.
        var live = _orders.All
            .GroupBy(o => NormalizeStatus(o.Status))
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .ToList();

        if (SelectedRange is "today" or "7days" && live.Count > 0)
        {
            var colors = ColorMap();
            return live
                .OrderByDescending(x => x.Count)
                .Select(x => new AdminStatusSlice(x.Label, x.Count, colors.GetValueOrDefault(x.Label, "#94A3B8")))
                .ToList();
        }

        return MockAdminData.OrderStatus
            .Select(s => new AdminStatusSlice(
                s.Label,
                Math.Max(0, (int)Math.Round(s.Count * (double)scale)),
                s.Color))
            .ToList();
    }

    private List<ReportCategoryBar> BuildCategories(decimal scale)
    {
        var groups = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Apparel"] = 52000,
            ["Accessories"] = 22000,
            ["Bags"] = 19000,
            ["Footwear"] = 14000,
            ["Supplies"] = 9000
        };

        // Blend with live product sold * price where possible.
        foreach (var product in _products.All)
        {
            var bucket = MapCategory(product.Category);
            var contrib = product.Sold * product.Price * 0.05m;
            if (groups.ContainsKey(bucket))
                groups[bucket] += contrib;
        }

        return groups
            .Select(kv => new ReportCategoryBar
            {
                Category = kv.Key,
                Revenue = Math.Round(kv.Value * scale, 0)
            })
            .OrderByDescending(c => c.Revenue)
            .ToList();
    }

    private List<ReportBestSeller> BuildBestSellers(decimal scale)
    {
        var seed = new (int Id, int Units, decimal Revenue)[]
        {
            (1, 891, 132759),
            (2, 543, 162357),
            (3, 342, 170658),
            (4, 310, 39990),
            (5, 215, 75035),
            (6, 201, 200799),
            (7, 178, 124422),
            (8, 132, 32868)
        };

        var rows = new List<ReportBestSeller>();
        foreach (var (id, units, revenue) in seed)
        {
            var product = _products.GetById(id);
            var fallback = MockData.GetById(id);
            if (product is null && fallback is null)
                continue;

            product ??= AdminProduct.FromProduct(fallback!);
            rows.Add(new ReportBestSeller
            {
                ProductId = product.Id,
                Name = product.Name,
                Category = product.Category,
                ImageUrl = product.ImageUrl,
                UnitsSold = Math.Max(1, (int)Math.Round(units * (double)scale)),
                Revenue = Math.Round(revenue * scale, 0),
                StockLeft = product.Stock
            });
        }

        return rows
            .OrderByDescending(r => r.UnitsSold)
            .Select((r, i) =>
            {
                r.Rank = i + 1;
                return r;
            })
            .ToList();
    }

    private static string MapCategory(string category) => category switch
    {
        "T-Shirts" or "Polo Shirts" or "Hoodies" or "Jackets" => "Apparel",
        "Accessories" or "Caps" or "Tumblers" => "Accessories",
        "Bags" => "Bags",
        "School Supplies" => "Supplies",
        _ => "Footwear"
    };

    private static string NormalizeStatus(string status) => status switch
    {
        "Confirmed" => "Pending",
        _ => status
    };

    private static Dictionary<string, string> ColorMap() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Completed"] = "#22C55E",
        ["Processing"] = "#3B82F6",
        ["Pending"] = "#F9C424",
        ["Ready for Pickup"] = "#A855F7",
        ["Cancelled"] = "#EF4444"
    };

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
