namespace NUBulldogsExchange.Mobile.Services;

/// <summary>
/// Shared Web/API base URL used by HttpClient (non-Windows) and Staff/Admin portal launcher.
/// Matches the same host resolution as <see cref="MauiProgram"/>.
/// </summary>
public static class MobileWebUrls
{
    public static string ApiBase =>
        DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5016/"
            : "http://localhost:5016/";

    public static Uri AdminPortalUri => new(new Uri(ApiBase), "admin");
}
