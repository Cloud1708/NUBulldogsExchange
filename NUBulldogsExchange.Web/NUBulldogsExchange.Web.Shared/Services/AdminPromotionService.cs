using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminPromotionService
{
    public static readonly string[] DiscountTypes = ["percentage", "fixed"];
    public static readonly string[] ManualStatuses = ["Active", "Inactive"];

    private readonly IAppDatabase _db;
    private readonly List<AdminPromotion> _promotions = [];
    private int _nextId = 1;
    private bool _loaded;

    public event Action? OnChange;

    public AdminPromotionService(IAppDatabase db)
    {
        _db = db;
    }

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        _promotions.Clear();
        _promotions.AddRange(await _db.GetPromotionsAsync());
        _nextId = _promotions
            .Select(p => int.TryParse(p.Id.Replace("PROMO-", "", StringComparison.OrdinalIgnoreCase), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        _loaded = true;
        OnChange?.Invoke();
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

    public async Task<(bool Success, string Message, AdminPromotion? Promo, decimal Discount)> ValidateForCartAsync(
        string? code,
        IEnumerable<PromoCartItem> items,
        string? userEmail = null,
        int? userId = null)
    {
        var result = await _db.ValidatePromotionAsync(new PromoValidationRequest
        {
            Code = code ?? "",
            UserEmail = userEmail,
            UserId = userId,
            Items = items.ToList()
        });

        if (!result.Valid)
            return (false, result.Message, null, 0);

        var promo = GetByCode(result.Code ?? code ?? "") ?? new AdminPromotion
        {
            Id = result.PromotionId ?? "",
            Code = result.Code ?? code?.Trim().ToUpperInvariant() ?? "",
            Name = result.PromotionName ?? ""
        };

        return (true, result.Message, promo, result.DiscountAmount);
    }

    public (bool Success, string Message) Create(AdminPromotion input)
    {
        var error = ValidateInput(input, excludeId: null);
        if (error is not null)
            return (false, error);

        input.Id = $"PROMO-{_nextId++:D3}";
        input.Code = input.Code.Trim().ToUpperInvariant();
        input.Name = input.Name.Trim();
        input.UsedCount = 0;
        input.UsagePerCustomer = input.UsagePerCustomer <= 0 ? 1 : input.UsagePerCustomer;
        _db.UpsertPromotionAsync(input).GetAwaiter().GetResult();
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
        existing.MaximumDiscount = input.MaximumDiscount;
        existing.UsageLimit = input.UsageLimit;
        existing.UsagePerCustomer = input.UsagePerCustomer <= 0 ? 1 : input.UsagePerCustomer;
        existing.StartDate = input.StartDate.Date;
        existing.EndDate = input.EndDate.Date;
        existing.Enabled = input.Enabled;
        existing.Description = input.Description?.Trim() ?? string.Empty;
        existing.ProductIds = [.. input.ProductIds];
        existing.CategoryIds = [.. input.CategoryIds];

        _db.UpsertPromotionAsync(existing).GetAwaiter().GetResult();
        OnChange?.Invoke();
        return (true, "Promotion updated successfully.");
    }

    public (bool Success, string Message) Delete(string id)
    {
        var existing = GetById(id);
        if (existing is null)
            return (false, "Promotion not found.");

        _db.DeletePromotionAsync(id).GetAwaiter().GetResult();
        if (existing.UsedCount > 0)
        {
            existing.Enabled = false;
            OnChange?.Invoke();
            return (true, "Promotion was deactivated because it already has usage history.");
        }

        _promotions.Remove(existing);
        OnChange?.Invoke();
        return (true, "Promotion deleted successfully.");
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
        if (input.MaximumDiscount is < 0)
            return "Maximum discount cannot be negative.";
        if (input.UsageLimit <= 0)
            return "Usage limit must be greater than 0.";
        if (input.UsagePerCustomer < 0)
            return "Usage per customer cannot be negative.";
        if (input.EndDate.Date < input.StartDate.Date)
            return "End date must be on or after the start date.";

        return null;
    }
}
