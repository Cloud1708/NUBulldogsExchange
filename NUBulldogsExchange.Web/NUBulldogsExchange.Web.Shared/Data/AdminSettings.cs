using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace NUBulldogsExchange.Web.Shared.Data;

public class AdminStoreSettings
{
    public string Name { get; set; } = "NU Bulldogs Exchange";
    public string Email { get; set; } = "store@nu.edu.ph";
    public string Contact { get; set; } = "+63 2 8123 4567";
    public string Address { get; set; } = "551 M.F. Jhocson St., Sampaloc, Manila";

    /// <summary>Splits the name for the two-line storefront brand ("NU Bulldogs" / "Exchange").</summary>
    [JsonIgnore]
    public (string Title, string Subtitle) BrandLines
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(Name) ? "NU Bulldogs Exchange" : Name.Trim();
            var lastSpace = name.LastIndexOf(' ');
            return lastSpace <= 0 ? (name, string.Empty) : (name[..lastSpace], name[(lastSpace + 1)..]);
        }
    }
}

public class AdminBrandingSettings
{
    public const int TaglineMaxLength = 120;
    public const string DefaultPrimary = "#123A63";
    public const string DefaultAccent = "#F9C424";

    public string PrimaryColor { get; set; } = DefaultPrimary;
    public string AccentColor { get; set; } = DefaultAccent;
    public string Tagline { get; set; } = "Official merchandise for the NU Bulldogs community.";
    /// <summary>Uploaded logo URL; empty means the default "NU" badge.</summary>
    public string LogoUrl { get; set; } = string.Empty;

    [JsonIgnore]
    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoUrl);
}

public class AdminOrderSettings
{
    public const int MaxCancellationHours = 720;

    public bool CampusPickup { get; set; } = true;
    public bool Delivery { get; set; } = true;
    public string DefaultFulfillment { get; set; } = OrderFlow.CampusPickup;
    public bool AllowCancellation { get; set; } = true;
    public int CancellationHours { get; set; } = 24;
    public decimal MinimumOrder { get; set; }
    /// <summary>
    /// Read by checkout and by save_checkout_shipping_snapshot (docs/sql/022) as orders.deliveryFee;
    /// that function treats 0 as "use ₱150", so the fee must stay positive.
    /// </summary>
    public decimal DeliveryFee { get; set; } = 150;
    public string ProcessingTime { get; set; } = "1–3 business days";

    [JsonIgnore]
    public IReadOnlyList<string> EnabledFulfillments
    {
        get
        {
            var list = new List<string>(2);
            if (CampusPickup) list.Add(OrderFlow.CampusPickup);
            if (Delivery) list.Add(OrderFlow.Delivery);
            return list;
        }
    }

    public bool IsFulfillmentEnabled(string? method) =>
        EnabledFulfillments.Contains(method ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    /// <summary>Default method for new checkouts, falling back to whichever method is enabled.</summary>
    [JsonIgnore]
    public string ResolvedDefaultFulfillment =>
        IsFulfillmentEnabled(DefaultFulfillment)
            ? EnabledFulfillments.First(m => string.Equals(m, DefaultFulfillment, StringComparison.OrdinalIgnoreCase))
            : EnabledFulfillments.FirstOrDefault() ?? OrderFlow.CampusPickup;
}

public class AdminInventorySettings
{
    public int LowStockThreshold { get; set; } = 20;
}

public class AdminPortalSettings
{
    public AdminStoreSettings Store { get; set; } = new();
    public AdminBrandingSettings Branding { get; set; } = new();
    public AdminOrderSettings Orders { get; set; } = new();
    public AdminInventorySettings Inventory { get; set; } = new();

    public AdminPortalSettings Clone() => new()
    {
        Store = new AdminStoreSettings
        {
            Name = Store.Name,
            Email = Store.Email,
            Contact = Store.Contact,
            Address = Store.Address
        },
        Branding = new AdminBrandingSettings
        {
            PrimaryColor = Branding.PrimaryColor,
            AccentColor = Branding.AccentColor,
            Tagline = Branding.Tagline,
            LogoUrl = Branding.LogoUrl
        },
        Orders = new AdminOrderSettings
        {
            CampusPickup = Orders.CampusPickup,
            Delivery = Orders.Delivery,
            DefaultFulfillment = Orders.DefaultFulfillment,
            AllowCancellation = Orders.AllowCancellation,
            CancellationHours = Orders.CancellationHours,
            MinimumOrder = Orders.MinimumOrder,
            DeliveryFee = Orders.DeliveryFee,
            ProcessingTime = Orders.ProcessingTime
        },
        Inventory = new AdminInventorySettings
        {
            LowStockThreshold = Inventory.LowStockThreshold
        }
    };
}

public static partial class AdminSettingsValidation
{
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorRegex();

    public static bool IsHexColor(string? value) =>
        !string.IsNullOrWhiteSpace(value) && HexColorRegex().IsMatch(value.Trim());

    public static string SafeColor(string? value, string fallback) =>
        IsHexColor(value) ? value!.Trim().ToUpperInvariant() : fallback;
}
