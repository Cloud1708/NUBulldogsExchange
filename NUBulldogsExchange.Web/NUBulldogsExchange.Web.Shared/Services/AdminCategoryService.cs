using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminCategoryService
{
    private readonly List<AdminCategory> _categories;
    private int _nextId;

    public event Action? OnChange;

    public AdminCategoryService()
    {
        _categories = MockData.Categories.Select((c, index) => new AdminCategory
        {
            Id = $"cat-{(index + 1):000}",
            Name = c.Name,
            Slug = string.IsNullOrWhiteSpace(c.Slug) ? AdminCategory.ToSlug(c.Name) : c.Slug,
            ImageUrl = c.ImageUrl,
            Description = $"NU Bulldogs {c.Name.ToLowerInvariant()} and related merchandise.",
            Status = "Active",
            ProductCount = c.Count
        }).ToList();

        _nextId = _categories.Count + 1;
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
            ? MockData.PlaceholderImage
            : category.ImageUrl.Trim();
        category.Description ??= string.Empty;
        category.ProductCount = Math.Max(0, category.ProductCount);

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
            ? MockData.PlaceholderImage
            : category.ImageUrl.Trim();
        existing.Description = category.Description?.Trim() ?? string.Empty;
        existing.Status = string.IsNullOrWhiteSpace(category.Status) ? "Active" : category.Status.Trim();
        OnChange?.Invoke();
        return true;
    }

    public bool SetStatus(string id, string status)
    {
        var category = GetById(id);
        if (category is null) return false;
        category.Status = status;
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

        _categories.Remove(category);
        OnChange?.Invoke();
        return (true, "Category deleted.");
    }

    public void AdjustProductCount(string categoryName, int delta)
    {
        if (string.IsNullOrWhiteSpace(categoryName) || delta == 0) return;

        var category = _categories.FirstOrDefault(c =>
            c.Name.Equals(categoryName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (category is null) return;

        category.ProductCount = Math.Max(0, category.ProductCount + delta);
        OnChange?.Invoke();
    }
}
