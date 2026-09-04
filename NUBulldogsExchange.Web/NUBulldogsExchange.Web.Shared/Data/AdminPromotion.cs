namespace NUBulldogsExchange.Web.Shared.Data;

public class AdminPromotion
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "percentage"; // percentage | fixed
    public decimal DiscountValue { get; set; }
    public decimal MinimumOrder { get; set; }
    public decimal? MaximumDiscount { get; set; }
    public int UsedCount { get; set; }
    public int UsageLimit { get; set; }
    public int UsagePerCustomer { get; set; } = 1;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool Enabled { get; set; } = true;
    public string Description { get; set; } = string.Empty;
    public List<int> ProductIds { get; set; } = [];
    public List<string> CategoryIds { get; set; } = [];

    public string Status
    {
        get
        {
            if (!Enabled)
                return "Inactive";

            var today = DateTime.Today;
            if (today > EndDate.Date)
                return "Expired";
            if (today < StartDate.Date)
                return "Scheduled";
            if (today >= StartDate.Date && today <= EndDate.Date)
                return "Active";
            return "Inactive";
        }
    }

    public string StatusKey => Status.ToLowerInvariant();

    public bool IsMuted => Status is "Expired" or "Inactive" or "Scheduled";

    public string DiscountLabel =>
        DiscountType.Equals("fixed", StringComparison.OrdinalIgnoreCase)
            ? $"₱{DiscountValue:N0}"
            : $"{DiscountValue:N0}%";

    public string UsedLabel =>
        UsageLimit > 0 ? $"{UsedCount}/{UsageLimit}" : $"{UsedCount}/∞";

    public string MinimumOrderLabel => $"₱{MinimumOrder:N0}";

    public string DateRangeLabel =>
        $"{StartDate:yyyy-MM-dd} → {EndDate:yyyy-MM-dd}";

    public double UsagePercent =>
        UsageLimit <= 0 ? 0 : Math.Clamp(UsedCount * 100.0 / UsageLimit, 0, 100);

    public decimal CalculateDiscount(decimal eligibleSubtotal)
    {
        if (eligibleSubtotal <= 0) return 0;

        decimal discount;
        if (DiscountType.Equals("fixed", StringComparison.OrdinalIgnoreCase))
            discount = DiscountValue;
        else
            discount = Math.Round(eligibleSubtotal * DiscountValue / 100m, 2, MidpointRounding.AwayFromZero);

        if (MaximumDiscount is > 0)
            discount = Math.Min(discount, MaximumDiscount.Value);

        return Math.Min(discount, eligibleSubtotal);
    }
}
