using System.Text.RegularExpressions;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminStaffService
{
    public static readonly string[] Roles = ["Admin", "Staff"];
    public static readonly string[] Statuses = ["Active", "Inactive"];

    public static readonly (string Key, string Label)[] PermissionOptions =
    [
        ("products", "Manage Products"),
        ("categories", "Manage Categories"),
        ("orders", "Manage Orders"),
        ("inventory", "Manage Inventory"),
        ("customers", "View Customers"),
        ("promotions", "Manage Promotions"),
        ("reports", "View Reports")
    ];

    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly List<AdminStaffMember> _staff;
    private int _nextId = 5;

    public event Action? OnChange;

    public AdminStaffService()
    {
        _staff =
        [
            new()
            {
                Id = "S-001",
                FirstName = "Admin",
                LastName = "User",
                Email = "admin@nu.edu",
                Role = "Admin",
                Status = "Active",
                LastLogin = new DateTime(2026, 8, 12, 9, 14, 0),
                IsPrimaryAdmin = true,
                Permissions = AdminStaffPermissions.FullAccess()
            },
            new()
            {
                Id = "S-002",
                FirstName = "Maria",
                LastName = "Reyes",
                Email = "maria.reyes@nu.edu",
                Role = "Staff",
                Status = "Active",
                LastLogin = new DateTime(2026, 8, 11, 15, 22, 0),
                Permissions = AdminStaffPermissions.DefaultStaff()
            },
            new()
            {
                Id = "S-003",
                FirstName = "Juan",
                LastName = "Santos",
                Email = "juan.santos@nu.edu",
                Role = "Staff",
                Status = "Active",
                LastLogin = new DateTime(2026, 8, 10, 11, 5, 0),
                Permissions = new AdminStaffPermissions
                {
                    ManageProducts = true,
                    ManageCategories = false,
                    ManageOrders = true,
                    ManageInventory = true,
                    ViewCustomers = false,
                    ManagePromotions = false,
                    ViewReports = false
                }
            },
            new()
            {
                Id = "S-004",
                FirstName = "Ana",
                LastName = "Lim",
                Email = "ana.lim@nu.edu",
                Role = "Staff",
                Status = "Inactive",
                LastLogin = new DateTime(2026, 7, 28, 14, 0, 0),
                Permissions = AdminStaffPermissions.DefaultStaff()
            }
        ];
    }

    public IReadOnlyList<AdminStaffMember> All => _staff;

    public int TotalStaff => _staff.Count;
    public int AdminCount => _staff.Count(s => s.IsAdmin);
    public int ActiveCount => _staff.Count(s => s.IsActive);

    public AdminStaffMember? GetById(string id) =>
        _staff.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public AdminStaffMember? GetByEmail(string email) =>
        _staff.FirstOrDefault(s => s.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase));

    public bool HasPermission(string email, string permissionKey)
    {
        var member = GetByEmail(email);
        if (member is null) return false;
        if (!member.IsActive) return false;
        if (member.IsAdmin) return true;
        return GetPermission(member.Permissions, permissionKey);
    }

    public (bool Success, string Message) Add(
        string firstName,
        string lastName,
        string email,
        string role,
        string status,
        AdminStaffPermissions? permissions = null)
    {
        firstName = firstName.Trim();
        lastName = lastName.Trim();
        email = email.Trim();

        if (string.IsNullOrWhiteSpace(firstName))
            return (false, "First name is required.");
        if (string.IsNullOrWhiteSpace(lastName))
            return (false, "Last name is required.");
        if (string.IsNullOrWhiteSpace(email) || !EmailRegex.IsMatch(email))
            return (false, "Enter a valid email address.");
        if (GetByEmail(email) is not null)
            return (false, "A staff member with this email already exists.");
        if (string.IsNullOrWhiteSpace(role) || !Roles.Contains(role))
            return (false, "Select a role.");
        if (string.IsNullOrWhiteSpace(status) || !Statuses.Contains(status))
            status = "Active";

        var isAdmin = role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        _staff.Add(new AdminStaffMember
        {
            Id = $"S-{_nextId++:D3}",
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Role = isAdmin ? "Admin" : "Staff",
            Status = status,
            LastLogin = DateTime.Now,
            Permissions = isAdmin
                ? AdminStaffPermissions.FullAccess()
                : (permissions ?? AdminStaffPermissions.DefaultStaff()).Clone()
        });

        OnChange?.Invoke();
        return (true, "Staff member added successfully.");
    }

    public (bool Success, string Message) Update(
        string id,
        string firstName,
        string lastName,
        string email,
        string role,
        string status)
    {
        var member = GetById(id);
        if (member is null)
            return (false, "Staff member not found.");

        firstName = firstName.Trim();
        lastName = lastName.Trim();
        email = email.Trim();

        if (string.IsNullOrWhiteSpace(firstName))
            return (false, "First name is required.");
        if (string.IsNullOrWhiteSpace(lastName))
            return (false, "Last name is required.");
        if (string.IsNullOrWhiteSpace(email) || !EmailRegex.IsMatch(email))
            return (false, "Enter a valid email address.");

        var duplicate = GetByEmail(email);
        if (duplicate is not null && !duplicate.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            return (false, "A staff member with this email already exists.");

        if (member.IsPrimaryAdmin)
        {
            member.FirstName = firstName;
            member.LastName = lastName;
            // Preserve primary admin email, role, and active status.
            member.Role = "Admin";
            member.Status = "Active";
            member.Permissions = AdminStaffPermissions.FullAccess();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(role) || !Roles.Contains(role))
                return (false, "Select a role.");
            if (string.IsNullOrWhiteSpace(status) || !Statuses.Contains(status))
                return (false, "Select a status.");

            member.FirstName = firstName;
            member.LastName = lastName;
            member.Email = email;
            member.Role = role;
            member.Status = status;
            if (member.IsAdmin)
                member.Permissions = AdminStaffPermissions.FullAccess();
        }

        OnChange?.Invoke();
        return (true, "Staff information updated successfully.");
    }

    public (bool Success, string Message) UpdatePermissions(string id, AdminStaffPermissions permissions)
    {
        var member = GetById(id);
        if (member is null)
            return (false, "Staff member not found.");

        if (member.IsPrimaryAdmin || member.IsAdmin)
        {
            member.Permissions = AdminStaffPermissions.FullAccess();
            OnChange?.Invoke();
            return (true, "Admin accounts always have full access.");
        }

        member.Permissions = permissions.Clone();
        OnChange?.Invoke();
        return (true, "Permissions updated successfully.");
    }

    public (bool Success, string Message) Delete(string id, string? currentUserEmail)
    {
        var member = GetById(id);
        if (member is null)
            return (false, "Staff member not found.");

        if (member.IsPrimaryAdmin)
            return (false, "The primary admin account cannot be deleted.");

        if (!string.IsNullOrWhiteSpace(currentUserEmail) &&
            member.Email.Equals(currentUserEmail.Trim(), StringComparison.OrdinalIgnoreCase))
            return (false, "You cannot delete your own account.");

        _staff.Remove(member);
        OnChange?.Invoke();
        return (true, "Staff member removed successfully.");
    }

    public static bool GetPermission(AdminStaffPermissions permissions, string key) =>
        key switch
        {
            "products" => permissions.ManageProducts,
            "categories" => permissions.ManageCategories,
            "orders" => permissions.ManageOrders,
            "inventory" => permissions.ManageInventory,
            "customers" => permissions.ViewCustomers,
            "promotions" => permissions.ManagePromotions,
            "reports" => permissions.ViewReports,
            _ => false
        };

    public static void SetPermission(AdminStaffPermissions permissions, string key, bool value)
    {
        switch (key)
        {
            case "products": permissions.ManageProducts = value; break;
            case "categories": permissions.ManageCategories = value; break;
            case "orders": permissions.ManageOrders = value; break;
            case "inventory": permissions.ManageInventory = value; break;
            case "customers": permissions.ViewCustomers = value; break;
            case "promotions": permissions.ManagePromotions = value; break;
            case "reports": permissions.ViewReports = value; break;
        }
    }
}
