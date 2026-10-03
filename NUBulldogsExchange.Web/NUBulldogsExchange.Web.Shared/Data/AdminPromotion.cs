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

    public bool IsPercentage => !IsFixed;

    public bool IsFixed => DiscountType.Equals("fixed", StringComparison.OrdinalIgnoreCase);

    public bool HasUsageLimit => UsageLimit > 0;

    public bool IsExhausted => HasUsageLimit && UsedCount >= UsageLimit;

    public string Status
    {
        get
        {
            if (!Enabled)
                return "Inactive";

            var today = DateTime.Today;
            if (today > EndDate.Date)
                return "Expired";
            if (IsExhausted)
                return "Expired";
            if (today < StartDate.Date)
                return "Scheduled";
            return "Active";
        }
    }

    public string StatusKey => Status.ToLowerInvariant();

    /// <summary>Why a promotion is not usable when the status alone is ambiguous.</summary>
    public string? StatusNote =>
        Enabled && IsExhausted && DateTime.Today <= EndDate.Date ? "Usage limit reached" : null;

    public bool IsMuted => Status is "Expired" or "Inactive" or "Scheduled";

    /// <summary>all | categories | products — derived from existing link tables.</summary>
    public string ScopeType =>
        ProductIds.Count > 0 ? "products" :
        CategoryIds.Count > 0 ? "categories" :
        "all";

    public string ScopeLabel => ScopeType switch
    {
        "products" => "Selected Products",
        "categories" => "Selected Categories",
        _ => "All Products"
    };

    public string DiscountLabel =>
        IsFixed
            ? $"₱{DiscountValue:N0}"
            : $"{DiscountValue:N0}%";

    public string DiscountHeadline => $"{DiscountLabel} OFF";

    public string DiscountTypeLabel => IsFixed ? "Fixed Amount" : "Percentage Discount";

    public bool ShowsMaxDiscount => IsPercentage;

    public string MaxDiscountLabel =>
        MaximumDiscount is > 0 ? $"₱{MaximumDiscount.Value:N0}" : "No Cap";

    public string UsedLabel =>
        UsageLimit > 0 ? $"{UsedCount}/{UsageLimit}" : $"{UsedCount}/∞";

    public string UsageSummaryLabel =>
        HasUsageLimit
            ? $"{UsedCount:N0} of {UsageLimit:N0} uses • {RemainingUses:N0} remaining"
            : $"{UsedCount:N0} uses • Unlimited";

    public int RemainingUses => HasUsageLimit ? Math.Max(0, UsageLimit - UsedCount) : 0;

    public string MinimumOrderLabel => $"₱{MinimumOrder:N0}";

    public string DateRangeLabel =>
        $"{StartDate:yyyy-MM-dd} → {EndDate:yyyy-MM-dd}";

    public string DateRangeDisplay =>
        $"{StartDate:MMM d, yyyy} → {EndDate:MMM d, yyyy}";

    public double UsagePercent =>
        UsageLimit <= 0 ? 0 : Math.Clamp(UsedCount * 100.0 / UsageLimit, 0, 100);

    public decimal CalculateDiscount(decimal eligibleSubtotal)
    {
        if (eligibleSubtotal <= 0) return 0;

        decimal discount;
        if (IsFixed)
        {
            discount = DiscountValue;
        }
        else
        {
            discount = Math.Round(eligibleSubtotal * DiscountValue / 100m, 2, MidpointRounding.AwayFromZero);
            if (MaximumDiscount is > 0)
                discount = Math.Min(discount, MaximumDiscount.Value);
        }

        return Math.Min(discount, eligibleSubtotal);
    }

    public AdminPromotion Clone() => new()
    {
        Id = Id,
        Name = Name,
        Code = Code,
        DiscountType = DiscountType,
        DiscountValue = DiscountValue,
        MinimumOrder = MinimumOrder,
        MaximumDiscount = MaximumDiscount,
        UsedCount = UsedCount,
        UsageLimit = UsageLimit,
        UsagePerCustomer = UsagePerCustomer,
        StartDate = StartDate,
        EndDate = EndDate,
        Enabled = Enabled,
        Description = Description,
        ProductIds = [.. ProductIds],
        CategoryIds = [.. CategoryIds]
    };
}
