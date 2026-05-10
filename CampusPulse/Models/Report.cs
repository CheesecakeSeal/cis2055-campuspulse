using System.ComponentModel.DataAnnotations;

namespace CampusPulse.Models
{
    public class Report
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public DateTime Date_Reported { get; set; } = DateTime.Now;

        [Required]
        public string Location { get; set; } = string.Empty;

        [Required]
        public DateTime Time_Observed { get; set; }

        [Required]
        public string Category { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public string Status { get; set; } = "Open";

        public string? ReporterId { get; set; }

        public string ReporterEmail { get; set; } = string.Empty;

        public string? ReporterPhone { get; set; }

        public string? ImageUrl { get; set; }

        public int Upvotes { get; set; } = 0;

        public virtual Investigation? Investigation { get; set; }

        public virtual ICollection<ReportUpvote> ReportUpvotes { get; set; } = new List<ReportUpvote>();
    }
}