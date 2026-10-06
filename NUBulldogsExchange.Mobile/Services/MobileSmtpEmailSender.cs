using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.Services;

/// <summary>
/// Client-side SMTP sender using Hostinger SMTP credentials for Mobile password reset.
/// </summary>
public sealed class MobileSmtpEmailSender : IAppEmailSender
{
    public async Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new ArgumentException("Recipient email is required.", nameof(toEmail));

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            SupabaseClientConfig.Mail.FromName,
            SupabaseClientConfig.Mail.FromAddress));
        message.To.Add(new MailboxAddress(
            string.IsNullOrWhiteSpace(toName) ? toEmail : toName.Trim(),
            toEmail.Trim()));
        message.Subject = subject;

        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = textBody
        };
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(
                SupabaseClientConfig.Mail.Host,
                SupabaseClientConfig.Mail.Port,
                SecureSocketOptions.SslOnConnect,
                cancellationToken);
            await client.AuthenticateAsync(
                SupabaseClientConfig.Mail.Username,
                SupabaseClientConfig.Mail.Password,
                cancellationToken);
            await client.SendAsync(message, cancellationToken);
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(true, cancellationToken);
        }
    }

    public async Task SendStaffTemporaryPasswordAsync(
        string toEmail,
        string staffFullName,
        string temporaryPassword,
        bool mustChangePassword,
        CancellationToken cancellationToken = default)
    {
        var (subject, html, text) = StaffCredentialsEmail.Build(
            staffFullName,
            toEmail,
            temporaryPassword,
            mustChangePassword);

        await SendAsync(toEmail, staffFullName, subject, html, text, cancellationToken);
    }
}
