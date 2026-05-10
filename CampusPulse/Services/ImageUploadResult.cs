namespace CampusPulse.Services
{
    public class ImageUploadResult
    {
        public bool Success { get; private set; }
        public string? ImageUrl { get; private set; }
        public string? ErrorMessage { get; private set; }

        public static ImageUploadResult Succeeded(string imageUrl)
        {
            return new ImageUploadResult
            {
                Success = true,
                ImageUrl = imageUrl
            };
        }

        public static ImageUploadResult Failed(string errorMessage)
        {
            return new ImageUploadResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }
}