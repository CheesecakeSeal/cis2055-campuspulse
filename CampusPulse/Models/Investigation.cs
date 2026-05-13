using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusPulse.Models
{
    public class Investigation
    {
        [Key, ForeignKey("Report")]
        public int ReportId { get; set; }
        
        [Required]
        public string ActionTaken { get; set; }
        
        public DateTime ActionDate { get; set; } = DateTime.Now;
        
        [EmailAddress]
        public string InvestigatorEmail { get; set; }
        
        public string? InvestigatorPhone { get; set; }

        public virtual Report Report { get; set; }

        public string? InvestigatorId { get; set; }

        public virtual ApplicationUser? Investigator { get; set; }
    }
}