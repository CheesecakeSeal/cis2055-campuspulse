using CampusPulse.Data;
using CampusPulse.Models;

namespace CampusPulse.Services
{
    public class ReportActivityService : IReportActivityService
    {
        private readonly AppDbContext _context;

        public ReportActivityService(AppDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(
            int reportId,
            string actionType,
            string description,
            string? actorId,
            string actorDisplayName)
        {
            var activity = new ReportActivity
            {
                ReportId = reportId,
                ActionType = actionType,
                Description = description,
                ActorId = actorId,
                ActorDisplayName = string.IsNullOrWhiteSpace(actorDisplayName)
                    ? "Unknown user"
                    : actorDisplayName,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.ReportActivities.Add(activity);
            await _context.SaveChangesAsync();
        }
    }
}