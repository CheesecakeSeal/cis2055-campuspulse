using CampusPulse.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CampusPulse.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Report> Reports { get; set; }
        public DbSet<Investigation> Investigations { get; set; }
        public DbSet<ReportUpvote> ReportUpvotes { get; set; }
        public DbSet<InvestigatorEmail> InvestigatorEmails { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Report>()
                .HasOne(r => r.Reporter)
                .WithMany()
                .HasForeignKey(r => r.ReporterId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Investigation>()
                .HasOne(i => i.Investigator)
                .WithMany()
                .HasForeignKey(i => i.InvestigatorId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ApplicationUser>()
            .HasIndex(u => u.NormalizedDisplayName)
            .IsUnique()
            .HasFilter("[NormalizedDisplayName] IS NOT NULL");

            builder.Entity<InvestigatorEmail>()
                .HasIndex(i => i.NormalizedEmail)
                .IsUnique();

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

            builder.Entity<ReportUpvote>()
                .HasKey(ru => new { ru.ReportId, ru.UserId });

            builder.Entity<ReportUpvote>()
                .HasOne(ru => ru.Report)
                .WithMany(r => r.ReportUpvotes)
                .HasForeignKey(ru => ru.ReportId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ReportUpvote>()
                .Property(ru => ru.UserId)
                .HasMaxLength(450);
        }
    }
}