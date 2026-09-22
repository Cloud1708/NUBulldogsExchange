namespace NUBulldogsExchange.Mobile.Services;

/// <summary>
/// The Supabase publishable key is designed for browser/mobile clients.
/// RLS is what protects the data. NEVER put an sb_secret_* or service_role
/// key in this file.
/// </summary>
public static class SupabaseClientConfig
{
    public const string Url = "https://ykawbknefvjegisnuqaa.supabase.co";
    public const string PublishableKey = "sb_publishable_l_sA8AOIuGaKwbZo64Lpvw_4A7J089i";
}
