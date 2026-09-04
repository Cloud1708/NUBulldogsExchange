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

    public AdminCategory Add(AdminCategory category)
    {
        category.Id = $"cat-{_nextId:000}";
        _nextId++;
        category.Name = category.Name.Trim();
        category.Slug = string.IsNullOrWhiteSpace(category.Slug)
            ? AdminCategory.ToSlug(category.Name)
            : AdminCategory.ToSlug(category.Slug);
        category.Status = string.IsNullOrWhiteSpace(category.Status) ? "Active" : category.Status;
        category.ImageUrl = string.IsNullOrWhiteSpace(category.ImageUrl)
            ? CatalogHelpers.PlaceholderImage
            : category.ImageUrl.Trim();
        category.Description ??= string.Empty;
        category.ProductCount = Math.Max(0, category.ProductCount);

        _db.UpsertCategoryAsync(category).GetAwaiter().GetResult();
        _categories.Add(category);
        OnChange?.Invoke();
        return category;
    }

    public bool Update(AdminCategory category)
    {
        var existing = GetById(category.Id);
        if (existing is null) return false;

        existing.Name = category.Name.Trim();
        existing.Slug = string.IsNullOrWhiteSpace(category.Slug)
            ? AdminCategory.ToSlug(existing.Name)
            : AdminCategory.ToSlug(category.Slug);
        existing.ImageUrl = string.IsNullOrWhiteSpace(category.ImageUrl)
            ? CatalogHelpers.PlaceholderImage
            : category.ImageUrl.Trim();
        existing.Description = category.Description?.Trim() ?? string.Empty;
        existing.Status = string.IsNullOrWhiteSpace(category.Status) ? "Active" : category.Status.Trim();
        _db.UpsertCategoryAsync(existing).GetAwaiter().GetResult();
        OnChange?.Invoke();
        return true;
    }

    public bool SetStatus(string id, string status)
    {
        var category = GetById(id);
        if (category is null) return false;
        category.Status = status;
        _db.UpsertCategoryAsync(category).GetAwaiter().GetResult();
        OnChange?.Invoke();
        return true;
    }

    public (bool Success, string Message) TryDelete(string id)
    {
        var category = GetById(id);
        if (category is null)
            return (false, "Category not found.");

        if (category.ProductCount > 0)
            return (false, "Move or remove the products in this category first.");

        _db.DeleteCategoryAsync(id).GetAwaiter().GetResult();
        _categories.Remove(category);
        OnChange?.Invoke();
        return (true, "Category deleted.");
    }

    public void AdjustProductCount(string categoryName, int delta)
    {
        var category = _categories.FirstOrDefault(c =>
            c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
        if (category is null) return;
        category.ProductCount = Math.Max(0, category.ProductCount + delta);
        _db.UpsertCategoryAsync(category).GetAwaiter().GetResult();
        OnChange?.Invoke();
    }
}
