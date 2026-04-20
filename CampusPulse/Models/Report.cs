using System.ComponentModel.DataAnnotations;

namespace CampusPulse.Models
{
    public class Report
    {
        public int Id { get; set; }
        
        [Required]
        public string Title { get; set; }
        
        public DateTime Date_Reported { get; set; } = DateTime.Now;
        
        [Required]
        public string Location { get; set; }
        
        [Required]
        public DateTime Time_Observed { get; set; }
        
        [Required]
        public string Category { get; set; }
        
        [Required]
        public string Description { get; set; }
        
        public string Status { get; set; } = "Open"; //Defaults to Open
        
        public string ReporterEmail { get; set; } 
        
        public string? ReporterPhone { get; set; } 
        
        public string? ImageUrl { get; set; } 
        public int Upvotes { get; set; } = 0; 

        public virtual Investigation? Investigation { get; set; } //Keeps relation 1-1
    }
}