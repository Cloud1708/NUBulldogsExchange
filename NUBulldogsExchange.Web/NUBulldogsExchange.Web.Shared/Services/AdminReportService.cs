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
        var (start, end) = ResolveRange();
        var orders = _orders.All
            .Where(o => o.Date.Date >= start && o.Date.Date <= end)
            .ToList();

        if (orders.Count == 0 && _products.All.Count == 0)
        {
            return new ReportSnapshot
            {
                RangeKey = SelectedRange,
                HasData = false
            };
        }

        var activeOrders = orders
            .Where(o => !o.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
            .ToList();

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
        var categories = BuildCategories(activeOrders);
        var bestSellers = BuildBestSellers(activeOrders);

        return new ReportSnapshot
        {
            RangeKey = SelectedRange,
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
                groups[bucket] = groups.GetValueOrDefault(bucket) + item.Price * item.Quantity;
            }
        }

        return groups
            .Select(kv => new ReportCategoryBar { Category = kv.Key, Revenue = Math.Round(kv.Value, 0) })
            .OrderByDescending(c => c.Revenue)
            .ToList();
    }

    private List<ReportBestSeller> BuildBestSellers(List<AdminOrder> orders)
    {
        var units = new Dictionary<int, (int Units, decimal Revenue)>();
        foreach (var order in orders)
        {
            foreach (var item in order.Items)
            {
                var current = units.GetValueOrDefault(item.ProductId);
                units[item.ProductId] = (current.Units + item.Quantity, current.Revenue + item.Price * item.Quantity);
            }
        }

        return units
            .Select(kv =>
            {
                var product = _products.GetById(kv.Key);
                return new ReportBestSeller
                {
                    ProductId = kv.Key,
                    Name = product?.Name ?? $"Product #{kv.Key}",
                    Category = product?.Category ?? "",
                    ImageUrl = product?.ImageUrl ?? CatalogHelpers.PlaceholderImage,
                    UnitsSold = kv.Value.Units,
                    Revenue = kv.Value.Revenue,
                    StockLeft = product?.Stock ?? 0
                };
            })
            .OrderByDescending(r => r.UnitsSold)
            .Select((r, i) =>
            {
                r.Rank = i + 1;
                return r;
            })
            .Take(10)
            .ToList();
    }

    private static string MapCategory(string category) => category switch
    {
        "T-Shirts" or "Polo Shirts" or "Hoodies" or "Jackets" => "Apparel",
        "Accessories" or "Caps" or "Tumblers" => "Accessories",
        "Bags" => "Bags",
        "School Supplies" => "Supplies",
        _ => "Other"
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
