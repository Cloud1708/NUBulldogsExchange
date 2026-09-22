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

    private readonly IAppDatabase _db;
    private readonly List<AdminStaffMember> _staff = [];
    private int _nextId = 1;
    private bool _loaded;

    public event Action? OnChange;

    public AdminStaffService(IAppDatabase db)
    {
        _db = db;
    }

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _staff.Clear();
        _staff.AddRange(await _db.GetStaffAsync());
        _nextId = _staff
            .Select(s => int.TryParse(s.Id.Replace("S-", "", StringComparison.OrdinalIgnoreCase), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        _loaded = true;
        OnChange?.Invoke();
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
        var member = new AdminStaffMember
        {
            Id = string.Empty,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Role = isAdmin ? "Admin" : "Staff",
            Status = status,
            LastLogin = DateTime.Now,
            Permissions = isAdmin
                ? AdminStaffPermissions.FullAccess()
                : (permissions ?? AdminStaffPermissions.DefaultStaff()).Clone()
        };

        try
        {
            var saved = _db.UpsertStaffAsync(member).GetAwaiter().GetResult();
            _staff.Add(saved);
            OnChange?.Invoke();
            return (true, "Staff member added successfully.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
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

        if (!member.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
            return (false, "The login email cannot be changed from this page.");

        if (member.IsPrimaryAdmin)
        {
            member.FirstName = firstName;
            member.LastName = lastName;
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

        try
        {
            _db.UpsertStaffAsync(member).GetAwaiter().GetResult();
            OnChange?.Invoke();
            return (true, "Staff information updated successfully.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public (bool Success, string Message) UpdatePermissions(string id, AdminStaffPermissions permissions)
    {
        var member = GetById(id);
        if (member is null)
            return (false, "Staff member not found.");

        if (member.IsPrimaryAdmin || member.IsAdmin)
        {
            member.Permissions = AdminStaffPermissions.FullAccess();
            _db.UpsertStaffAsync(member).GetAwaiter().GetResult();
            OnChange?.Invoke();
            return (true, "Admin accounts always have full access.");
        }

        member.Permissions = permissions.Clone();
        _db.UpsertStaffAsync(member).GetAwaiter().GetResult();
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

        try
        {
            var removed = _db.DeleteStaffAsync(id).GetAwaiter().GetResult();
            if (!removed)
                return (false, "Staff member could not be removed.");

            _staff.Remove(member);
            OnChange?.Invoke();
            return (true, "Staff access removed successfully.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
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
