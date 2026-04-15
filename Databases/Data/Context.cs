using Microsoft.EntityFrameworkCore;
using CampusPulse.Models;

namespace CampusPulse.Data
{
    public class CampusPulseContext : DbContext
    {
        public CampusPulseContext(DbContextOptions<CampusPulseContext> options) : base(options) { }

        public DbSet<Report> Reports { get; set; }
        public DbSet<Investigation> Investigations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            [cite_start]//Ensures the 1-1 relationship between report and investigation
            modelBuilder.Entity<Report>()
                .HasOne(r => r.Investigation)
                .WithOne(i => i.Report)
                .HasForeignKey<Investigation>(i => i.ReportId);
        }
    }
}