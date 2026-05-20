using CampusPulse.Data;
using CampusPulse.Models.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CampusPulse.Models.Repositories
{
    public class ReportsRepository : IReportsRepository
    {
        private readonly AppDbContext _appDbContext;
        private readonly ILogger<ReportsRepository> _logger;

        public ReportsRepository(
            AppDbContext appDbContext,
            ILogger<ReportsRepository> logger)
        {
            _appDbContext = appDbContext;
            _logger = logger;
        }

        public IEnumerable<Report> GetAllReports()
        {
            try
            {
                return _appDbContext.Reports
                    .Include(r => r.Reporter)
                    .Include(r => r.Investigation)
                        .ThenInclude(i => i.Investigator)
                    .OrderByDescending(r => r.Date_Reported)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Repository error while retrieving all reports.");

                throw;
            }
        }

        public Report? GetReportById(int id)
        {
            try
            {
                return _appDbContext.Reports
                    .Include(r => r.Reporter)
                    .Include(r => r.Investigation)
                        .ThenInclude(i => i.Investigator)
                    .Include(r => r.Activities)
                    .FirstOrDefault(r => r.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Repository error while retrieving report by ID. ReportId: {ReportId}",
                    id);

                throw;
            }
        }

        public void CreateReport(Report report)
        {
            try
            {
                _appDbContext.Reports.Add(report);
                _appDbContext.SaveChanges();

                _logger.LogInformation(
                    "Repository created report. ReportId: {ReportId}; ReporterId: {ReporterId}; Category: {Category}; HasImage: {HasImage}",
                    report.Id,
                    report.ReporterId,
                    report.Category,
                    !string.IsNullOrWhiteSpace(report.ImageUrl));
            }
            catch (DbUpdateException ex)
            {
                LogDatabaseUpdateException(ex, "creating report", report.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected repository error while creating report. ReporterId: {ReporterId}; Category: {Category}",
                    report.ReporterId,
                    report.Category);

                throw;
            }
        }

        public void UpdateReport(Report report)
        {
            try
            {
                _appDbContext.Reports.Update(report);
                _appDbContext.SaveChanges();

                _logger.LogInformation(
                    "Repository updated report. ReportId: {ReportId}; ReporterId: {ReporterId}; Status: {Status}; HasImage: {HasImage}",
                    report.Id,
                    report.ReporterId,
                    report.Status,
                    !string.IsNullOrWhiteSpace(report.ImageUrl));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(
                    ex,
                    "Concurrency error while updating report. ReportId: {ReportId}; ReporterId: {ReporterId}",
                    report.Id,
                    report.ReporterId);

                throw;
            }
            catch (DbUpdateException ex)
            {
                LogDatabaseUpdateException(ex, "updating report", report.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected repository error while updating report. ReportId: {ReportId}; ReporterId: {ReporterId}",
                    report.Id,
                    report.ReporterId);

                throw;
            }
        }

        public void DeleteReport(int id)
        {
            try
            {
                var report = _appDbContext.Reports
                    .Include(r => r.Investigation)
                    .FirstOrDefault(r => r.Id == id);

                if (report == null)
                {
                    _logger.LogWarning(
                        "Repository delete requested for non-existent report. ReportId: {ReportId}",
                        id);

                    return;
                }

                var reporterId = report.ReporterId;
                var hadInvestigation = report.Investigation != null;
                var hadImage = !string.IsNullOrWhiteSpace(report.ImageUrl);

                _appDbContext.Reports.Remove(report);
                _appDbContext.SaveChanges();

                _logger.LogWarning(
                    "Repository deleted report. ReportId: {ReportId}; ReporterId: {ReporterId}; HadInvestigation: {HadInvestigation}; HadImage: {HadImage}",
                    id,
                    reporterId,
                    hadInvestigation,
                    hadImage);
            }
            catch (DbUpdateException ex)
            {
                LogDatabaseUpdateException(ex, "deleting report", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected repository error while deleting report. ReportId: {ReportId}",
                    id);

                throw;
            }
        }

        public bool HasUserUpvotedReport(int reportId, string userId)
        {
            try
            {
                return _appDbContext.ReportUpvotes
                    .Any(ru => ru.ReportId == reportId && ru.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Repository error while checking upvote state. ReportId: {ReportId}; UserId: {UserId}",
                    reportId,
                    userId);

                throw;
            }
        }

        public bool ToggleUpvoteReport(int reportId, string userId)
        {
            try
            {
                var report = _appDbContext.Reports
                    .FirstOrDefault(r => r.Id == reportId);

                if (report == null)
                {
                    _logger.LogWarning(
                        "Repository upvote toggle requested for non-existent report. ReportId: {ReportId}; UserId: {UserId}",
                        reportId,
                        userId);

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

                    // The ReportUpvotes table records the user's vote, while the counter is kept
                    // on Report for faster display on listing pages.
                    _appDbContext.ReportUpvotes.Add(upvote);
                    report.Upvotes++;
                    _appDbContext.SaveChanges();

                    _logger.LogInformation(
                        "Repository added report upvote. ReportId: {ReportId}; UserId: {UserId}; NewUpvoteCount: {Upvotes}",
                        reportId,
                        userId,
                        report.Upvotes);

                    return true;
                }

                _appDbContext.ReportUpvotes.Remove(existingUpvote);

                // Guard against negative counters if data has somehow become inconsistent.
                if (report.Upvotes > 0)
                {
                    report.Upvotes--;
                }

                _appDbContext.SaveChanges();

                _logger.LogInformation(
                    "Repository removed report upvote. ReportId: {ReportId}; UserId: {UserId}; NewUpvoteCount: {Upvotes}",
                    reportId,
                    userId,
                    report.Upvotes);

                return false;
            }
            catch (DbUpdateException ex)
            {
                LogDatabaseUpdateException(ex, "toggling report upvote", reportId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected repository error while toggling report upvote. ReportId: {ReportId}; UserId: {UserId}",
                    reportId,
                    userId);

                throw;
            }
        }

        public void UpdateReportStatus(int id, string status)
        {
            try
            {
                var report = _appDbContext.Reports.FirstOrDefault(r => r.Id == id);

                if (report == null)
                {
                    _logger.LogWarning(
                        "Repository status update requested for non-existent report. ReportId: {ReportId}; RequestedStatus: {Status}",
                        id,
                        status);

                    return;
                }

                var oldStatus = report.Status;

                report.Status = status;
                _appDbContext.SaveChanges();

                _logger.LogInformation(
                    "Repository updated report status. ReportId: {ReportId}; OldStatus: {OldStatus}; NewStatus: {NewStatus}",
                    id,
                    oldStatus,
                    status);
            }
            catch (DbUpdateException ex)
            {
                LogDatabaseUpdateException(ex, "updating report status", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected repository error while updating report status. ReportId: {ReportId}; RequestedStatus: {Status}",
                    id,
                    status);

                throw;
            }
        }

        public void AddOrUpdateInvestigation(
            int reportId,
            string actionTaken,
            string investigatorId,
            string investigatorEmail,
            string? investigatorPhone)
        {
            try
            {
                var report = _appDbContext.Reports
                    .Include(r => r.Investigation)
                    .FirstOrDefault(r => r.Id == reportId);

                if (report == null)
                {
                    _logger.LogWarning(
                        "Repository investigation update requested for non-existent report. ReportId: {ReportId}; InvestigatorId: {InvestigatorId}",
                        reportId,
                        investigatorId);

                    return;
                }

                if (report.Investigation == null)
                {
                    // Each report has at most one investigation entry.
                    // If no entry exists yet, create the initial investigation record.
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

                    _logger.LogInformation(
                        "Repository created investigation. ReportId: {ReportId}; InvestigatorId: {InvestigatorId}; HasInvestigatorPhone: {HasInvestigatorPhone}",
                        reportId,
                        investigatorId,
                        !string.IsNullOrWhiteSpace(investigatorPhone));
                }
                else
                {
                    // If an investigation already exists, update it instead of creating duplicates.
                    report.Investigation.ActionTaken = actionTaken;
                    report.Investigation.ActionDate = DateTime.Now;
                    report.Investigation.InvestigatorId = investigatorId;
                    report.Investigation.InvestigatorEmail = investigatorEmail;
                    report.Investigation.InvestigatorPhone = investigatorPhone;

                    _logger.LogInformation(
                        "Repository updated investigation. ReportId: {ReportId}; InvestigatorId: {InvestigatorId}; HasInvestigatorPhone: {HasInvestigatorPhone}",
                        reportId,
                        investigatorId,
                        !string.IsNullOrWhiteSpace(investigatorPhone));
                }

                _appDbContext.SaveChanges();
            }
            catch (DbUpdateException ex)
            {
                LogDatabaseUpdateException(ex, "adding or updating investigation", reportId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected repository error while adding or updating investigation. ReportId: {ReportId}; InvestigatorId: {InvestigatorId}",
                    reportId,
                    investigatorId);

                throw;
            }
        }

        private void LogDatabaseUpdateException(DbUpdateException ex, string operation, int? reportId)
        {
            var baseException = ex.GetBaseException();

            if (baseException is SqlException sqlException)
            {
                _logger.LogError(
                    ex,
                    "Database update error while {Operation}. ReportId: {ReportId}; SqlErrorNumber: {SqlErrorNumber}; SqlState: {SqlState}; SqlProcedure: {SqlProcedure}; SqlLineNumber: {SqlLineNumber}",
                    operation,
                    reportId,
                    sqlException.Number,
                    sqlException.State,
                    sqlException.Procedure,
                    sqlException.LineNumber);

                return;
            }

            _logger.LogError(
                ex,
                "Database update error while {Operation}. ReportId: {ReportId}; BaseExceptionType: {BaseExceptionType}; BaseExceptionMessage: {BaseExceptionMessage}",
                operation,
                reportId,
                baseException.GetType().Name,
                baseException.Message);
        }
    }
}