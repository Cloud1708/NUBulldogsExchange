using System.Text.RegularExpressions;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminStaffService
{
    public static readonly string[] Statuses = ["Active", "Inactive"];

    public static readonly string[] StatusFilters = ["All Status", "Active", "Inactive"];

    public static readonly (string Key, string Label)[] SortOptions =
    [
        ("newest", "Sort: Newest First"),
        ("oldest", "Sort: Oldest First"),
        ("name-asc", "Sort: Name A-Z"),
        ("name-desc", "Sort: Name Z-A"),
        ("recent", "Sort: Recently Active")
    ];

    public static readonly (string Key, string Label, string Description)[] PermissionOptions =
    [
        ("products", "Manage Products", "View and manage merchandise"),
        ("categories", "Manage Categories", "Create and organize categories"),
        ("orders", "Manage Orders", "Review and process customer orders"),
        ("inventory", "Manage Inventory", "View and adjust stock"),
        ("customers", "View Customers", "View customer information"),
        ("promotions", "Manage Promotions", "Create and manage promotions"),
        ("reports", "View Reports", "Access reports and analytics")
    ];

    public static readonly (string Group, string[] Keys)[] PermissionGroups =
    [
        ("Catalog Management", ["products", "categories", "promotions"]),
        ("Orders & Inventory", ["orders", "inventory"]),
        ("Customer & Reports", ["customers", "reports"])
    ];

    public static readonly (string Key, string Label)[] PermissionPresets =
    [
        ("custom", "Custom"),
        ("order", "Order Staff"),
        ("inventory", "Inventory Staff"),
        ("catalog", "Catalog Staff"),
        ("full", "Full Staff Access")
    ];

    private static readonly TimeSpan RecentlyActiveWindow = TimeSpan.FromDays(7);

    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IAppDatabase _db;
    private readonly IAppEmailSender _email;
    private readonly List<AdminStaffMember> _staff = [];
    private bool _loaded;

    public event Action? OnChange;

    public AdminStaffService(IAppDatabase db, IAppEmailSender email)
    {
        _db = db;
        _email = email;
    }

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        _staff.Clear();
        _staff.AddRange(await _db.GetStaffAsync());
        _loaded = true;
        OnChange?.Invoke();
    }

    public IReadOnlyList<AdminStaffMember> All => _staff;

    public bool IsLoaded => _loaded;

    /// <summary>Staff accounts only (role_id = 2).</summary>
    public int TotalStaff => _staff.Count;

    /// <summary>Active Staff accounts.</summary>
    public int ActiveCount => _staff.Count(s => s.IsActive);

    /// <summary>Inactive Staff accounts.</summary>
    public int InactiveCount => _staff.Count(s => !s.IsActive);

    /// <summary>Staff who logged in within the last 7 days (real last_login_at).</summary>
    public int RecentlyActiveCount =>
        _staff.Count(s => s.IsRecentlyActive(RecentlyActiveWindow));

    /// <summary>Staff with at least one optional permission assigned.</summary>
    public int StaffWithPermissionsCount =>
        _staff.Count(s => s.Permissions.CountAssigned() > 0);

    public AdminStaffMember? GetById(string id) =>
        _staff.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public AdminStaffMember? GetByEmail(string email) =>
        _staff.FirstOrDefault(s => s.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase));

    public bool HasPermission(string email, string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var member = GetByEmail(email);
        if (member is null) return false;
        if (!member.IsActive) return false;
        if (member.IsAdmin) return true;
        return GetPermission(member.Permissions, permissionKey);
    }

    public async Task<(bool Success, string Message, AdminStaffMember? Member)> AddStaffAsync(
        CreateStaffAccountRequest request)
    {
        var firstName = request.FirstName?.Trim() ?? string.Empty;
        var lastName = request.LastName?.Trim() ?? string.Empty;
        var email = request.Email?.Trim() ?? string.Empty;
        var password = request.TemporaryPassword ?? string.Empty;
        var confirm = request.ConfirmPassword ?? string.Empty;
        var status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status.Trim();

        if (string.IsNullOrWhiteSpace(firstName))
            return (false, "First name is required.", null);
        if (string.IsNullOrWhiteSpace(lastName))
            return (false, "Last name is required.", null);
        if (string.IsNullOrWhiteSpace(email) || !EmailRegex.IsMatch(email))
            return (false, "Please enter a valid email address.", null);
        if (GetByEmail(email) is not null)
            return (false, "An account with this email already exists.", null);
        if (string.IsNullOrWhiteSpace(password) || password.Length < AuthValidation.MinPasswordLength)
            return (false, $"Password must contain at least {AuthValidation.MinPasswordLength} characters.", null);
        if (!string.Equals(password, confirm, StringComparison.Ordinal))
            return (false, "Passwords do not match.", null);
        if (!Statuses.Contains(status))
            status = "Active";

        request.FirstName = firstName;
        request.LastName = lastName;
        request.Email = email;
        request.Status = status;
        request.TemporaryPassword = password;
        request.ConfirmPassword = confirm;

        try
        {
            var saved = await _db.CreateStaffAccountAsync(request);
            _staff.RemoveAll(s => s.Id.Equals(saved.Id, StringComparison.OrdinalIgnoreCase));
            _staff.Add(saved);
            OnChange?.Invoke();

            try
            {
                await _email.SendStaffTemporaryPasswordAsync(
                    saved.Email,
                    saved.FullName,
                    password,
                    request.MustChangePassword);

                return (true, "Staff Account Created! Temporary password emailed.", saved);
            }
            catch (Exception mailEx)
            {
                // Account already exists — do not roll back. Admin can resend/share manually.
                var mailError = CleanError(mailEx.Message);
                return (
                    true,
                    $"Staff account created, but the temporary password email could not be sent. {mailError}",
                    saved);
            }
        }
        catch (Exception ex)
        {
            var message = CleanError(ex.Message);
            if (message.Contains("already", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("registered", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("exists", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "An account with this email already exists.", null);
            }

            return (false, message, null);
        }
    }

    public async Task<(bool Success, string Message)> UpdateAsync(
        string id,
        string firstName,
        string lastName,
        string status)
    {
        var member = GetById(id);
        if (member is null)
            return (false, "Staff member not found.");

        firstName = firstName.Trim();
        lastName = lastName.Trim();

        if (string.IsNullOrWhiteSpace(firstName))
            return (false, "First name is required.");
        if (string.IsNullOrWhiteSpace(lastName))
            return (false, "Last name is required.");

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
            if (string.IsNullOrWhiteSpace(status) || !Statuses.Contains(status))
                return (false, "Select a status.");

            member.FirstName = firstName;
            member.LastName = lastName;
            // Role stays Staff (or Admin if already admin) — never promote Staff→Admin here.
            if (!member.IsAdmin)
                member.Role = "Staff";
            member.Status = status;
            if (member.IsAdmin)
                member.Permissions = AdminStaffPermissions.FullAccess();
        }

        try
        {
            await _db.UpsertStaffAsync(member);
            OnChange?.Invoke();
            return (true, "Staff information updated successfully.");
        }
        catch (Exception ex)
        {
            return (false, CleanError(ex.Message));
        }
    }

    public async Task<(bool Success, string Message)> UpdatePermissionsAsync(string id, AdminStaffPermissions permissions)
    {
        var member = GetById(id);
        if (member is null)
            return (false, "Staff member not found.");

        if (member.IsPrimaryAdmin || member.IsAdmin)
        {
            member.Permissions = AdminStaffPermissions.FullAccess();
            await _db.UpsertStaffAsync(member);
            OnChange?.Invoke();
            return (true, "Admin accounts always have full access.");
        }

        member.Permissions = permissions.Clone();
        await _db.UpsertStaffAsync(member);
        OnChange?.Invoke();
        return (true, "Permissions updated successfully.");
    }

    public async Task<(bool Success, string Message)> DeactivateAsync(string id, string? currentUserEmail)
    {
        var member = GetById(id);
        if (member is null)
            return (false, "Staff member not found.");

        if (member.IsPrimaryAdmin)
            return (false, "The primary admin account cannot be deactivated.");

        if (!string.IsNullOrWhiteSpace(currentUserEmail) &&
            member.Email.Equals(currentUserEmail.Trim(), StringComparison.OrdinalIgnoreCase))
            return (false, "You cannot deactivate your own account.");

        if (!member.IsActive)
            return (true, "Account is already inactive.");

        member.Status = "Inactive";
        try
        {
            await _db.UpsertStaffAsync(member);
            OnChange?.Invoke();
            return (true, "Staff account deactivated. Web login is now blocked.");
        }
        catch (Exception ex)
        {
            return (false, CleanError(ex.Message));
        }
    }

    public async Task<(bool Success, string Message)> ActivateAsync(string id)
    {
        var member = GetById(id);
        if (member is null)
            return (false, "Staff member not found.");

        if (member.IsActive)
            return (true, "Account is already active.");

        member.Status = "Active";
        try
        {
            await _db.UpsertStaffAsync(member);
            OnChange?.Invoke();
            return (true, "Staff account activated. Login is restored.");
        }
        catch (Exception ex)
        {
            return (false, CleanError(ex.Message));
        }
    }

    public IEnumerable<AdminStaffMember> FilterSort(
        string? search,
        string statusFilter,
        string sortKey)
    {
        IEnumerable<AdminStaffMember> query = _staff;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s =>
                s.FirstName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                s.LastName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                s.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                s.Email.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) &&
            !statusFilter.Equals("All Status", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(s => s.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase));
        }

        return sortKey switch
        {
            "oldest" => query
                .OrderBy(s => s.CreatedAt == default ? DateTime.MaxValue : s.CreatedAt)
                .ThenBy(s => s.LastName)
                .ThenBy(s => s.FirstName),
            "name-asc" => query
                .OrderBy(s => s.LastName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.FirstName, StringComparer.OrdinalIgnoreCase),
            "name-desc" => query
                .OrderByDescending(s => s.LastName, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(s => s.FirstName, StringComparer.OrdinalIgnoreCase),
            "recent" => query
                .OrderByDescending(s => s.HasLastLogin)
                .ThenByDescending(s => s.LastLogin ?? DateTime.MinValue)
                .ThenBy(s => s.LastName),
            _ => query
                .OrderByDescending(s => s.CreatedAt == default ? DateTime.MinValue : s.CreatedAt)
                .ThenBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
        };
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
            "staff" => false,
            "settings" => false,
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

    public static void SelectAllStaffSafe(AdminStaffPermissions permissions)
    {
        foreach (var option in PermissionOptions)
            SetPermission(permissions, option.Key, true);
    }

    public static void ClearAllOptional(AdminStaffPermissions permissions)
    {
        foreach (var option in PermissionOptions)
            SetPermission(permissions, option.Key, false);
    }

    public static void ApplyPreset(AdminStaffPermissions permissions, string presetKey)
    {
        ClearAllOptional(permissions);
        switch (presetKey)
        {
            case "order":
                permissions.ManageOrders = true;
                permissions.ViewCustomers = true;
                break;
            case "inventory":
                permissions.ManageProducts = true;
                permissions.ManageInventory = true;
                break;
            case "catalog":
                permissions.ManageProducts = true;
                permissions.ManageCategories = true;
                permissions.ManagePromotions = true;
                break;
            case "full":
                SelectAllStaffSafe(permissions);
                break;
            // "custom" leaves cleared — caller may have already set checkboxes
        }
    }

    public static string DetectPreset(AdminStaffPermissions permissions)
    {
        var order = new AdminStaffPermissions { ManageOrders = true, ViewCustomers = true };
        var inventory = new AdminStaffPermissions { ManageProducts = true, ManageInventory = true };
        var catalog = new AdminStaffPermissions
        {
            ManageProducts = true,
            ManageCategories = true,
            ManagePromotions = true
        };
        var full = AdminStaffPermissions.FullAccess();

        if (permissions.Matches(full)) return "full";
        if (permissions.Matches(order)) return "order";
        if (permissions.Matches(inventory)) return "inventory";
        if (permissions.Matches(catalog)) return "catalog";
        return "custom";
    }

    public static string? GetPermissionDescription(string key) =>
        PermissionOptions.FirstOrDefault(o => o.Key == key).Description;

    public static string? GetPermissionLabel(string key) =>
        PermissionOptions.FirstOrDefault(o => o.Key == key).Label;

    /// <summary>
    /// Maps an admin route path segment to a staff-assignable permission key.
    /// Null means open to all admin-portal users (Dashboard / Reviews / Notifications).
    /// "admin-only" means Admin role required.
    /// </summary>
    public static string? ResolveRoutePermission(string absolutePath)
    {
        var path = absolutePath.TrimEnd('/').ToLowerInvariant();
        if (path.EndsWith("/admin") || path.EndsWith("/admin/dashboard") || path.Contains("/admin/change-password"))
            return null;
        if (path.Contains("/admin/staff")) return "admin-only";
        if (path.Contains("/admin/settings")) return "admin-only";
        if (path.Contains("/admin/products")) return "products";
        if (path.Contains("/admin/categories")) return "categories";
        if (path.Contains("/admin/orders")) return "orders";
        if (path.Contains("/admin/inventory")) return "inventory";
        if (path.Contains("/admin/customers")) return "customers";
        if (path.Contains("/admin/promotions")) return "promotions";
        if (path.Contains("/admin/reports")) return "reports";
        if (path.Contains("/admin/reviews") || path.Contains("/admin/notifications") || path.Contains("/admin/access-denied"))
            return null;
        return null;
    }

    private static string CleanError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Unable to complete the staff operation.";

        var trimmed = message.Trim();
        if (trimmed.Contains("service_role", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("sb_secret", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("apikey", StringComparison.OrdinalIgnoreCase))
        {
            return "Unable to create the staff account. Please contact the system administrator.";
        }

        if (trimmed.Length > 220)
            trimmed = trimmed[..220].Trim() + "…";

        return trimmed;
    }
}
