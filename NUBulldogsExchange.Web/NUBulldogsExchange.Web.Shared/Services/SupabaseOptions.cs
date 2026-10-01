namespace NUBulldogsExchange.Web.Shared.Services;

public sealed class SupabaseOptions
{
    public string Url { get; }

    /// <summary>
    /// Use the client-safe Supabase publishable key (sb_publishable_...).
    /// A legacy anon key also works while Supabase still supports it.
    /// </summary>
    public string PublishableKey { get; }

    /// <summary>
    /// Optional server-only Admin API key. Never ship this to browsers, Mobile,
    /// or any client-side configuration. Used only by Web.Web Interactive Server
    /// for privileged Auth user creation (Add Staff).
    /// </summary>
    public string? ServiceRoleKey { get; }

    public bool HasServiceRoleKey =>
        !string.IsNullOrWhiteSpace(ServiceRoleKey);

    // Compatibility alias used internally by the migration code.
    public string AnonKey => PublishableKey;

    public SupabaseOptions(string url, string publishableKey, string? serviceRoleKey = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Supabase URL is required.", nameof(url));
        if (string.IsNullOrWhiteSpace(publishableKey))
            throw new ArgumentException("Supabase publishable key is required.", nameof(publishableKey));

        Url = url.TrimEnd('/') + "/";
        PublishableKey = publishableKey.Trim();
        ServiceRoleKey = string.IsNullOrWhiteSpace(serviceRoleKey) ? null : serviceRoleKey.Trim();
    }
}
