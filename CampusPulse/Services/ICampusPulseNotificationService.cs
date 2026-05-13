using CampusPulse.Models;

namespace CampusPulse.Services
{
    public interface ICampusPulseNotificationService
    {
        Task NotifyInvestigatorsOfNewReportAsync(Report report, string reportUrl);

        Task NotifyReporterOfStatusChangeAsync(Report report, string reportUrl);

        Task NotifyReporterOfInvestigationUpdateAsync(Report report, string reportUrl);
    }
}