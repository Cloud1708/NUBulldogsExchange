namespace NUBulldogsExchange.Web.Shared.Data;

public class AdminStaffPermissions
{
    public bool ManageProducts { get; set; }
    public bool ManageCategories { get; set; }
    public bool ManageOrders { get; set; }
    public bool ManageInventory { get; set; }
    public bool ViewCustomers { get; set; }
    public bool ManagePromotions { get; set; }
    public bool ViewReports { get; set; }

    public static AdminStaffPermissions FullAccess() => new()
    {
        ManageProducts = true,
        ManageCategories = true,
        ManageOrders = true,
        ManageInventory = true,
        ViewCustomers = true,
        ManagePromotions = true,
        ViewReports = true
    };

    public static AdminStaffPermissions DefaultStaff() => new()
    {
        ManageProducts = true,
        ManageCategories = true,
        ManageOrders = true,
        ManageInventory = false,
        ViewCustomers = false,
        ManagePromotions = false,
        ViewReports = false
    };

    public AdminStaffPermissions Clone() => new()
    {
        ManageProducts = ManageProducts,
        ManageCategories = ManageCategories,
        ManageOrders = ManageOrders,
        ManageInventory = ManageInventory,
        ViewCustomers = ViewCustomers,
        ManagePromotions = ManagePromotions,
        ViewReports = ViewReports
    };
}

public class AdminStaffMember
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Staff";
    public string Status { get; set; } = "Active";
    public DateTime LastLogin { get; set; }
    public bool IsPrimaryAdmin { get; set; }
    public AdminStaffPermissions Permissions { get; set; } = AdminStaffPermissions.DefaultStaff();

    public string FullName => $"{FirstName} {LastName}".Trim();

    public string Initial =>
        string.IsNullOrWhiteSpace(FullName)
            ? "?"
            : char.ToUpperInvariant(FullName.Trim()[0]).ToString();

    public bool IsAdmin => Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
    public bool IsActive => Status.Equals("Active", StringComparison.OrdinalIgnoreCase);

    public string RoleKey => IsAdmin ? "admin" : "staff";

    public string StatusKey => Status.ToLowerInvariant() switch
    {
        "active" => "active",
        "suspended" => "suspended",
        _ => "inactive"
    };

    public string LastLoginLabel => LastLogin.ToString("MMM d, yyyy h:mm tt");
}
