using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace SmartWorkerAutomation.DataProvider.Helper;

public static class ImageUploadHelper
{
    public static async Task<string?> UploadImageAsync(
        IFormFile file,
        string subFolder
    )
    {
        try
        {
            if (file == null || file.Length == 0)
                return null;

            var folderPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                subFolder
            );

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowedExtensions.Contains(extension))
                throw new Exception("Only JPG, JPEG, PNG, and WEBP images are allowed.");

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");

            var originalName = Path.GetFileNameWithoutExtension(file.FileName);
            var safeName = string.Concat(
                originalName.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-')
            );

            if (string.IsNullOrWhiteSpace(safeName))
                safeName = "image";

            var fileName = $"{safeName}_{timestamp}{extension}";
            var filePath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // ✅ return RELATIVE PATH
            return $"{subFolder}/{fileName}";
        }
        catch (Exception ex)
        {
            throw new Exception("Error occurred while uploading the image.", ex);
        }
    }
}
