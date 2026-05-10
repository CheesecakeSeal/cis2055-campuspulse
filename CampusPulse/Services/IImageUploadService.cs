using Microsoft.AspNetCore.Http;

namespace CampusPulse.Services
{
    public interface IImageUploadService
    {
        Task<ImageUploadResult> SaveReportImageAsync(IFormFile? imageFile);
        void DeleteImage(string? imageUrl);
    }
}