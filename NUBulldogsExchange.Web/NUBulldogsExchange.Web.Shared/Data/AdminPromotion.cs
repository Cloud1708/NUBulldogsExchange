namespace NUBulldogsExchange.Web.Shared.Data;

public class AdminPromotion
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "percentage"; // percentage | fixed
    public decimal DiscountValue { get; set; }
    public decimal MinimumOrder { get; set; }
    public int UsedCount { get; set; }
    public int UsageLimit { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool Enabled { get; set; } = true;
    public string Description { get; set; } = string.Empty;

    public string Status
    {
        get
        {
            if (!Enabled)
                return "Inactive";

            var today = DateTime.Today;
            if (today > EndDate.Date)
                return "Expired";
            if (today >= StartDate.Date && today <= EndDate.Date)
                return "Active";
            return "Inactive";
        }
    }

    public string StatusKey => Status.ToLowerInvariant();

    public bool IsMuted => Status is "Expired" or "Inactive";

    public string DiscountLabel =>
        DiscountType.Equals("fixed", StringComparison.OrdinalIgnoreCase)
            ? $"₱{DiscountValue:N0}"
            : $"{DiscountValue:N0}%";

    public string UsedLabel => $"{UsedCount}/{UsageLimit}";

    public string MinimumOrderLabel => $"₱{MinimumOrder:N0}";

    public string DateRangeLabel =>
        $"{StartDate:yyyy-MM-dd} → {EndDate:yyyy-MM-dd}";

    public double UsagePercent =>
        UsageLimit <= 0 ? 0 : Math.Clamp(UsedCount * 100.0 / UsageLimit, 0, 100);

    public decimal CalculateDiscount(decimal subtotal)
    {
        if (subtotal <= 0) return 0;

        var discount = DiscountType.Equals("fixed", StringComparison.OrdinalIgnoreCase)
            ? DiscountValue
            : Math.Round(subtotal * DiscountValue / 100m, 0, MidpointRounding.AwayFromZero);

        return Math.Min(discount, subtotal);
    }
}
