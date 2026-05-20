using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
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
        private readonly ILogger<ImageUploadService> _logger;

        public ImageUploadService(
            IWebHostEnvironment environment,
            ILogger<ImageUploadService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<ImageUploadResult> SaveReportImageAsync(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                _logger.LogWarning("Image upload rejected because no file was uploaded.");

                return ImageUploadResult.Failed("No image was uploaded.");
            }

            var originalFileName = imageFile.FileName;
            var fileSize = imageFile.Length;
            var contentType = imageFile.ContentType;

            if (imageFile.Length > MaxFileSizeBytes)
            {
                _logger.LogWarning(
                    "Image upload rejected because file was too large. FileName: {FileName}; FileSize: {FileSize}; MaxAllowedBytes: {MaxAllowedBytes}; ContentType: {ContentType}",
                    originalFileName,
                    fileSize,
                    MaxFileSizeBytes,
                    contentType);

                return ImageUploadResult.Failed("The image is too large. Maximum allowed size is 3 MB.");
            }

            var extension = Path.GetExtension(imageFile.FileName);

            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            {
                _logger.LogWarning(
                    "Image upload rejected because extension was not allowed. FileName: {FileName}; Extension: {Extension}; FileSize: {FileSize}; ContentType: {ContentType}",
                    originalFileName,
                    extension,
                    fileSize,
                    contentType);

                return ImageUploadResult.Failed("Only JPG, PNG, and WebP images are allowed.");
            }

            if (string.IsNullOrWhiteSpace(imageFile.ContentType) || !AllowedContentTypes.Contains(imageFile.ContentType))
            {
                _logger.LogWarning(
                    "Image upload rejected because content type was not allowed. FileName: {FileName}; Extension: {Extension}; FileSize: {FileSize}; ContentType: {ContentType}",
                    originalFileName,
                    extension,
                    fileSize,
                    contentType);

                return ImageUploadResult.Failed("Invalid image content type.");
            }

            await using var memoryStream = new MemoryStream();
            await imageFile.CopyToAsync(memoryStream);

            // Check the file signature so the app does not trust only the extension/content type.
            if (!HasAllowedImageSignature(memoryStream.ToArray(), extension))
            {
                _logger.LogWarning(
                    "Image upload rejected because file signature did not match an allowed image type. FileName: {FileName}; Extension: {Extension}; FileSize: {FileSize}; ContentType: {ContentType}",
                    originalFileName,
                    extension,
                    fileSize,
                    contentType);

                return ImageUploadResult.Failed("The uploaded file does not appear to be a valid image.");
            }

            try
            {
                memoryStream.Position = 0;

                var imageInfo = await Image.IdentifyAsync(memoryStream);

                if (imageInfo == null)
                {
                    _logger.LogWarning(
                        "Image upload rejected because ImageSharp could not identify the file as an image. FileName: {FileName}; Extension: {Extension}; FileSize: {FileSize}; ContentType: {ContentType}",
                        originalFileName,
                        extension,
                        fileSize,
                        contentType);

                    return ImageUploadResult.Failed("The uploaded file could not be read as an image.");
                }

                if (imageInfo.Width > MaxImageWidth || imageInfo.Height > MaxImageHeight)
                {
                    _logger.LogWarning(
                        "Image upload rejected because dimensions were too large. FileName: {FileName}; Width: {Width}; Height: {Height}; MaxWidth: {MaxWidth}; MaxHeight: {MaxHeight}; FileSize: {FileSize}; ContentType: {ContentType}",
                        originalFileName,
                        imageInfo.Width,
                        imageInfo.Height,
                        MaxImageWidth,
                        MaxImageHeight,
                        fileSize,
                        contentType);

                    return ImageUploadResult.Failed($"Image dimensions are too large. Maximum allowed dimensions are {MaxImageWidth}x{MaxImageHeight}.");
                }

                memoryStream.Position = 0;

                using var image = await Image.LoadAsync<Rgba32>(memoryStream);

                // Remove metadata such as EXIF/GPS data before saving the image.
                image.Metadata.ExifProfile = null;
                image.Metadata.IccProfile = null;
                image.Metadata.XmpProfile = null;

                // Flatten transparency onto a white background because the final stored format is JPG.
                using var flattenedImage = new Image<Rgba32>(image.Width, image.Height, Color.White);

                flattenedImage.Mutate(x =>
                {
                    x.DrawImage(image, new Point(0, 0), 1f);

                    // Resize large images to a consistent maximum display size.
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

                // Store the image using an application-generated filename.
                // This avoids trusting user-supplied filenames.
                var safeFileName = $"{Guid.NewGuid():N}.jpg";
                var physicalPath = Path.Combine(uploadsFolder, safeFileName);

                // Re-encode to a new JPG so the original uploaded bytes are not stored directly.
                await flattenedImage.SaveAsJpegAsync(physicalPath, new JpegEncoder
                {
                    Quality = 85
                });

                var imageUrl = $"/uploads/report-images/{safeFileName}";

                _logger.LogInformation(
                    "Image upload processed successfully. OriginalFileName: {OriginalFileName}; StoredFileName: {StoredFileName}; OriginalExtension: {Extension}; OriginalFileSize: {FileSize}; OriginalContentType: {ContentType}; OriginalWidth: {OriginalWidth}; OriginalHeight: {OriginalHeight}; OutputMaxWidth: {OutputMaxWidth}; OutputMaxHeight: {OutputMaxHeight}",
                    originalFileName,
                    safeFileName,
                    extension,
                    fileSize,
                    contentType,
                    imageInfo.Width,
                    imageInfo.Height,
                    MaxOutputWidth,
                    MaxOutputHeight);

                return ImageUploadResult.Succeeded(imageUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Image upload failed during processing. FileName: {FileName}; Extension: {Extension}; FileSize: {FileSize}; ContentType: {ContentType}",
                    originalFileName,
                    extension,
                    fileSize,
                    contentType);

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

            // Only delete images from the controlled report image folder.
            // This avoids deleting arbitrary files if a malicious or malformed URL is passed in.
            if (!normalizedUrl.StartsWith("/uploads/report-images/", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Image deletion skipped because URL was outside the allowed upload folder. ImageUrl: {ImageUrl}",
                    imageUrl);

                return;
            }

            var fileName = Path.GetFileName(normalizedUrl);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                _logger.LogWarning(
                    "Image deletion skipped because no file name could be extracted. ImageUrl: {ImageUrl}",
                    imageUrl);

                return;
            }

            var physicalPath = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "report-images",
                fileName
            );

            try
            {
                if (File.Exists(physicalPath))
                {
                    File.Delete(physicalPath);

                    _logger.LogInformation(
                        "Image deleted successfully. ImageUrl: {ImageUrl}; FileName: {FileName}",
                        imageUrl,
                        fileName);
                }
                else
                {
                    _logger.LogWarning(
                        "Image deletion requested but file did not exist. ImageUrl: {ImageUrl}; FileName: {FileName}",
                        imageUrl,
                        fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Image deletion failed. ImageUrl: {ImageUrl}; FileName: {FileName}",
                    imageUrl,
                    fileName);

                throw;
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