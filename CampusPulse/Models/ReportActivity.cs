using System.ComponentModel.DataAnnotations;

namespace CampusPulse.Models
{
    public class ReportActivity
    {
        public int Id { get; set; }

        public int ReportId { get; set; }

        public virtual Report Report { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string ActionType { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        public string? ActorId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ActorDisplayName { get; set; } = "System";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}