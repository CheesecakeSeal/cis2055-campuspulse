using System.ComponentModel.DataAnnotations;

namespace CampusPulse.Models
{
    public class InvestigatorEmail
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string NormalizedEmail { get; set; } = string.Empty;
    }
}