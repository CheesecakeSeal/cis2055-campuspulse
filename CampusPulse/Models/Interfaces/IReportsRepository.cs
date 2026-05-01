namespace CampusPulse.Models.Interfaces
{
    public interface IReportsRepository
    {
        IEnumerable<Report> GetAllReports();

        Report? GetReportById(int id);

        void CreateReport(Report report);

        void UpvoteReport(int id);

        void UpdateReportStatus(int id, string status);

        void AddOrUpdateInvestigation(
            int reportId,
            string actionTaken,
            string investigatorEmail,
            string? investigatorPhone
        );
    }
}