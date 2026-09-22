using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.Services;

/// <summary>
/// Customer-only access for the MAUI app. Admin/Staff remain valid on Web.
/// </summary>
public static class MobileAuthGuard
{
    public const int CustomerRoleId = 3;
    private const string TokenKey = "nube-mobile-session";

    public const string AccessDeniedTitle = "Access Denied";
    public const string AccessDeniedMessage =
        "Only customer accounts can access the NU Bulldogs Exchange mobile application.";
    public const string InactiveMessage =
        "Your account is currently inactive. Please contact the administrator.";
    public const string NotFoundMessage = "User account was not found.";

    public static bool IsAllowedCustomer(MockUser? user) =>
        user is not null
        && user.RoleId == CustomerRoleId
        && string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Signs out and clears the local session unless the user is an Active customer.
    /// Returns null when access is allowed; otherwise the message to display.
    /// </summary>
    public static async Task<string?> EnforceAsync(AuthService auth, MockUser? user)
    {
        if (IsAllowedCustomer(user))
            return null;

        await auth.LogoutAsync();
        await ClearAsync();

        if (user is null)
            return NotFoundMessage;

        if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            return InactiveMessage;

        return AccessDeniedMessage;
    }

    public static async Task PersistAsync(MockUser user)
    {
        if (string.IsNullOrWhiteSpace(user.SessionToken) || !user.RememberMe)
        {
            await ClearAsync();
            return;
        }

        try
        {
            await SecureStorage.Default.SetAsync(TokenKey, user.SessionToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    public static async Task PersistAfterRegisterAsync(MockUser user)
    {
        if (string.IsNullOrWhiteSpace(user.SessionToken))
            return;

        try
        {
            await SecureStorage.Default.SetAsync(TokenKey, user.SessionToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    public static async Task<string?> GetTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(TokenKey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return null;
        }
    }

    public static Task ClearAsync()
    {
        try
        {
            SecureStorage.Default.Remove(TokenKey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }

        return Task.CompletedTask;
    }
}
