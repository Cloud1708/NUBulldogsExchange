using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public sealed class AdminSizeGuideService
{
    private readonly IAppDatabase _db;
    private readonly List<SizeGuide> _guides = [];
    private bool _loaded;

    public event Action? OnChange;

    public AdminSizeGuideService(IAppDatabase db)
    {
        _db = db;
    }

    public IReadOnlyList<SizeGuide> All => _guides;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        var rows = await _db.GetSizeGuidesAsync();
        _guides.Clear();
        _guides.AddRange(rows.Select(g => g.Clone()).OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase));
        _loaded = true;
        OnChange?.Invoke();
    }

    public SizeGuide? GetById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return _guides.FirstOrDefault(g => g.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<SizeGuide?> LoadByIdAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var cached = GetById(id);
        if (cached is not null) return cached;

        var loaded = await _db.GetSizeGuideByIdAsync(id);
        if (loaded is null) return null;
        UpsertLocal(loaded);
        return GetById(loaded.Id);
    }

    public async Task<SizeGuide> SaveAsync(SizeGuide guide)
    {
        var saved = await _db.UpsertSizeGuideAsync(guide);
        UpsertLocal(saved);
        OnChange?.Invoke();
        return saved;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        if (!await _db.DeleteSizeGuideAsync(id))
            return false;
        _guides.RemoveAll(g => g.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        OnChange?.Invoke();
        return true;
    }

    public int CountProductsUsing(string id, IEnumerable<AdminProduct> products) =>
        products.Count(p =>
            !string.IsNullOrWhiteSpace(p.SizeGuideId)
            && p.SizeGuideId.Equals(id, StringComparison.OrdinalIgnoreCase)
            && p.HasSizeVariants);

    private void UpsertLocal(SizeGuide guide)
    {
        var index = _guides.FindIndex(g => g.Id.Equals(guide.Id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            _guides[index] = guide.Clone();
        else
            _guides.Add(guide.Clone());
        _guides.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    }
}
