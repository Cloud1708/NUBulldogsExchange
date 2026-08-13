namespace NUBulldogsExchange.Web.Shared.Data;

public class AdminStoreSettings
{
    public string Name { get; set; } = "NU Bulldogs Exchange";
    public string Email { get; set; } = "store@nu.edu.ph";
    public string Contact { get; set; } = "+63 2 8123 4567";
    public string Address { get; set; } = "551 M.F. Jhocson St., Sampaloc, Manila";
}

public class AdminBrandingSettings
{
    public string PrimaryColor { get; set; } = "#123A63";
    public string AccentColor { get; set; } = "#F9C424";
    public string Tagline { get; set; } = "Official merchandise for the NU Bulldogs community.";
    public string LogoLabel { get; set; } = "NU Logo";
}

public class AdminOrderSettings
{
    public bool CampusPickup { get; set; } = true;
    public bool Delivery { get; set; } = true;
    public string DefaultFulfillment { get; set; } = "Campus Pickup";
    public bool AllowCancellation { get; set; } = true;
    public int CancellationHours { get; set; } = 24;
    public decimal MinimumOrder { get; set; }
    public string ProcessingTime { get; set; } = "1–3 business days";
}

public class AdminInventorySettings
{
    public int LowStockThreshold { get; set; } = 20;
    public bool AllowBackorders { get; set; }
    public bool LowStockAlerts { get; set; } = true;
    public bool OutOfStockAlerts { get; set; } = true;
    public bool ReservePendingStock { get; set; } = true;
}

public class AdminNotificationSettings
{
    public bool NewOrders { get; set; } = true;
    public bool CancelledOrders { get; set; } = true;
    public bool ReadyForPickup { get; set; } = true;
    public bool LowStock { get; set; } = true;
    public bool OutOfStock { get; set; } = true;
    public bool NewCustomers { get; set; } = true;
    public bool PromotionExpiry { get; set; } = true;
    public bool StaffActivity { get; set; }
}

public class AdminSecuritySettings
{
    public bool TwoFactor { get; set; }
    public bool LoginAlerts { get; set; } = true;
    public int SessionTimeout { get; set; } = 30;
    public bool StrongPasswords { get; set; } = true;
}

public class AdminPortalSettings
{
    public AdminStoreSettings Store { get; set; } = new();
    public AdminBrandingSettings Branding { get; set; } = new();
    public AdminOrderSettings Orders { get; set; } = new();
    public AdminInventorySettings Inventory { get; set; } = new();
    public AdminNotificationSettings Notifications { get; set; } = new();
    public AdminSecuritySettings Security { get; set; } = new();

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
            LogoLabel = Branding.LogoLabel
        },
        Orders = new AdminOrderSettings
        {
            CampusPickup = Orders.CampusPickup,
            Delivery = Orders.Delivery,
            DefaultFulfillment = Orders.DefaultFulfillment,
            AllowCancellation = Orders.AllowCancellation,
            CancellationHours = Orders.CancellationHours,
            MinimumOrder = Orders.MinimumOrder,
            ProcessingTime = Orders.ProcessingTime
        },
        Inventory = new AdminInventorySettings
        {
            LowStockThreshold = Inventory.LowStockThreshold,
            AllowBackorders = Inventory.AllowBackorders,
            LowStockAlerts = Inventory.LowStockAlerts,
            OutOfStockAlerts = Inventory.OutOfStockAlerts,
            ReservePendingStock = Inventory.ReservePendingStock
        },
        Notifications = new AdminNotificationSettings
        {
            NewOrders = Notifications.NewOrders,
            CancelledOrders = Notifications.CancelledOrders,
            ReadyForPickup = Notifications.ReadyForPickup,
            LowStock = Notifications.LowStock,
            OutOfStock = Notifications.OutOfStock,
            NewCustomers = Notifications.NewCustomers,
            PromotionExpiry = Notifications.PromotionExpiry,
            StaffActivity = Notifications.StaffActivity
        },
        Security = new AdminSecuritySettings
        {
            TwoFactor = Security.TwoFactor,
            LoginAlerts = Security.LoginAlerts,
            SessionTimeout = Security.SessionTimeout,
            StrongPasswords = Security.StrongPasswords
        }
    };
}
