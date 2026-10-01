using System.Text.RegularExpressions;
using NUBulldogsExchange.Web.Shared.Data;

namespace NUBulldogsExchange.Web.Shared.Services;

public class AdminStaffService
{
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

    /// <summary>Staff accounts only (role_id = 2).</summary>
    public int TotalStaff => _staff.Count;

    /// <summary>Active Staff accounts.</summary>
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
