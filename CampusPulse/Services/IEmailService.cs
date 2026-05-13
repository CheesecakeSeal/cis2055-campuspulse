namespace CampusPulse.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(
            IEnumerable<string> recipients,
            string subject,
            string htmlBody);
    }
}