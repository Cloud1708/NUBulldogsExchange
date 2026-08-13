using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminPromotionService
{
    public static readonly string[] DiscountTypes = ["percentage", "fixed"];
    public static readonly string[] ManualStatuses = ["Active", "Inactive"];

    private readonly List<AdminPromotion> _promotions;
    private int _nextId = 5;

    public event Action? OnChange;

    public AdminPromotionService()
    {
        _promotions =
        [
            new()
            {
                Id = "PROMO-001",
                Name = "Welcome Discount",
                Code = "BULLDOG10",
                DiscountType = "percentage",
                DiscountValue = 10,
                MinimumOrder = 500,
                UsedCount = 142,
                UsageLimit = 500,
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31),
                Enabled = true,
                Description = "10% off for Bulldogs welcome orders."
            },
            new()
            {
                Id = "PROMO-002",
                Name = "Summer Sale",
                Code = "SUMMER50",
                DiscountType = "fixed",
                DiscountValue = 50,
                MinimumOrder = 300,
                UsedCount = 198,
                UsageLimit = 200,
                StartDate = new DateTime(2026, 6, 1),
                EndDate = new DateTime(2026, 8, 31),
                Enabled = true,
                Description = "₱50 off summer campus essentials."
            },
            new()
            {
                Id = "PROMO-003",
                Name = "Founding Day Special",
                Code = "NUFD2026",
                DiscountType = "percentage",
                DiscountValue = 15,
                MinimumOrder = 1000,
                UsedCount = 100,
                UsageLimit = 100,
                StartDate = new DateTime(2026, 7, 15),
                EndDate = new DateTime(2026, 7, 20),
                Enabled = true,
                Description = "Founding Day celebration promo."
            },
            new()
            {
                Id = "PROMO-004",
                Name = "Back to School",
                Code = "BTS2026",
                DiscountType = "percentage",
                DiscountValue = 20,
                MinimumOrder = 800,
                UsedCount = 0,
                UsageLimit = 300,
                StartDate = new DateTime(2026, 9, 1),
                EndDate = new DateTime(2026, 9, 30),
                Enabled = false,
                Description = "Back to school savings for merch."
            }
        ];
    }

    public IReadOnlyList<AdminPromotion> All => _promotions;

    public int TotalPromos => _promotions.Count;
    public int ActiveCount => _promotions.Count(p => p.Status == "Active");
    public int ExpiredCount => _promotions.Count(p => p.Status == "Expired");
    public int TotalUses => _promotions.Sum(p => p.UsedCount);

    public AdminPromotion? GetById(string id) =>
        _promotions.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public AdminPromotion? GetByCode(string code) =>
        _promotions.FirstOrDefault(p => p.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase));

    public (bool Success, string Message, AdminPromotion? Promo, decimal Discount) ValidateForCart(
        string? code,
        decimal cartSubtotal)
    {
        if (string.IsNullOrWhiteSpace(code))
            return (false, "Invalid promo code.", null, 0);

        var promo = GetByCode(code);
        if (promo is null)
            return (false, "Invalid promo code.", null, 0);

        if (!promo.Enabled || promo.Status == "Inactive")
            return (false, "Promo code is inactive.", promo, 0);

        if (promo.Status == "Expired")
            return (false, "Promo code has expired.", promo, 0);

        if (promo.UsedCount >= promo.UsageLimit)
            return (false, "Promo usage limit reached.", promo, 0);

        if (cartSubtotal < promo.MinimumOrder)
            return (false, "Minimum order requirement not met.", promo, 0);

        var discount = promo.CalculateDiscount(cartSubtotal);
        return (true, "Promo applied successfully.", promo, discount);
    }

    public (bool Success, string Message) Create(AdminPromotion input)
    {
        var error = ValidateInput(input, excludeId: null);
        if (error is not null)
            return (false, error);

        input.Id = $"PROMO-{_nextId++:D3}";
        input.Code = input.Code.Trim().ToUpperInvariant();
        input.Name = input.Name.Trim();
        input.UsedCount = Math.Max(0, input.UsedCount);
        _promotions.Insert(0, input);
        OnChange?.Invoke();
        return (true, "Promotion created successfully.");
    }

    public (bool Success, string Message) Update(string id, AdminPromotion input)
    {
        var existing = GetById(id);
        if (existing is null)
            return (false, "Promotion not found.");

        var error = ValidateInput(input, excludeId: id);
        if (error is not null)
            return (false, error);

        existing.Name = input.Name.Trim();
        existing.Code = input.Code.Trim().ToUpperInvariant();
        existing.DiscountType = input.DiscountType;
        existing.DiscountValue = input.DiscountValue;
        existing.MinimumOrder = input.MinimumOrder;
        existing.UsageLimit = input.UsageLimit;
        existing.StartDate = input.StartDate.Date;
        existing.EndDate = input.EndDate.Date;
        existing.Enabled = input.Enabled;
        existing.Description = input.Description?.Trim() ?? string.Empty;

        OnChange?.Invoke();
        return (true, "Promotion updated successfully.");
    }

    public (bool Success, string Message) Delete(string id)
    {
        var existing = GetById(id);
        if (existing is null)
            return (false, "Promotion not found.");

        _promotions.Remove(existing);
        OnChange?.Invoke();
        return (true, "Promotion deleted successfully.");
    }

    public bool IncrementUsage(string code)
    {
        var promo = GetByCode(code);
        if (promo is null || promo.UsedCount >= promo.UsageLimit)
            return false;

        promo.UsedCount++;
        OnChange?.Invoke();
        return true;
    }

    private string? ValidateInput(AdminPromotion input, string? excludeId)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            return "Promotion name is required.";
        if (string.IsNullOrWhiteSpace(input.Code))
            return "Promo code is required.";

        var duplicate = GetByCode(input.Code);
        if (duplicate is not null &&
            (excludeId is null || !duplicate.Id.Equals(excludeId, StringComparison.OrdinalIgnoreCase)))
            return "Promo code must be unique.";

        if (!DiscountTypes.Contains(input.DiscountType))
            return "Select a discount type.";
        if (input.DiscountValue <= 0)
            return "Discount value must be greater than 0.";
        if (input.DiscountType.Equals("percentage", StringComparison.OrdinalIgnoreCase) &&
            input.DiscountValue > 100)
            return "Percentage discount cannot exceed 100.";
        if (input.MinimumOrder < 0)
            return "Minimum order cannot be negative.";
        if (input.UsageLimit <= 0)
            return "Usage limit must be greater than 0.";
        if (input.EndDate.Date < input.StartDate.Date)
            return "End date must be on or after the start date.";

        return null;
    }
}
