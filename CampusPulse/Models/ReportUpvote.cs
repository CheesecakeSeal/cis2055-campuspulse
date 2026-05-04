using System.ComponentModel.DataAnnotations;

namespace CampusPulse.Models
{
    public class ReportUpvote
    {
        public int ReportId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual Report Report { get; set; } = null!;
    }
}