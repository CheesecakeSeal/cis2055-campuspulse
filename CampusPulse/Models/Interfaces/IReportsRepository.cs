namespace CampusPulse.Models.Interfaces
{
    public interface IReportsRepository
    {
        IEnumerable<Report> GetAllReports();
        Report? GetReportById(int id);
        void CreateReport(Report report);
        void UpdateReport(Report report);
        void DeleteReport(int id);

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