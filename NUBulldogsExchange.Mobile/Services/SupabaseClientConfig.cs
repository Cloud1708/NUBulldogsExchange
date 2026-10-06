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
    public const string ServiceRoleKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InlrYXdia25lZnZqZWdpc251cWFhIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImlhdCI6MTc5MDA3Mjg5MSwiZXhwIjoyMTA1NjQ4ODkxfQ.UoxMB-v9AlgQH-LGnWkplkvCjiHv6igGVTQxwXHMkn8";

    public static class Mail
    {
        public const string Host = "smtp.hostinger.com";
        public const int Port = 465;
        public const string Username = "no-reply@nu-secure.com";
        public const string Password = "@bh2mRs72Qp";
        public const string FromAddress = "no-reply@nu-secure.com";
        public const string FromName = "NU-Secure";
    }
}
