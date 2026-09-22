namespace NUBulldogsExchange.Web.Shared.Services;

/// <summary>
/// Per-user Supabase session state. Register as Scoped on Web and Singleton on MAUI.
/// Never store the service_role key here.
/// </summary>
public sealed class SupabaseSessionState
{
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public string? AuthUserId { get; private set; }
    public string? Email { get; private set; }

    public bool IsAuthenticated =>
        !string.IsNullOrWhiteSpace(AccessToken) &&
        !string.IsNullOrWhiteSpace(AuthUserId);

    public void Set(string accessToken, string? refreshToken, string authUserId, string? email)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        AuthUserId = authUserId;
        Email = email;
    }

    public void SetFromAccessToken(string accessToken, string authUserId, string? email)
    {
        AccessToken = accessToken;
        AuthUserId = authUserId;
        Email = email;
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        AuthUserId = null;
        Email = null;
    }
}
