namespace NUBulldogsExchange.Web.Shared.Services;

public sealed class MailOptions
{
    public const string SectionName = "Mail";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 465;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    /// <summary>ssl | starttls | none</summary>
    public string Encryption { get; set; } = "ssl";
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "NU Bulldogs Exchange";
}

public interface IAppEmailSender
{
    Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default);

    Task SendStaffTemporaryPasswordAsync(
        string toEmail,
        string staffFullName,
        string temporaryPassword,
        bool mustChangePassword,
        CancellationToken cancellationToken = default);
}

/// <summary>No-op sender for hosts that do not configure SMTP (e.g. local MAUI).</summary>
public sealed class NullAppEmailSender : IAppEmailSender
{
    public Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SendStaffTemporaryPasswordAsync(
        string toEmail,
        string staffFullName,
        string temporaryPassword,
        bool mustChangePassword,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public static class StaffCredentialsEmail
{
    public static (string Subject, string Html, string Text) Build(
        string staffFullName,
        string toEmail,
        string temporaryPassword,
        bool mustChangePassword)
    {
        var name = string.IsNullOrWhiteSpace(staffFullName) ? "Staff Member" : staffFullName.Trim();
        var subject = "Your NU Bulldogs Exchange Staff Account";

        var changeNote = mustChangePassword
            ? "For security, you will be required to create a new password after your first login."
            : "Please keep this temporary password secure and change it after you sign in.";

        var text = $"""
            Hello {name},

            An Admin created a Staff account for you on NU Bulldogs Exchange.

            Email: {toEmail}
            Temporary Password: {temporaryPassword}

            {changeNote}

            Sign in at the NU Bulldogs Admin Portal using this email and temporary password.

            If you did not expect this message, contact your administrator.

            — NU-Secure / NU Bulldogs Exchange
            """;

        var html = $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;color:#0f172a;line-height:1.5;max-width:560px;margin:0 auto;">
              <h2 style="color:#123A63;margin:0 0 12px;">Your Staff Account</h2>
              <p style="margin:0 0 12px;">Hello <strong>{System.Net.WebUtility.HtmlEncode(name)}</strong>,</p>
              <p style="margin:0 0 12px;">An Admin created a Staff account for you on <strong>NU Bulldogs Exchange</strong>.</p>
              <div style="background:#F8FAFC;border:1px solid #E2E8F0;border-radius:12px;padding:14px 16px;margin:0 0 14px;">
                <p style="margin:0 0 8px;"><strong>Email:</strong> {System.Net.WebUtility.HtmlEncode(toEmail)}</p>
                <p style="margin:0;"><strong>Temporary Password:</strong> <code style="font-size:15px;">{System.Net.WebUtility.HtmlEncode(temporaryPassword)}</code></p>
              </div>
              <p style="margin:0 0 12px;color:#334155;">{System.Net.WebUtility.HtmlEncode(changeNote)}</p>
              <p style="margin:0 0 12px;">Sign in at the NU Bulldogs Admin Portal using this email and temporary password.</p>
              <p style="margin:0;color:#64748B;font-size:13px;">If you did not expect this message, contact your administrator.</p>
              <p style="margin:18px 0 0;color:#94A3B8;font-size:12px;">— NU-Secure / NU Bulldogs Exchange</p>
            </div>
            """;

        return (subject, html, text);
    }
}

public static class PasswordResetCodeEmail
{
    public static (string Subject, string Html, string Text) Build(
        string recipientName,
        string code,
        int expiresInMinutes)
    {
        var name = string.IsNullOrWhiteSpace(recipientName) ? "there" : recipientName.Trim();
        var subject = "NU Bulldogs Exchange — Password Reset Code";
        var minutes = Math.Max(1, expiresInMinutes);

        var text = $"""
            Hello {name},

            We received a request to reset the password for your NU Bulldogs Exchange account.

            Your verification code is:

            {code}

            This code will expire in {minutes} minutes.

            If you did not request a password reset, you can ignore this email.

            NU Bulldogs Exchange
            """;

        var html = $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;color:#0f172a;line-height:1.5;max-width:560px;margin:0 auto;">
              <h2 style="color:#123A63;margin:0 0 12px;">Password Reset Code</h2>
              <p style="margin:0 0 12px;">Hello <strong>{System.Net.WebUtility.HtmlEncode(name)}</strong>,</p>
              <p style="margin:0 0 12px;">We received a request to reset the password for your <strong>NU Bulldogs Exchange</strong> account.</p>
              <div style="background:#F8FAFC;border:1px solid #E2E8F0;border-radius:12px;padding:18px 16px;margin:0 0 14px;text-align:center;">
                <p style="margin:0 0 8px;color:#64748B;font-size:13px;">Your verification code</p>
                <p style="margin:0;letter-spacing:6px;font-size:28px;font-weight:800;color:#123A63;font-family:Consolas,Menlo,monospace;">{System.Net.WebUtility.HtmlEncode(code)}</p>
              </div>
              <p style="margin:0 0 12px;">This code will expire in {minutes} minutes.</p>
              <p style="margin:0;color:#64748B;font-size:13px;">If you did not request a password reset, you can ignore this email.</p>
              <p style="margin:18px 0 0;color:#94A3B8;font-size:12px;">NU Bulldogs Exchange</p>
            </div>
            """;

        return (subject, html, text);
    }
}
