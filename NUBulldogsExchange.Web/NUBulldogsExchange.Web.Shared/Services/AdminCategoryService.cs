using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminCategoryService
{
    private readonly IAppDatabase _db;
    private readonly List<AdminCategory> _categories = [];
    private int _nextId = 1;
    private bool _loaded;

    public event Action? OnChange;

    public AdminCategoryService(IAppDatabase db)
    {
        _db = db;
    }

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _categories.Clear();
        _categories.AddRange(await _db.GetCategoriesAsync());
        _nextId = _categories
            .Select(c => int.TryParse(c.Id.Replace("cat-", "", StringComparison.OrdinalIgnoreCase), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        _loaded = true;
        OnChange?.Invoke();
    }

    public IReadOnlyList<AdminCategory> All => _categories;

    public int TotalCount => _categories.Count;
    public int ActiveCount => _categories.Count(c => c.IsActive);
    public int InactiveCount => _categories.Count(c => !c.IsActive);
    public int TotalProducts => _categories.Sum(c => c.ProductCount);

    public AdminCategory? GetById(string id) =>
        _categories.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Case-insensitive name and slug check against loaded categories.
    /// Returns an error message, or null when the values are free.
    /// </summary>
    public string? FindDuplicate(string? name, string? slug, string? excludeId)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length > 0)
        {
            var nameHit = _categories.FirstOrDefault(c =>
                !SameId(c.Id, excludeId) &&
                c.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
            if (nameHit is not null)
                return $"A category named \"{nameHit.Name}\" already exists.";
        }

        var normalizedSlug = string.IsNullOrWhiteSpace(slug) ? AdminCategory.ToSlug(trimmed) : slug.Trim();
        if (normalizedSlug.Length > 0)
        {
            var slugHit = _categories.FirstOrDefault(c =>
                !SameId(c.Id, excludeId) &&
                c.Slug.Equals(normalizedSlug, StringComparison.OrdinalIgnoreCase));
            if (slugHit is not null)
                return $"The slug \"{normalizedSlug}\" is already used by \"{slugHit.Name}\".";
        }

        return null;
    }

    private static bool SameId(string id, string? excludeId) =>
        !string.IsNullOrWhiteSpace(excludeId) &&
        id.Equals(excludeId, StringComparison.OrdinalIgnoreCase);

    public async Task<AdminCategory> AddAsync(AdminCategory category)
    {
        category.Name = category.Name.Trim();
        category.Slug = string.IsNullOrWhiteSpace(category.Slug)
            ? AdminCategory.ToSlug(category.Name)
            : AdminCategory.ToSlug(category.Slug);
        var duplicate = FindDuplicate(category.Name, category.Slug, null);
        if (duplicate is not null)
            throw new InvalidOperationException(duplicate);
        category.Id = $"cat-{_nextId:000}";
        _nextId++;
        category.Status = string.IsNullOrWhiteSpace(category.Status) ? "Active" : category.Status;
        category.ImageUrl = string.IsNullOrWhiteSpace(category.ImageUrl)
            ? CatalogHelpers.PlaceholderImage
            : category.ImageUrl.Trim();
        category.Description ??= string.Empty;
        category.ProductCount = Math.Max(0, category.ProductCount);

        await _db.UpsertCategoryAsync(category);
        _categories.Add(category);
        OnChange?.Invoke();
        return category;
    }

    public async Task<bool> UpdateAsync(AdminCategory category)
    {
        var existing = GetById(category.Id);
        if (existing is null) return false;

        var name = category.Name.Trim();
        var slug = string.IsNullOrWhiteSpace(category.Slug)
            ? AdminCategory.ToSlug(name)
            : AdminCategory.ToSlug(category.Slug);
        var duplicate = FindDuplicate(name, slug, existing.Id);
        if (duplicate is not null)
            throw new InvalidOperationException(duplicate);

        existing.Name = name;
        existing.Slug = slug;
        existing.ImageUrl = string.IsNullOrWhiteSpace(category.ImageUrl)
            ? CatalogHelpers.PlaceholderImage
            : category.ImageUrl.Trim();
        existing.Description = category.Description?.Trim() ?? string.Empty;
        existing.Status = string.IsNullOrWhiteSpace(category.Status) ? "Active" : category.Status.Trim();
        await _db.UpsertCategoryAsync(existing);
        OnChange?.Invoke();
        return true;
    }

    public async Task<bool> SetStatusAsync(string id, string status)
    {
        var category = GetById(id);
        if (category is null) return false;
        category.Status = status;
        await _db.UpsertCategoryAsync(category);
        OnChange?.Invoke();
        return true;
    }

    /// <summary>
    /// Deletes a category that contains no products.
    /// <paramref name="liveProductCount"/> is the actual catalog count; the stored
    /// product_count column is only a fallback and can drift.
    /// </summary>
    public async Task<(bool Success, string Message)> TryDeleteAsync(string id, int? liveProductCount = null)
    {
        var category = GetById(id);
        if (category is null)
            return (false, "Category not found.");

        var count = Math.Max(0, liveProductCount ?? category.ProductCount);
        if (count > 0)
        {
            var noun = count == 1 ? "product" : "products";
            return (false, $"Cannot delete this category because it contains {count} {noun}. Move or remove the {noun} first.");
        }

        if (!await _db.DeleteCategoryAsync(id))
            return (false, "Unable to delete this category.");

        _categories.Remove(category);
        OnChange?.Invoke();
        return (true, "Category deleted.");
    }

    public async Task AdjustProductCountAsync(string categoryName, int delta)
    {
        var category = _categories.FirstOrDefault(c =>
            c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
        if (category is null) return;
        category.ProductCount = Math.Max(0, category.ProductCount + delta);
        await _db.UpsertCategoryAsync(category);
        OnChange?.Invoke();
    }
}
