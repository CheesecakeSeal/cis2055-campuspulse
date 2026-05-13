namespace CampusPulse.Services
{
    public interface IReportActivityService
    {
        Task LogAsync(
            int reportId,
            string actionType,
            string description,
            string? actorId,
            string actorDisplayName);
    }
}