namespace CampusPulse.Models.ViewModels
{
    public class ReporterStatsViewModel
    {
        public string DisplayName { get; set; } = string.Empty;

        public int ReportsSubmitted { get; set; }

        public int TotalUpvotes { get; set; }
    }
}