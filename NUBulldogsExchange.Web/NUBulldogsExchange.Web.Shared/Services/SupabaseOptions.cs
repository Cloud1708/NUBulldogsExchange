namespace NUBulldogsExchange.Web.Shared.Services;

public sealed class SupabaseOptions
{
    public string Url { get; }

    /// <summary>
    /// Use the client-safe Supabase publishable key (sb_publishable_...).
    /// A legacy anon key also works while Supabase still supports it.
    /// </summary>
    public string PublishableKey { get; }

    // Compatibility alias used internally by the migration code.
    public string AnonKey => PublishableKey;

    public SupabaseOptions(string url, string publishableKey)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Supabase URL is required.", nameof(url));
        if (string.IsNullOrWhiteSpace(publishableKey))
            throw new ArgumentException("Supabase publishable key is required.", nameof(publishableKey));

        Url = url.TrimEnd('/') + "/";
        PublishableKey = publishableKey.Trim();
    }
}
