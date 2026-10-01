using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Web.Web.Services;

/// <summary>
/// Server-side SMTP sender (Hostinger / NU-Secure).
/// Credentials come from Web.Web appsettings only — never from browser/Mobile.
/// </summary>
public sealed class SmtpEmailSender : IAppEmailSender
{
    private readonly MailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<MailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
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

    public async Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.Username) ||
            string.IsNullOrWhiteSpace(_options.Password) ||
            string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            throw new InvalidOperationException(
                "Mail is not configured. Set Mail:* in Web.Web appsettings.json.");
        }

        if (string.IsNullOrWhiteSpace(toEmail))
            throw new ArgumentException("Recipient email is required.", nameof(toEmail));

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            string.IsNullOrWhiteSpace(_options.FromName) ? "NU-Secure" : _options.FromName,
            _options.FromAddress.Trim()));
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
            var socketOptions = ResolveSocketOptions(_options.Encryption, _options.Port);
            await client.ConnectAsync(_options.Host, _options.Port, socketOptions, cancellationToken);
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            throw new InvalidOperationException(
                "Unable to send the temporary password email. Check SMTP settings and try again.",
                ex);
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(true, cancellationToken);
        }
    }

    private static SecureSocketOptions ResolveSocketOptions(string? encryption, int port)
    {
        var value = (encryption ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "ssl" or "sslonconnect" => SecureSocketOptions.SslOnConnect,
            "starttls" => SecureSocketOptions.StartTls,
            "none" or "false" => SecureSocketOptions.None,
            _ when port == 465 => SecureSocketOptions.SslOnConnect,
            _ when port == 587 => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.Auto
        };
    }
}
