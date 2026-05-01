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
                .Include(r => r.Investigation)
                .OrderByDescending(r => r.Date_Reported)
                .ToList();
        }

        public Report? GetReportById(int id)
        {
            return _appDbContext.Reports
                .Include(r => r.Investigation)
                .FirstOrDefault(r => r.Id == id);
        }

        public void CreateReport(Report report)
        {
            _appDbContext.Reports.Add(report);
            _appDbContext.SaveChanges();
        }

        public void UpvoteReport(int id)
        {
            var report = _appDbContext.Reports.FirstOrDefault(r => r.Id == id);

            if (report == null)
            {
                return;
            }

            report.Upvotes++;
            _appDbContext.SaveChanges();
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
                    InvestigatorEmail = investigatorEmail,
                    InvestigatorPhone = investigatorPhone
                };

                _appDbContext.Investigations.Add(investigation);
            }
            else
            {
                report.Investigation.ActionTaken = actionTaken;
                report.Investigation.ActionDate = DateTime.Now;
                report.Investigation.InvestigatorEmail = investigatorEmail;
                report.Investigation.InvestigatorPhone = investigatorPhone;
            }

            _appDbContext.SaveChanges();
        }
    }
}