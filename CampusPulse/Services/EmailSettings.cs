namespace CampusPulse.Services
{
    public class EmailSettings
    {
        public bool Enabled { get; set; } = false;

        public string FromName { get; set; } = "CampusPulse";

        public string FromAddress { get; set; } = "noreply@campuspulse.local";

        public string SmtpHost { get; set; } = string.Empty;

        public int SmtpPort { get; set; } = 587;

        public bool UseStartTls { get; set; } = true;

        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}