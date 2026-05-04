using CampusPulse.Models;

namespace CampusPulse.Models.ViewModels
{
    public class ReportDetailsViewModel
    {
        public Report Report { get; set; } = new Report();

        public bool IsAuthenticated { get; set; }

        public bool IsOwner { get; set; }

        public bool IsInvestigator { get; set; }

        public bool HasUpvoted { get; set; }

        public List<string> StatusOptions { get; set; } = new()
        {
            "Open",
            "Being Investigated",
            "Resolved",
            "No Action Required"
        };
    }
}