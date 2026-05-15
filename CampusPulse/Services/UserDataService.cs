using CampusPulse.Data;
using CampusPulse.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusPulse.Services
{
    public class UserDataService : IUserDataService
    {
        private readonly AppDbContext _context;
        private readonly IImageUploadService _imageUploadService;

        public UserDataService(
            AppDbContext context,
            IImageUploadService imageUploadService)
        {
            _context = context;
            _imageUploadService = imageUploadService;
        }

        public async Task<object> BuildPersonalDataExportAsync(ApplicationUser user)
        {
            var userId = user.Id;
            var email = user.Email ?? user.UserName ?? string.Empty;

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

        public async Task DeleteOrAnonymiseUserDataAsync(ApplicationUser user)
        {
            var userId = user.Id;
            var email = user.Email ?? user.UserName ?? string.Empty;

            var submittedReports = await _context.Reports
                .Where(r => r.ReporterId == userId || r.ReporterEmail == email)
                .ToListAsync();

            foreach (var report in submittedReports)
            {
                // Uploaded images may contain personal data, so they are removed during account deletion.
                _imageUploadService.DeleteImage(report.ImageUrl);

                // Reports are anonymised rather than deleted so campus issue history is preserved.
                report.ReporterId = null;
                report.ReporterEmail = "Deleted User";
                report.ReporterPhone = null;
                report.ImageUrl = null;
            }

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

            await _context.SaveChangesAsync();
        }
    }
}