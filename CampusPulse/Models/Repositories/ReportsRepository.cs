using CampusPulse.Data;
using CampusPulse.Models.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CampusPulse.Models.Repositories
{
    public class ReportsRepository : IReportsRepository
    {
        private readonly AppDbContext _appDbContext;

        public ReportsRepository(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public IEnumerable<Report> GetAllReports()
        {
            return _appDbContext.Reports
                .Include(r => r.Reporter)
                .Include(r => r.Investigation)
                    .ThenInclude(i => i.Investigator)
                .OrderByDescending(r => r.Date_Reported)
                .ToList();
        }

        public Report? GetReportById(int id)
        {
            return _appDbContext.Reports
                .Include(r => r.Reporter)
                .Include(r => r.Investigation)
                    .ThenInclude(i => i.Investigator)
                .Include(r => r.Activities)
                .FirstOrDefault(r => r.Id == id);
        }

        public void CreateReport(Report report)
        {
            _appDbContext.Reports.Add(report);
            _appDbContext.SaveChanges();
        }

        public void UpdateReport(Report report)
        {
            _appDbContext.Reports.Update(report);
            _appDbContext.SaveChanges();
        }

        public void DeleteReport(int id)
        {
            var report = _appDbContext.Reports
                .Include(r => r.Investigation)
                .FirstOrDefault(r => r.Id == id);

            if (report == null)
            {
                return;
            }

            _appDbContext.Reports.Remove(report);
            _appDbContext.SaveChanges();
        }

        public bool HasUserUpvotedReport(int reportId, string userId)
        {
            return _appDbContext.ReportUpvotes
                .Any(ru => ru.ReportId == reportId && ru.UserId == userId);
        }

        public bool ToggleUpvoteReport(int reportId, string userId)
        {
            var report = _appDbContext.Reports
                .FirstOrDefault(r => r.Id == reportId);

            if (report == null)
            {
                return false;
            }

            var existingUpvote = _appDbContext.ReportUpvotes
                .FirstOrDefault(ru => ru.ReportId == reportId && ru.UserId == userId);

            if (existingUpvote == null)
            {
                var upvote = new ReportUpvote
                {
                    ReportId = reportId,
                    UserId = userId,
                    CreatedAt = DateTime.Now
                };

                _appDbContext.ReportUpvotes.Add(upvote);
                report.Upvotes++;
                _appDbContext.SaveChanges();

                return true;
            }

            _appDbContext.ReportUpvotes.Remove(existingUpvote);

            if (report.Upvotes > 0)
            {
                report.Upvotes--;
            }

            _appDbContext.SaveChanges();

            return false;
        }

        public void UpdateReportStatus(int id, string status)
        {
            var report = _appDbContext.Reports.FirstOrDefault(r => r.Id == id);

            if (report == null)
            {
                return;
            }

            report.Status = status;
            _appDbContext.SaveChanges();
        }

        public void AddOrUpdateInvestigation(
            int reportId,
            string actionTaken,
            string investigatorId,
            string investigatorEmail,
            string? investigatorPhone)
        {
            var report = _appDbContext.Reports
                .Include(r => r.Investigation)
                .FirstOrDefault(r => r.Id == reportId);

            if (report == null)
            {
                return;
            }

            if (report.Investigation == null)
            {
                var investigation = new Investigation
                {
                    ReportId = reportId,
                    ActionTaken = actionTaken,
                    ActionDate = DateTime.Now,
                    InvestigatorId = investigatorId,
                    InvestigatorEmail = investigatorEmail,
                    InvestigatorPhone = investigatorPhone
                };

                _appDbContext.Investigations.Add(investigation);
            }
            else
            {
                report.Investigation.ActionTaken = actionTaken;
                report.Investigation.ActionDate = DateTime.Now;
                report.Investigation.InvestigatorId = investigatorId;
                report.Investigation.InvestigatorEmail = investigatorEmail;
                report.Investigation.InvestigatorPhone = investigatorPhone;
            }

            _appDbContext.SaveChanges();
        }
    }
}