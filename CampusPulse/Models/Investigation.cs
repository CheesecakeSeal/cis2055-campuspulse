using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusPulse.Models
{
    public class Investigation
    {
        [Key, ForeignKey(nameof(Report))]
        public int ReportId { get; set; }

        [Required]
        public string ActionTaken { get; set; } = string.Empty;

        public DateTime ActionDate { get; set; } = DateTime.Now;

        public string? InvestigatorId { get; set; }

        [Required]
        [EmailAddress]
        public string InvestigatorEmail { get; set; } = string.Empty;

        public string? InvestigatorPhone { get; set; }

        public virtual Report Report { get; set; } = null!;

        public virtual ApplicationUser? Investigator { get; set; }
    }
}