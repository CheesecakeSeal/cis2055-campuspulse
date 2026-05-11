using CampusPulse.Data;
using Microsoft.AspNetCore.Identity;
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

        public async Task<object> BuildPersonalDataExportAsync(IdentityUser user)
        {
            var userId = user.Id;
            var email = user.Email ?? user.UserName ?? string.Empty;

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

        public async Task DeleteOrAnonymiseUserDataAsync(IdentityUser user)
        {
            var userId = user.Id;
            var email = user.Email ?? user.UserName ?? string.Empty;

            var submittedReports = await _context.Reports
                .Where(r => r.ReporterId == userId || r.ReporterEmail == email)
                .ToListAsync();

            foreach (var report in submittedReports)
            {
                _imageUploadService.DeleteImage(report.ImageUrl);

                report.ReporterId = null;
                report.ReporterEmail = "Deleted user";
                report.ReporterPhone = null;
                report.ImageUrl = null;
            }

            var userUpvotes = await _context.ReportUpvotes
                .Include(u => u.Report)
                .Where(u => u.UserId == userId)
                .ToListAsync();

            foreach (var upvote in userUpvotes)
            {
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
                investigation.InvestigatorEmail = "Deleted investigator";
                investigation.InvestigatorPhone = null;
            }

            await _context.SaveChangesAsync();
        }
    }
}