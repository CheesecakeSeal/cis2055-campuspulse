using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace CampusPulse.Models
{
    public class ApplicationUser : IdentityUser
    {
        [MaxLength(30)]
        public string? DisplayName { get; set; }

        [MaxLength(30)]
        public string? NormalizedDisplayName { get; set; }
    }
}