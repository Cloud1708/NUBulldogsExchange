using System.Text.RegularExpressions;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminPromotionService
{
    public static readonly string[] DiscountTypes = ["percentage", "fixed"];
    public static readonly string[] ManualStatuses = ["Active", "Inactive"];
    public static readonly string[] StatusFilters = ["All Status", "Active", "Scheduled", "Inactive", "Expired"];

    public static readonly (string Key, string Label)[] DiscountTypeFilters =
    [
        ("all", "All Discount Types"),
        ("percentage", "Percentage"),
        ("fixed", "Fixed Amount")
    ];

    public static readonly (string Key, string Label)[] SortOptions =
    [
        ("newest", "Sort: Newest First"),
        ("oldest", "Sort: Oldest First"),
        ("name-asc", "Sort: Name A-Z"),
        ("most-used", "Sort: Most Used"),
        ("least-used", "Sort: Least Used"),
        ("ending-soon", "Sort: Ending Soon")
    ];

    public const int MaxCodeLength = 30;

    private static readonly Regex CodeRegex = new("^[A-Z0-9-]+$", RegexOptions.Compiled);

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
            .Select(SequenceNumber)
            .DefaultIfEmpty(0)
            .Max() + 1;
        _loaded = true;
        OnChange?.Invoke();
    }

    public IReadOnlyList<AdminPromotion> All => _promotions;

    public int TotalPromos => _promotions.Count;
    public int ActiveCount => _promotions.Count(p => p.Status == "Active");
    public int ScheduledCount => _promotions.Count(p => p.Status == "Scheduled");
    public int ExpiredCount => _promotions.Count(p => p.Status == "Expired");
    public int TotalUses => _promotions.Sum(p => Math.Max(0, p.UsedCount));

    public AdminPromotion? GetById(string id) =>
        _promotions.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public AdminPromotion? GetByCode(string code) =>
        _promotions.FirstOrDefault(p => p.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase));

    public bool IsCodeTaken(string? code, string? excludeId)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        var existing = GetByCode(code);
        return existing is not null &&
               (excludeId is null || !existing.Id.Equals(excludeId, StringComparison.OrdinalIgnoreCase));
    }

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

    public async Task<(bool Success, string Message)> CreateAsync(AdminPromotion input)
    {
        Normalize(input);
        var error = Validate(input, excludeId: null);
        if (error is not null)
            return (false, error);

        input.Id = $"PROMO-{_nextId++:D3}";
        input.UsedCount = 0;

        try
        {
            var saved = await _db.UpsertPromotionAsync(input);
            _promotions.Insert(0, saved);
            OnChange?.Invoke();
            return (true, "Promotion created successfully.");
        }
        catch (Exception ex)
        {
            return (false, CleanError(ex.Message, "Unable to create the promotion."));
        }
    }

    public async Task<(bool Success, string Message)> UpdateAsync(string id, AdminPromotion input)
    {
        var existing = GetById(id);
        if (existing is null)
            return (false, "Promotion not found.");

        Normalize(input);

        if (existing.UsedCount > 0 &&
            !existing.Code.Equals(input.Code, StringComparison.OrdinalIgnoreCase))
            return (false, "Promo code cannot be changed after the promotion has been used.");

        var error = Validate(input, excludeId: id);
        if (error is not null)
            return (false, error);

        var updated = existing.Clone();
        updated.Name = input.Name;
        updated.Code = input.Code;
        updated.DiscountType = input.DiscountType;
        updated.DiscountValue = input.DiscountValue;
        updated.MinimumOrder = input.MinimumOrder;
        updated.MaximumDiscount = input.MaximumDiscount;
        updated.UsageLimit = input.UsageLimit;
        updated.UsagePerCustomer = input.UsagePerCustomer;
        updated.StartDate = input.StartDate;
        updated.EndDate = input.EndDate;
        updated.Enabled = input.Enabled;
        updated.Description = input.Description;
        updated.ProductIds = [.. input.ProductIds];
        updated.CategoryIds = [.. input.CategoryIds];

        try
        {
            var saved = await _db.UpsertPromotionAsync(updated);
            Replace(existing, saved);
            return (true, "Promotion updated successfully.");
        }
        catch (Exception ex)
        {
            return (false, CleanError(ex.Message, "Unable to update the promotion."));
        }
    }

    public async Task<(bool Success, string Message)> SetEnabledAsync(string id, bool enabled)
    {
        var existing = GetById(id);
        if (existing is null)
            return (false, "Promotion not found.");

        if (existing.Enabled == enabled)
            return (true, enabled ? "Promotion is already active." : "Promotion is already inactive.");

        var updated = existing.Clone();
        updated.Enabled = enabled;

        try
        {
            var saved = await _db.UpsertPromotionAsync(updated);
            Replace(existing, saved);
            return (true, enabled ? "Promotion activated." : "Promotion deactivated.");
        }
        catch (Exception ex)
        {
            return (false, CleanError(ex.Message, "Unable to update the promotion status."));
        }
    }

    public async Task<(bool Success, string Message)> DuplicateAsync(string id)
    {
        var source = GetById(id);
        if (source is null)
            return (false, "Promotion not found.");

        var copy = source.Clone();
        copy.Id = string.Empty;
        copy.Name = $"{source.Name} (Copy)";
        copy.Code = GenerateCode(source.Name, source.DiscountValue);
        copy.UsedCount = 0;
        copy.Enabled = false;

        if (copy.EndDate.Date < DateTime.Today)
        {
            var length = (source.EndDate.Date - source.StartDate.Date).Days;
            copy.StartDate = DateTime.Today;
            copy.EndDate = DateTime.Today.AddDays(Math.Max(1, length));
        }

        var (success, message) = await CreateAsync(copy);
        return success
            ? (true, $"Promotion duplicated as {copy.Code} (Inactive).")
            : (false, message);
    }

    /// <summary>
    /// Used promotions are deactivated, never hard-deleted, so order history keeps its promo reference.
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteAsync(string id)
    {
        var existing = GetById(id);
        if (existing is null)
            return (false, "Promotion not found.");

        if (existing.UsedCount > 0)
        {
            var (ok, msg) = await SetEnabledAsync(id, false);
            return ok
                ? (true, "Promotion was deactivated because it already has usage history.")
                : (false, msg);
        }

        try
        {
            if (!await _db.DeletePromotionAsync(id))
                return (false, "Promotion could not be deleted. Deactivate it instead.");
        }
        catch (Exception ex)
        {
            return (false, CleanError(ex.Message, "Promotion could not be deleted. Deactivate it instead."));
        }

        _promotions.Remove(existing);
        OnChange?.Invoke();
        return (true, "Promotion deleted successfully.");
    }

    public string GenerateCode(string? name, decimal discountValue)
    {
        var letters = new string((name ?? string.Empty)
            .ToUpperInvariant()
            .Where(char.IsLetter)
            .Take(8)
            .ToArray());

        if (letters.Length < 3)
            letters = "NUDEAL";

        var number = discountValue > 0 && discountValue < 1000
            ? ((int)Math.Round(discountValue)).ToString()
            : "10";

        var candidate = $"{letters}{number}";
        if (!IsCodeTaken(candidate, null))
            return candidate;

        for (var i = 2; i < 100; i++)
        {
            var next = $"{letters}{number}-{i}";
            if (!IsCodeTaken(next, null))
                return next;
        }

        return $"{letters}{Random.Shared.Next(1000, 9999)}";
    }

    public IEnumerable<AdminPromotion> FilterSort(
        string? search,
        string statusFilter,
        string typeFilter,
        string sortKey)
    {
        IEnumerable<AdminPromotion> query = _promotions;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Code.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) &&
            !statusFilter.Equals("All Status", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => p.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(typeFilter) && typeFilter != "all")
            query = query.Where(p => p.DiscountType.Equals(typeFilter, StringComparison.OrdinalIgnoreCase));

        var today = DateTime.Today;
        return sortKey switch
        {
            "oldest" => query.OrderBy(SequenceNumber).ThenBy(p => p.StartDate),
            "name-asc" => query.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase),
            "most-used" => query.OrderByDescending(p => p.UsedCount).ThenBy(p => p.Name),
            "least-used" => query.OrderBy(p => p.UsedCount).ThenBy(p => p.Name),
            "ending-soon" => query
                .OrderBy(p => p.EndDate.Date < today ? 1 : 0)
                .ThenBy(p => p.EndDate),
            _ => query.OrderByDescending(SequenceNumber).ThenByDescending(p => p.StartDate)
        };
    }

    /// <summary>Single source of truth for Admin form validation (also used live by the drawer).</summary>
    public string? Validate(AdminPromotion input, string? excludeId)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            return "Promotion name is required.";
        if (string.IsNullOrWhiteSpace(input.Code))
            return "Promo code is required.";

        var code = input.Code.Trim().ToUpperInvariant();
        if (code.Length > MaxCodeLength)
            return $"Promo code must be {MaxCodeLength} characters or fewer.";
        if (!CodeRegex.IsMatch(code))
            return "Promo code may only contain letters, numbers, and hyphens.";
        if (IsCodeTaken(code, excludeId))
            return "Promo code already exists.";

        if (!DiscountTypes.Contains(input.DiscountType))
            return "Select a discount type.";
        if (input.DiscountValue <= 0)
            return "Discount value must be greater than 0.";
        if (input.IsPercentage && input.DiscountValue > 100)
            return "Percentage discount cannot exceed 100.";
        if (input.MinimumOrder < 0)
            return "Minimum order cannot be negative.";
        if (input.MaximumDiscount is < 0)
            return "Maximum discount cannot be negative.";
        if (input.UsageLimit < 1)
            return "Total usage limit must be at least 1.";
        if (input.UsagePerCustomer < 1)
            return "Usage per customer must be at least 1.";
        if (input.UsagePerCustomer > input.UsageLimit)
            return "Usage per customer cannot exceed the total usage limit.";
        if (input.StartDate == default || input.EndDate == default)
            return "Start and end dates are required.";
        if (input.EndDate.Date < input.StartDate.Date)
            return "End date must be on or after the start date.";

        return null;
    }

    private static void Normalize(AdminPromotion input)
    {
        input.Name = input.Name?.Trim() ?? string.Empty;
        input.Code = input.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        input.DiscountType = (input.DiscountType ?? "percentage").Trim().ToLowerInvariant();
        input.Description = input.Description?.Trim() ?? string.Empty;
        input.StartDate = input.StartDate.Date;
        input.EndDate = input.EndDate.Date;
        if (input.IsFixed || input.MaximumDiscount is not > 0)
            input.MaximumDiscount = null;
        input.ProductIds = input.ProductIds.Distinct().ToList();
        input.CategoryIds = input.CategoryIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void Replace(AdminPromotion existing, AdminPromotion saved)
    {
        var index = _promotions.IndexOf(existing);
        if (index >= 0)
            _promotions[index] = saved;
        else
            _promotions.Insert(0, saved);
        OnChange?.Invoke();
    }

    private static int SequenceNumber(AdminPromotion p) =>
        int.TryParse(p.Id.Replace("PROMO-", "", StringComparison.OrdinalIgnoreCase), out var n) ? n : 0;

    private static string CleanError(string? message, string fallback)
    {
        if (string.IsNullOrWhiteSpace(message))
            return fallback;
        if (message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("unique", StringComparison.OrdinalIgnoreCase))
            return "Promo code already exists.";
        if (message.Contains("foreign key", StringComparison.OrdinalIgnoreCase))
            return "Promotion is referenced by orders. Deactivate it instead.";
        return message.Length > 220 ? message[..220] + "…" : message;
    }
}
