using CampusPulse.Models;
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
        public DbSet<ReportActivity> ReportActivities { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ReportActivity stores an audit-style timeline of important changes.
            // If a report is deleted, its activity log is deleted with it.
            builder.Entity<ReportActivity>()
                .HasOne(a => a.Report)
                .WithMany(r => r.Activities)
                .HasForeignKey(a => a.ReportId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ReportActivity>()
                .Property(a => a.ActionType)
                .HasMaxLength(50);

            builder.Entity<ReportActivity>()
                .Property(a => a.Description)
                .HasMaxLength(500);

            builder.Entity<ReportActivity>()
                .Property(a => a.ActorDisplayName)
                .HasMaxLength(100);

            // Reports keep a link to the user who submitted them.
            // SetNull preserves the report if the user account is deleted/anonymised.
            builder.Entity<Report>()
                .HasOne(r => r.Reporter)
                .WithMany()
                .HasForeignKey(r => r.ReporterId)
                .OnDelete(DeleteBehavior.SetNull);

            // Investigations keep a link to the investigator who last updated them.
            // SetNull prevents investigation history being deleted with the account.
            builder.Entity<Investigation>()
                .HasOne(i => i.Investigator)
                .WithMany()
                .HasForeignKey(i => i.InvestigatorId)
                .OnDelete(DeleteBehavior.SetNull);

            // Public display names must be unique when present.
            // Existing/null users are allowed so older accounts do not break during migration.
            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.NormalizedDisplayName)
                .IsUnique()
                .HasFilter("[NormalizedDisplayName] IS NOT NULL");

            // Investigator access is controlled through an email allowlist
            builder.Entity<InvestigatorEmail>()
                .HasIndex(i => i.NormalizedEmail)
                .IsUnique();

            // One report can have at most one investigation entry.
            builder.Entity<Report>()
                .HasOne(r => r.Investigation)
                .WithOne(i => i.Report)
                .HasForeignKey<Investigation>(i => i.ReportId)
                .OnDelete(DeleteBehavior.Cascade);

            // Report defaults.
            builder.Entity<Report>()
                .Property(r => r.Status)
                .HasDefaultValue("Open");

            builder.Entity<Report>()
                .Property(r => r.Upvotes)
                .HasDefaultValue(0);

            // Composite key ensures a user can only upvote the same report once.
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