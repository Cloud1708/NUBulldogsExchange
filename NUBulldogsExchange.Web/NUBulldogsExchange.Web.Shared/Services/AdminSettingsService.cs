using System.Text.Json;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminSettingsService
{
    public const string DatabaseKey = "AdminPortalSettings";
    public const string SaveFailedMessage = "Unable to save settings. Please try again.";

    public static readonly (string Key, string Label, string Icon)[] Tabs =
    [
        ("store", "Store Information", "store"),
        ("branding", "Branding", "palette"),
        ("orders", "Order Settings", "shopping-bag"),
        ("inventory", "Inventory Settings", "boxes"),
        ("notifications", "Notification Settings", "bell"),
        ("security", "Security", "shield")
    ];

    /// <summary>Sections that persist values; Notifications and Security have nothing stored.</summary>
    public static readonly string[] EditableSections = ["store", "branding", "orders", "inventory"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IAppDatabase _db;
    private AdminPortalSettings _saved = new();
    private AdminPortalSettings _draft = new();
    private bool _loaded;

    public event Action? OnChange;

    public AdminSettingsService(IAppDatabase db)
    {
        _db = db;
    }

    public AdminPortalSettings Draft => _draft;
    public AdminPortalSettings Saved => _saved;
    public bool IsLoaded => _loaded;

    public bool HasUnsavedChanges => EditableSections.Any(IsSectionDirty);

    public int LowStockThreshold => Math.Max(0, _saved.Inventory.LowStockThreshold);

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        try
        {
            var loaded = Parse(await _db.GetSettingAsync(DatabaseKey));
            if (loaded is not null)
            {
                _saved = loaded;
                _draft = loaded.Clone();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }

        _loaded = true;
        OnChange?.Invoke();
    }

    public void NotifyDraftChanged() => OnChange?.Invoke();

    public bool IsSectionDirty(string section) =>
        Serialize(SectionOf(_draft, section)) != Serialize(SectionOf(_saved, section));

    public void DiscardSection(string section)
    {
        var restored = _saved.Clone();
        switch (section)
        {
            case "store": _draft.Store = restored.Store; break;
            case "branding": _draft.Branding = restored.Branding; break;
            case "orders": _draft.Orders = restored.Orders; break;
            case "inventory": _draft.Inventory = restored.Inventory; break;
        }

        OnChange?.Invoke();
    }

    /// <summary>Field-level errors for one section, keyed by field name.</summary>
    public Dictionary<string, string> ValidateSection(string section)
    {
        var errors = new Dictionary<string, string>();
        switch (section)
        {
            case "store":
            {
                var s = _draft.Store;
                if (string.IsNullOrWhiteSpace(s.Name))
                    errors["name"] = "Store name is required.";
                else if (s.Name.Trim().Length > 80)
                    errors["name"] = "Store name must be 80 characters or fewer.";
                if (!AuthValidation.IsValidEmail(s.Email))
                    errors["email"] = "Enter a valid store email.";
                if (string.IsNullOrWhiteSpace(s.Contact))
                    errors["contact"] = "Contact number is required.";
                else if (!AuthValidation.IsValidPhone(s.Contact))
                    errors["contact"] = "Enter a valid contact number.";
                if (string.IsNullOrWhiteSpace(s.Address))
                    errors["address"] = "Store address is required.";
                break;
            }
            case "branding":
            {
                var b = _draft.Branding;
                if (!AdminSettingsValidation.IsHexColor(b.PrimaryColor))
                    errors["primary"] = "Use a hex color like #123A63.";
                if (!AdminSettingsValidation.IsHexColor(b.AccentColor))
                    errors["accent"] = "Use a hex color like #F9C424.";
                if (string.IsNullOrWhiteSpace(b.Tagline))
                    errors["tagline"] = "Store tagline is required.";
                else if (b.Tagline.Trim().Length > AdminBrandingSettings.TaglineMaxLength)
                    errors["tagline"] = $"Tagline must be {AdminBrandingSettings.TaglineMaxLength} characters or fewer.";
                break;
            }
            case "orders":
            {
                var o = _draft.Orders;
                if (!o.CampusPickup && !o.Delivery)
                    errors["fulfillment"] = "At least one fulfillment method must remain enabled.";
                else if (!o.IsFulfillmentEnabled(o.DefaultFulfillment))
                    errors["default"] = "Choose an enabled fulfillment method as the default.";
                if (o.Delivery && o.DeliveryFee <= 0)
                    errors["deliveryFee"] = "Delivery fee must be greater than ₱0.";
                if (o.MinimumOrder < 0)
                    errors["minimum"] = "Minimum order amount cannot be negative.";
                if (string.IsNullOrWhiteSpace(o.ProcessingTime))
                    errors["processing"] = "Estimated processing time is required.";
                else if (o.ProcessingTime.Trim().Length > 40)
                    errors["processing"] = "Keep the processing time under 40 characters.";
                if (o.AllowCancellation &&
                    (o.CancellationHours < 1 || o.CancellationHours > AdminOrderSettings.MaxCancellationHours))
                    errors["cancelHours"] = $"Enter between 1 and {AdminOrderSettings.MaxCancellationHours} hours.";
                break;
            }
            case "inventory":
                if (_draft.Inventory.LowStockThreshold < 0)
                    errors["threshold"] = "Low stock threshold must be 0 or more.";
                break;
        }

        return errors;
    }

    /// <summary>
    /// Saves only <paramref name="section"/>. The stored JSON is re-read first so edits to
    /// other sections (here or by another admin) are not overwritten.
    /// </summary>
    public async Task<(bool Success, string Message)> SaveSectionAsync(string section)
    {
        if (!EditableSections.Contains(section))
            return (false, SaveFailedMessage);
        if (ValidateSection(section).Count > 0)
            return (false, "Please fix the highlighted fields.");

        Normalize(section);

        try
        {
            var latest = Parse(await _db.GetSettingAsync(DatabaseKey)) ?? _saved.Clone();
            var draft = _draft.Clone();
            switch (section)
            {
                case "store": latest.Store = draft.Store; break;
                case "branding": latest.Branding = draft.Branding; break;
                case "orders": latest.Orders = draft.Orders; break;
                case "inventory": latest.Inventory = draft.Inventory; break;
            }

            await _db.SetSettingAsync(DatabaseKey, Serialize(latest));

            _saved = latest;
            var refreshed = latest.Clone();
            foreach (var other in EditableSections.Where(s => s != section && IsSectionDirty(s)))
                CopySection(_draft, refreshed, other);
            _draft = refreshed;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return (false, SaveFailedMessage);
        }

        OnChange?.Invoke();
        return (true, "Settings saved successfully.");
    }

    private void Normalize(string section)
    {
        switch (section)
        {
            case "store":
                _draft.Store.Name = _draft.Store.Name.Trim();
                _draft.Store.Email = _draft.Store.Email.Trim();
                _draft.Store.Contact = _draft.Store.Contact.Trim();
                _draft.Store.Address = _draft.Store.Address.Trim();
                break;
            case "branding":
                _draft.Branding.PrimaryColor = _draft.Branding.PrimaryColor.Trim().ToUpperInvariant();
                _draft.Branding.AccentColor = _draft.Branding.AccentColor.Trim().ToUpperInvariant();
                _draft.Branding.Tagline = _draft.Branding.Tagline.Trim();
                break;
            case "orders":
                _draft.Orders.DefaultFulfillment = _draft.Orders.ResolvedDefaultFulfillment;
                _draft.Orders.ProcessingTime = _draft.Orders.ProcessingTime.Trim();
                break;
        }
    }

    private static void CopySection(AdminPortalSettings from, AdminPortalSettings to, string section)
    {
        var copy = from.Clone();
        switch (section)
        {
            case "store": to.Store = copy.Store; break;
            case "branding": to.Branding = copy.Branding; break;
            case "orders": to.Orders = copy.Orders; break;
            case "inventory": to.Inventory = copy.Inventory; break;
        }
    }

    private static object SectionOf(AdminPortalSettings settings, string section) => section switch
    {
        "store" => settings.Store,
        "branding" => settings.Branding,
        "orders" => settings.Orders,
        "inventory" => settings.Inventory,
        _ => string.Empty
    };

    private static string Serialize(object value) => JsonSerializer.Serialize(value, value.GetType(), JsonOptions);

    private static AdminPortalSettings? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<AdminPortalSettings>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
