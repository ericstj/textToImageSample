using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace textToImage.Web.Services.Images;

public class ImageCacheService : IImageCacheService, IDisposable
{
    private readonly ILogger<ImageCacheService> _logger;
    private readonly string _cacheDirectory;
    private readonly ConcurrentDictionary<string, CachedImageMetadata> _imageMetadata = new();

    public ImageCacheService(ILogger<ImageCacheService> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _cacheDirectory = Path.Combine(environment.ContentRootPath, "ImageCache");
        
        // Ensure cache directory exists
        Directory.CreateDirectory(_cacheDirectory);
        
        // Load existing metadata on startup
        LoadExistingMetadata();
    }

    public async Task<string> CacheImageAsync(byte[] imageBytes, string contentType, string? fileName = null)
    {
        try
        {
            // Generate a unique identifier for the image
            var imageId = GenerateImageId(imageBytes);
            
            // Check if image already exists
            if (_imageMetadata.ContainsKey(imageId))
            {
                _logger.LogDebug("Image {ImageId} already cached", imageId);
                return $"/api/images/{imageId}";
            }

            // Determine file extension from content type
            var extension = GetFileExtension(contentType);
            var filePath = Path.Combine(_cacheDirectory, $"{imageId}{extension}");

            // Save image to disk
            await File.WriteAllBytesAsync(filePath, imageBytes);

            // Store metadata
            var metadata = new CachedImageMetadata
            {
                ImageId = imageId,
                ContentType = contentType,
                FileName = fileName,
                FilePath = filePath,
                CreatedAt = DateTime.UtcNow,
                FileSize = imageBytes.Length
            };

            _imageMetadata[imageId] = metadata;

            _logger.LogInformation("Cached image {ImageId} ({FileSize} bytes)", imageId, imageBytes.Length);
            
            return $"/api/images/{imageId}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching image");
            throw;
        }
    }

    public async Task<(byte[] imageBytes, string contentType)?> GetCachedImageAsync(string imageId)
    {
        try
        {
            if (!_imageMetadata.TryGetValue(imageId, out var metadata))
            {
                _logger.LogWarning("Image {ImageId} not found in cache", imageId);
                return null;
            }

            if (!File.Exists(metadata.FilePath))
            {
                _logger.LogWarning("Image file {FilePath} not found on disk", metadata.FilePath);
                _imageMetadata.TryRemove(imageId, out _);
                return null;
            }

            var imageBytes = await File.ReadAllBytesAsync(metadata.FilePath);
            return (imageBytes, metadata.ContentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cached image {ImageId}", imageId);
            return null;
        }
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async Task RemoveCachedImageAsync(string imageId)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        try
        {
            if (_imageMetadata.TryRemove(imageId, out var metadata))
            {
                if (File.Exists(metadata.FilePath))
                {
                    File.Delete(metadata.FilePath);
                }
                
                _logger.LogInformation("Removed cached image {ImageId}", imageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cached image {ImageId}", imageId);
        }
    }

    public async Task CleanupOldImagesAsync(TimeSpan maxAge)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow - maxAge;
            var imagesToRemove = _imageMetadata
                .Where(kvp => kvp.Value.CreatedAt < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var imageId in imagesToRemove)
            {
                await RemoveCachedImageAsync(imageId);
            }

            _logger.LogInformation("Cleaned up {Count} old cached images", imagesToRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache cleanup");
        }
    }

    private string GenerateImageId(byte[] imageBytes)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(imageBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetFileExtension(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            "image/tiff" => ".tiff",
            _ => ".bin"
        };
    }

    private void LoadExistingMetadata()
    {
        try
        {
            if (!Directory.Exists(_cacheDirectory))
                return;

            var files = Directory.GetFiles(_cacheDirectory);
            foreach (var filePath in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var extension = Path.GetExtension(filePath);
                
                if (string.IsNullOrEmpty(fileName))
                    continue;

                var contentType = GetContentTypeFromExtension(extension);
                var fileInfo = new FileInfo(filePath);

                var metadata = new CachedImageMetadata
                {
                    ImageId = fileName,
                    ContentType = contentType,
                    FilePath = filePath,
                    CreatedAt = fileInfo.CreationTimeUtc,
                    FileSize = fileInfo.Length
                };

                _imageMetadata[fileName] = metadata;
            }

            _logger.LogInformation("Loaded {Count} existing cached images", _imageMetadata.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading existing image metadata");
        }
    }

    private static string GetContentTypeFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".tiff" => "image/tiff",
            _ => "application/octet-stream"
        };
    }

    public void Dispose()
    {
        _imageMetadata.Clear();
        try
        {
            Directory.Delete(_cacheDirectory, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing ImageCacheService");
        }
    }

    private record CachedImageMetadata
    {
        public required string ImageId { get; init; }
        public required string ContentType { get; init; }
        public string? FileName { get; init; }
        public required string FilePath { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required long FileSize { get; init; }
    }
}