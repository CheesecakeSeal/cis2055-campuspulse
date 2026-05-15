using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CampusPulse.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(
            IOptions<EmailSettings> settings,
            ILogger<SmtpEmailService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(
            IEnumerable<string> recipients,
            string subject,
            string htmlBody)
        {
            // Remove empty addresses and avoid sending duplicate emails to the same recipient.
            var recipientList = recipients
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!recipientList.Any())
            {
                _logger.LogInformation("Email was not sent because there were no recipients. Subject: {Subject}", subject);
                return;
            }

            // Email can be disabled in appsettings.json so the site still runs without SMTP credentials.
            if (!_settings.Enabled)
            {
                _logger.LogInformation(
                    "Email sending is disabled. Subject: {Subject}. Recipients: {Recipients}",
                    subject,
                    string.Join(", ", recipientList));

                return;
            }

            // SMTP credentials comes from configuration/User Secrets
            if (string.IsNullOrWhiteSpace(_settings.SmtpHost) ||
                string.IsNullOrWhiteSpace(_settings.Username) ||
                string.IsNullOrWhiteSpace(_settings.Password))
            {
                _logger.LogWarning("Email sending is enabled, but SMTP settings are incomplete.");
                return;
            }

            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));

            foreach (var recipient in recipientList)
            {
                message.To.Add(MailboxAddress.Parse(recipient));
            }

            message.Subject = subject;

            message.Body = new BodyBuilder
            {
                HtmlBody = htmlBody
            }.ToMessageBody();

            using var client = new SmtpClient();

            var socketOptions = _settings.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, socketOptions);
            await client.AuthenticateAsync(_settings.Username, _settings.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}