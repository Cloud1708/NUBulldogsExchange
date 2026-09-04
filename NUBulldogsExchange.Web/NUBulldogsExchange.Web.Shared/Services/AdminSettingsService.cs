using System.Text.Json;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminSettingsService
{
    public const string StorageKey = "nuBulldogsAdminSettings";
    public const string DatabaseKey = "AdminPortalSettings";

    public static readonly (string Key, string Label, string Icon)[] Tabs =
    [
        ("store", "Store Information", "store"),
        ("branding", "Branding", "palette"),
        ("orders", "Order Settings", "shopping-bag"),
        ("inventory", "Inventory Settings", "boxes"),
        ("notifications", "Notification Settings", "bell"),
        ("security", "Security", "shield")
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IAppDatabase _db;
    private AdminPortalSettings _saved = new();
    private AdminPortalSettings _draft = new();

    public event Action? OnChange;

    public AdminSettingsService(IAppDatabase db)
    {
        _db = db;
    }

    public AdminPortalSettings Draft => _draft;
    public AdminPortalSettings Saved => _saved;

    public bool HasUnsavedChanges =>
        JsonSerializer.Serialize(_draft, JsonOptions) != JsonSerializer.Serialize(_saved, JsonOptions);

    public int LowStockThreshold => Math.Max(1, _saved.Inventory.LowStockThreshold);

    public void ReplaceDraft(AdminPortalSettings draft)
    {
        _draft = draft;
        OnChange?.Invoke();
    }

    public void Discard()
    {
        _draft = _saved.Clone();
        OnChange?.Invoke();
    }

    public (bool Success, string Message) Save()
    {
        if (string.IsNullOrWhiteSpace(_draft.Store.Name))
            return (false, "Store name is required.");
        if (string.IsNullOrWhiteSpace(_draft.Store.Email) || !_draft.Store.Email.Contains('@'))
            return (false, "Enter a valid store email.");
        if (_draft.Inventory.LowStockThreshold < 1)
            return (false, "Low stock threshold must be at least 1.");
        if (_draft.Security.SessionTimeout < 5)
            return (false, "Session timeout must be at least 5 minutes.");

        _saved = _draft.Clone();
        _draft = _saved.Clone();
        try
        {
            _db.SetSettingAsync(DatabaseKey, ToStorageJson()).GetAwaiter().GetResult();
        }
        catch
        {
            // Persistence failure should not block in-memory save for local demo.
        }

        OnChange?.Invoke();
        return (true, "Settings saved successfully.");
    }

    public async Task<bool> EnsureLoadedAsync()
    {
        try
        {
            var json = await _db.GetSettingAsync(DatabaseKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                LoadFromStorageJson(json);
                return true;
            }
        }
        catch
        {
            // Fall through to empty defaults / localStorage.
        }

        return false;
    }

    public string ToStorageJson() => JsonSerializer.Serialize(_saved, JsonOptions);

    public void LoadFromStorageJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            var loaded = JsonSerializer.Deserialize<AdminPortalSettings>(json, JsonOptions);
            if (loaded is null) return;
            _saved = loaded;
            _draft = loaded.Clone();
            OnChange?.Invoke();
        }
        catch
        {
            // Ignore corrupt local storage.
        }
    }

    public (bool Success, string Message) ValidatePasswordChange(
        string currentPassword,
        string newPassword,
        string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword))
            return (false, "Current password is required.");
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            return (false, "New password must be at least 8 characters.");
        if (_saved.Security.StrongPasswords &&
            !(newPassword.Any(char.IsUpper) && newPassword.Any(char.IsDigit)))
            return (false, "Password must include an uppercase letter and a number.");
        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            return (false, "New password and confirmation do not match.");

        return (true, "Password updated successfully.");
    }
}
