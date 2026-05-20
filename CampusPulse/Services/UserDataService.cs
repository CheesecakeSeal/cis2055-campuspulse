using CampusPulse.Data;
using CampusPulse.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusPulse.Services
{
    public class UserDataService : IUserDataService
    {
        private readonly AppDbContext _context;
        private readonly IImageUploadService _imageUploadService;
        private readonly ILogger<UserDataService> _logger;

        public UserDataService(
            AppDbContext context,
            IImageUploadService imageUploadService,
            ILogger<UserDataService> logger)
        {
            _context = context;
            _imageUploadService = imageUploadService;
            _logger = logger;
        }

        public async Task<object> BuildPersonalDataExportAsync(ApplicationUser user)
        {
            try
            {
                var userId = user.Id;
                var email = user.Email ?? user.UserName ?? string.Empty;

                _logger.LogInformation(
                    "Personal data export started. UserId: {UserId}",
                    userId);

                // Export custom CampusPulse data as well as Identity account data.
                // This ensures reports, upvotes, and investigation entries are included in the user's data download.
                var submittedReports = await _context.Reports
                    .Where(r => r.ReporterId == userId || r.ReporterEmail == email)
                    .Select(r => new
                    {
                        r.Id,
                        r.Title,
                        r.Date_Reported,
                        r.Location,
                        r.Time_Observed,
                        r.Category,
                        r.Description,
                        r.Status,
                        r.ReporterEmail,
                        r.ReporterPhone,
                        r.ImageUrl,
                        r.Upvotes
                    })
                    .ToListAsync();

                var upvotes = await _context.ReportUpvotes
                    .Where(u => u.UserId == userId)
                    .Select(u => new
                    {
                        u.ReportId,
                        u.CreatedAt
                    })
                    .ToListAsync();

                var investigations = await _context.Investigations
                    .Where(i => i.InvestigatorEmail == email)
                    .Select(i => new
                    {
                        i.ReportId,
                        i.ActionTaken,
                        i.ActionDate,
                        i.InvestigatorEmail,
                        i.InvestigatorPhone
                    })
                    .ToListAsync();

                _logger.LogInformation(
                    "Personal data export built successfully. UserId: {UserId}; SubmittedReports: {SubmittedReports}; Upvotes: {Upvotes}; InvestigationsAuthored: {InvestigationsAuthored}",
                    userId,
                    submittedReports.Count,
                    upvotes.Count,
                    investigations.Count);

                return new
                {
                    ExportedAtUtc = DateTime.UtcNow,

                    Account = new
                    {
                        user.Id,
                        user.UserName,
                        user.Email,
                        user.DisplayName,
                        user.EmailConfirmed,
                        user.PhoneNumber,
                        user.PhoneNumberConfirmed,
                        user.TwoFactorEnabled,
                        user.LockoutEnabled,
                        user.LockoutEnd,
                        user.AccessFailedCount
                    },

                    SubmittedReports = submittedReports,
                    Upvotes = upvotes,
                    InvestigationsAuthored = investigations
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to build personal data export. UserId: {UserId}",
                    user.Id);

                throw;
            }
        }

        public async Task DeleteOrAnonymiseUserDataAsync(ApplicationUser user)
        {
            try
            {
                var userId = user.Id;
                var email = user.Email ?? user.UserName ?? string.Empty;

                _logger.LogWarning(
                    "User data deletion/anonymisation started. UserId: {UserId}",
                    userId);

                var submittedReports = await _context.Reports
                    .Where(r => r.ReporterId == userId || r.ReporterEmail == email)
                    .ToListAsync();

                var imagesDeleted = 0;

                foreach (var report in submittedReports)
                {
                    // Uploaded images may contain personal data, so they are removed during account deletion.
                    if (!string.IsNullOrWhiteSpace(report.ImageUrl))
                    {
                        _imageUploadService.DeleteImage(report.ImageUrl);
                        imagesDeleted++;
                    }

                    // Reports are anonymised rather than deleted so campus issue history is preserved.
                    report.ReporterId = null;
                    report.ReporterEmail = "Deleted User";
                    report.ReporterPhone = null;
                    report.ImageUrl = null;
                }

                _logger.LogInformation(
                    "Submitted reports anonymised. UserId: {UserId}; ReportsAnonymised: {ReportsAnonymised}; ImagesDeleted: {ImagesDeleted}",
                    userId,
                    submittedReports.Count,
                    imagesDeleted);

                var userUpvotes = await _context.ReportUpvotes
                    .Include(u => u.Report)
                    .Where(u => u.UserId == userId)
                    .ToListAsync();

                foreach (var upvote in userUpvotes)
                {
                    // Keep the stored upvote counter consistent when removing the user's upvote records.
                    if (upvote.Report.Upvotes > 0)
                    {
                        upvote.Report.Upvotes--;
                    }
                }

                _context.ReportUpvotes.RemoveRange(userUpvotes);

                _logger.LogInformation(
                    "User upvotes removed during account deletion. UserId: {UserId}; UpvotesRemoved: {UpvotesRemoved}",
                    userId,
                    userUpvotes.Count);

                var investigations = await _context.Investigations
                    .Where(i => i.InvestigatorEmail == email)
                    .ToListAsync();

                foreach (var investigation in investigations)
                {
                    // Investigation records remain, but personal investigator details are removed.
                    investigation.InvestigatorEmail = "Deleted Investigator";
                    investigation.InvestigatorPhone = null;
                    investigation.InvestigatorId = null;
                }

                _logger.LogInformation(
                    "Investigation records anonymised during account deletion. UserId: {UserId}; InvestigationsAnonymised: {InvestigationsAnonymised}",
                    userId,
                    investigations.Count);

                await _context.SaveChangesAsync();

                _logger.LogWarning(
                    "User data deletion/anonymisation completed successfully. UserId: {UserId}; ReportsAnonymised: {ReportsAnonymised}; ImagesDeleted: {ImagesDeleted}; UpvotesRemoved: {UpvotesRemoved}; InvestigationsAnonymised: {InvestigationsAnonymised}",
                    userId,
                    submittedReports.Count,
                    imagesDeleted,
                    userUpvotes.Count,
                    investigations.Count);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Database update error during user data deletion/anonymisation. UserId: {UserId}",
                    user.Id);

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error during user data deletion/anonymisation. UserId: {UserId}",
                    user.Id);

                throw;
            }
        }
    }
}