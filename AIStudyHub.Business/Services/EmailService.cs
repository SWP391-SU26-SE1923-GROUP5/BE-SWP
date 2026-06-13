using System.Net;
using System.Net.Mail;
using AIStudyHub.Business.Interfaces.Services;
using AIStudyHub.Business.Options;

namespace AIStudyHub.Business.Services;

public sealed class EmailService : IEmailService
{
    private readonly SmtpOptions _smtpOptions;

    public EmailService(SmtpOptions smtpOptions)
    {
        _smtpOptions = smtpOptions;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_smtpOptions.Host)
            || string.IsNullOrWhiteSpace(_smtpOptions.FromEmail))
        {
            throw new InvalidOperationException("SMTP settings are not configured.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_smtpOptions.FromEmail, _smtpOptions.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(toEmail));

        using var client = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
        {
            EnableSsl = _smtpOptions.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = string.IsNullOrWhiteSpace(_smtpOptions.UserName)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_smtpOptions.UserName, _smtpOptions.Password)
        };

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }
}
