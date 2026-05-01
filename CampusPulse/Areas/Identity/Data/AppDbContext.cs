using CampusPulse.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CampusPulse.Data
{
    public class AppDbContext : IdentityDbContext<IdentityUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Report> Reports { get; set; }
        public DbSet<Investigation> Investigations { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Report>()
                .HasOne(r => r.Investigation)
                .WithOne(i => i.Report)
                .HasForeignKey<Investigation>(i => i.ReportId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Report>()
                .Property(r => r.Status)
                .HasDefaultValue("Open");

            builder.Entity<Report>()
                .Property(r => r.Upvotes)
                .HasDefaultValue(0);
        }
    }
}