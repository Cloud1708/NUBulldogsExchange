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

    public int CountAssigned()
    {
        var count = 0;
        if (ManageProducts) count++;
        if (ManageCategories) count++;
        if (ManageOrders) count++;
        if (ManageInventory) count++;
        if (ViewCustomers) count++;
        if (ManagePromotions) count++;
        if (ViewReports) count++;
        return count;
    }

    public IReadOnlyList<string> AssignedShortLabels()
    {
        var labels = new List<string>(7);
        if (ManageProducts) labels.Add("Products");
        if (ManageCategories) labels.Add("Categories");
        if (ManageOrders) labels.Add("Orders");
        if (ManageInventory) labels.Add("Inventory");
        if (ViewCustomers) labels.Add("Customers");
        if (ManagePromotions) labels.Add("Promotions");
        if (ViewReports) labels.Add("Reports");
        return labels;
    }

    public string AccessCountLabel
    {
        get
        {
            var count = CountAssigned();
            return count switch
            {
                0 => "No permissions",
                1 => "1 permission",
                _ => $"{count} permissions"
            };
        }
    }

    public string AccessSummaryLabel
    {
        get
        {
            var labels = AssignedShortLabels();
            if (labels.Count == 0)
                return string.Empty;
            if (labels.Count <= 3)
                return string.Join(", ", labels);

            var shown = string.Join(", ", labels.Take(3));
            return $"{shown} +{labels.Count - 3} more";
        }
    }

    public bool Matches(AdminStaffPermissions other) =>
        ManageProducts == other.ManageProducts &&
        ManageCategories == other.ManageCategories &&
        ManageOrders == other.ManageOrders &&
        ManageInventory == other.ManageInventory &&
        ViewCustomers == other.ViewCustomers &&
        ManagePromotions == other.ManagePromotions &&
        ViewReports == other.ViewReports;
}

public class AdminStaffMember
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Staff";
    public string Status { get; set; } = "Active";
    public DateTime? LastLogin { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ProfileImage { get; set; } = string.Empty;
    public bool IsPrimaryAdmin { get; set; }
    public bool MustChangePassword { get; set; }
    public AdminStaffPermissions Permissions { get; set; } = AdminStaffPermissions.DefaultStaff();

    public string FullName => $"{FirstName} {LastName}".Trim();

    public string Initial =>
        string.IsNullOrWhiteSpace(FullName)
            ? "?"
            : char.ToUpperInvariant(FullName.Trim()[0]).ToString();

    public bool HasProfileImage => !string.IsNullOrWhiteSpace(ProfileImage);

    public bool IsAdmin => Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
    public bool IsStaff => Role.Equals("Staff", StringComparison.OrdinalIgnoreCase);
    public bool IsActive => Status.Equals("Active", StringComparison.OrdinalIgnoreCase);

    public string RoleKey => IsAdmin ? "admin" : "staff";

    public string StatusKey => Status.ToLowerInvariant() switch
    {
        "active" => "active",
        "suspended" => "suspended",
        _ => "inactive"
    };

    public bool HasLastLogin => LastLogin is not null && LastLogin != DateTime.MinValue;

    public string LastLoginLabel =>
        !HasLastLogin
            ? "Never"
            : LastLogin!.Value.ToLocalTime().ToString("MMM d, yyyy h:mm tt");

    public string LastLoginDateLabel =>
        !HasLastLogin
            ? "Never"
            : LastLogin!.Value.ToLocalTime().ToString("MMM d, yyyy");

    public string LastLoginTimeLabel =>
        !HasLastLogin
            ? string.Empty
            : LastLogin!.Value.ToLocalTime().ToString("h:mm tt");

    public bool IsRecentlyActive(TimeSpan window) =>
        HasLastLogin && LastLogin!.Value.ToUniversalTime() >= DateTime.UtcNow.Subtract(window);
}
