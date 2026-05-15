using CampusPulse.Data;
using CampusPulse.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace CampusPulse.Services
{
    public class CampusPulseNotificationService : ICampusPulseNotificationService
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<CampusPulseNotificationService> _logger;

        public CampusPulseNotificationService(
            AppDbContext context,
            IEmailService emailService,
            ILogger<CampusPulseNotificationService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task NotifyInvestigatorsOfNewReportAsync(Report report, string reportUrl)
        {
            try
            {
                var investigatorEmails = await _context.InvestigatorEmails
                    .Select(i => i.Email)
                    .ToListAsync();

                if (!investigatorEmails.Any())
                {
                    _logger.LogInformation("No investigator emails are configured. New report notification skipped.");
                    return;
                }

                var subject = $"New CampusPulse report: {report.Title}";

                // User-submitted values are HTML-encoded before being inserted into email HTML.
                var body = $@"
                    <h2>New CampusPulse Report Submitted</h2>
                    <p>A new report has been submitted and is awaiting review.</p>

                    <p><strong>Title:</strong> {Html(report.Title)}</p>
                    <p><strong>Category:</strong> {Html(report.Category)}</p>
                    <p><strong>Location:</strong> {Html(report.Location)}</p>
                    <p><strong>Status:</strong> {Html(report.Status)}</p>
                    <p><strong>Date Reported:</strong> {report.Date_Reported:dd MMM yyyy HH:mm}</p>

                    <p>
                        <a href=""{Html(reportUrl)}"">View report</a>
                    </p>
                ";

                await _emailService.SendEmailAsync(investigatorEmails, subject, body);
            }
            catch (Exception ex)
            {
                // Email failure should not block the report workflow from completing.
                _logger.LogError(ex, "Failed to send new report notification for report {ReportId}.", report.Id);
            }
        }

        public async Task NotifyReporterOfStatusChangeAsync(Report report, string reportUrl)
        {
            try
            {
                // Anonymised/deleted users should not receive notification emails.
                if (!IsValidRecipient(report.ReporterEmail))
                {
                    _logger.LogInformation("Status notification skipped because report {ReportId} has no valid reporter email.", report.Id);
                    return;
                }

                var subject = $"CampusPulse report status updated: {report.Title}";

                var body = $@"
                    <h2>Your CampusPulse Report Was Updated</h2>

                    <p>Your report status has been updated.</p>

                    <p><strong>Title:</strong> {Html(report.Title)}</p>
                    <p><strong>New Status:</strong> {Html(report.Status)}</p>
                    <p><strong>Location:</strong> {Html(report.Location)}</p>

                    <p>
                        <a href=""{Html(reportUrl)}"">View report</a>
                    </p>
                ";

                await _emailService.SendEmailAsync(
                    new[] { report.ReporterEmail },
                    subject,
                    body);
            }
            catch (Exception ex)
            {
                // Email failure should be logged.
                _logger.LogError(ex, "Failed to send status update notification for report {ReportId}.", report.Id);
            }
        }

        public async Task NotifyReporterOfInvestigationUpdateAsync(Report report, string reportUrl)
        {
            try
            {
                // Anonymised/deleted users should not receive notification emails.
                if (!IsValidRecipient(report.ReporterEmail))
                {
                    _logger.LogInformation("Investigation notification skipped because report {ReportId} has no valid reporter email.", report.Id);
                    return;
                }

                var subject = $"CampusPulse investigation update: {report.Title}";

                var actionTaken = report.Investigation?.ActionTaken ?? "An investigation update has been added.";

                var body = $@"
                    <h2>Your CampusPulse Report Has an Investigation Update</h2>

                    <p>An investigator has added or updated the investigation details for your report.</p>

                    <p><strong>Title:</strong> {Html(report.Title)}</p>
                    <p><strong>Status:</strong> {Html(report.Status)}</p>
                    <p><strong>Actions Taken:</strong></p>
                    <p>{Html(actionTaken)}</p>

                    <p>
                        <a href=""{Html(reportUrl)}"">View report</a>
                    </p>
                ";

                await _emailService.SendEmailAsync(
                    new[] { report.ReporterEmail },
                    subject,
                    body);
            }
            catch (Exception ex)
            {
                // Email failure should be logged.
                _logger.LogError(ex, "Failed to send investigation update notification for report {ReportId}.", report.Id);
            }
        }

        private static string Html(string? value)
        {
            // Central helper so every user-controlled value in email HTML is encoded consistently.
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private static bool IsValidRecipient(string? email)
        {
            return !string.IsNullOrWhiteSpace(email)
                   && email.Contains('@')
                   && !email.Equals("Deleted user", StringComparison.OrdinalIgnoreCase);
        }
    }
}