using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;

namespace CampusPulse.Services
{
    public class ImageUploadService : IImageUploadService
    {
        private const long MaxFileSizeBytes = 3 * 1024 * 1024;
        private const int MaxImageWidth = 4000;
        private const int MaxImageHeight = 4000;
        private const int MaxOutputWidth = 1600;
        private const int MaxOutputHeight = 1600;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        private readonly IWebHostEnvironment _environment;

        public ImageUploadService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<ImageUploadResult> SaveReportImageAsync(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return ImageUploadResult.Failed("No image was uploaded.");
            }

            if (imageFile.Length > MaxFileSizeBytes)
            {
                return ImageUploadResult.Failed("The image is too large. Maximum allowed size is 2 MB.");
            }

            var extension = Path.GetExtension(imageFile.FileName);

            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            {
                return ImageUploadResult.Failed("Only JPG, PNG, and WebP images are allowed.");
            }

            if (string.IsNullOrWhiteSpace(imageFile.ContentType) || !AllowedContentTypes.Contains(imageFile.ContentType))
            {
                return ImageUploadResult.Failed("Invalid image content type.");
            }

            await using var memoryStream = new MemoryStream();
            await imageFile.CopyToAsync(memoryStream);

            if (!HasAllowedImageSignature(memoryStream.ToArray(), extension))
            {
                return ImageUploadResult.Failed("The uploaded file does not appear to be a valid image.");
            }

            try
            {
                memoryStream.Position = 0;

                var imageInfo = await Image.IdentifyAsync(memoryStream);

                if (imageInfo == null)
                {
                    return ImageUploadResult.Failed("The uploaded file could not be read as an image.");
                }

                if (imageInfo.Width > MaxImageWidth || imageInfo.Height > MaxImageHeight)
                {
                    return ImageUploadResult.Failed($"Image dimensions are too large. Maximum allowed dimensions are {MaxImageWidth}x{MaxImageHeight}.");
                }

                memoryStream.Position = 0;

                using var image = await Image.LoadAsync<Rgba32>(memoryStream);

                image.Metadata.ExifProfile = null;
                image.Metadata.IccProfile = null;
                image.Metadata.XmpProfile = null;

                using var flattenedImage = new Image<Rgba32>(image.Width, image.Height, Color.White);

                flattenedImage.Mutate(x =>
                {
                    x.DrawImage(image, new Point(0, 0), 1f);

                    x.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(MaxOutputWidth, MaxOutputHeight)
                    });
                });

                var uploadsFolder = Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "report-images"
                );

                Directory.CreateDirectory(uploadsFolder);

                var safeFileName = $"{Guid.NewGuid():N}.jpg";
                var physicalPath = Path.Combine(uploadsFolder, safeFileName);

                await flattenedImage.SaveAsJpegAsync(physicalPath, new JpegEncoder
                {
                    Quality = 85
                });

                var imageUrl = $"/uploads/report-images/{safeFileName}";

                return ImageUploadResult.Succeeded(imageUrl);
            }
            catch
            {
                return ImageUploadResult.Failed("The uploaded image could not be processed.");
            }
        }

        public void DeleteImage(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return;
            }

            var normalizedUrl = imageUrl.Replace('\\', '/');

            if (!normalizedUrl.StartsWith("/uploads/report-images/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fileName = Path.GetFileName(normalizedUrl);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            var physicalPath = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "report-images",
                fileName
            );

            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }
        }

        private static bool HasAllowedImageSignature(byte[] fileBytes, string extension)
        {
            if (fileBytes.Length < 12)
            {
                return false;
            }

            if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return fileBytes[0] == 0xFF &&
                       fileBytes[1] == 0xD8 &&
                       fileBytes[2] == 0xFF;
            }

            if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                byte[] pngSignature =
                {
                    0x89, 0x50, 0x4E, 0x47,
                    0x0D, 0x0A, 0x1A, 0x0A
                };

                return fileBytes.Take(8).SequenceEqual(pngSignature);
            }

            if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
            {
                var riff = Encoding.ASCII.GetString(fileBytes, 0, 4);
                var webp = Encoding.ASCII.GetString(fileBytes, 8, 4);

                return riff == "RIFF" && webp == "WEBP";
            }

            return false;
        }
    }
}