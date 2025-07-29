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
        using var memoryStream = new MemoryStream(imageBytes);
        return await CacheImageFromStreamInternalAsync(memoryStream, contentType, fileName);
    }

    public async Task<string> CacheImageAsync(Stream imageStream, string contentType, string? fileName = null)
    {
        return await CacheImageFromStreamInternalAsync(imageStream, contentType, fileName);
    }

    private async Task<string> CacheImageFromStreamInternalAsync(Stream imageStream, string contentType, string? fileName = null)
    {
        try
        {
            // Generate a temporary file path
            var tempFileName = $"temp_{Guid.NewGuid():N}";
            var extension = GetFileExtension(contentType);
            var tempFilePath = Path.Combine(_cacheDirectory, $"{tempFileName}{extension}");

            string imageId;
            long fileSize;

            // Stream the image to a temporary file while calculating the hash
            using (var sha256 = SHA256.Create())
            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
            using (var cryptoStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Write))
            {
                await imageStream.CopyToAsync(cryptoStream);
                cryptoStream.FlushFinalBlock();
                
                // Get the computed hash
                var hash = sha256.Hash!;
                imageId = Convert.ToHexString(hash).ToLowerInvariant();
                
                // Get file size
                fileSize = fileStream.Length;
            }

            // Check if image already exists after calculating the hash
            if (_imageMetadata.ContainsKey(imageId))
            {
                _logger.LogDebug("Image {ImageId} already cached, removing temporary file", imageId);
                // Clean up temporary file since we already have this image
                File.Delete(tempFilePath);
                return $"/api/images/{imageId}";
            }

            // Rename temporary file to final name
            var finalFilePath = Path.Combine(_cacheDirectory, $"{imageId}{extension}");
            
            // Handle case where target file might already exist from concurrent operations
            if (File.Exists(finalFilePath))
            {
                File.Delete(tempFilePath);
                _logger.LogDebug("Image {ImageId} was cached by concurrent operation", imageId);
            }
            else
            {
                File.Move(tempFilePath, finalFilePath);
            }

            // Store metadata only if we don't already have it
            if (!_imageMetadata.ContainsKey(imageId))
            {
                var metadata = new CachedImageMetadata
                {
                    ImageId = imageId,
                    ContentType = contentType,
                    FileName = fileName,
                    FilePath = finalFilePath,
                    CreatedAt = DateTime.UtcNow,
                    FileSize = fileSize
                };

                _imageMetadata[imageId] = metadata;
                _logger.LogInformation("Cached image {ImageId} ({FileSize} bytes)", imageId, fileSize);
            }

            return $"/api/images/{imageId}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching image from stream");
            throw;
        }
    }

    public async Task<(byte[] imageBytes, string contentType)?> GetCachedImageAsync(string imageIdOrUri)
    {
        try
        {
            string imageId = imageIdOrUri;

            if (Uri.TryCreate(imageIdOrUri, UriKind.RelativeOrAbsolute, out var uri))
            {
                var uriPath = uri.IsAbsoluteUri ? uri.LocalPath : uri.OriginalString;
                if (uriPath.StartsWith("/api/images/", StringComparison.OrdinalIgnoreCase))
                {
                    imageId = Path.GetFileNameWithoutExtension(uriPath);
                }
            }

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
            _logger.LogError(ex, "Error retrieving cached image {ImageIdOrUri}", imageIdOrUri);
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

            // Also clean up any temporary files that might have been left behind
            await CleanupTemporaryFilesAsync();

            _logger.LogInformation("Cleaned up {Count} old cached images", imagesToRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache cleanup");
        }
    }

    private async Task CleanupTemporaryFilesAsync()
    {
        try
        {
            var tempFiles = Directory.GetFiles(_cacheDirectory, "temp_*")
                .Where(file => File.GetCreationTime(file) < DateTime.Now.AddHours(-1)) // Remove temp files older than 1 hour
                .ToList();

            foreach (var tempFile in tempFiles)
            {
                try
                {
                    File.Delete(tempFile);
                    _logger.LogDebug("Cleaned up temporary file {TempFile}", tempFile);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary file {TempFile}", tempFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during temporary files cleanup");
        }
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

            var files = Directory.GetFiles(_cacheDirectory)
                .Where(file => !Path.GetFileName(file).StartsWith("temp_")) // Skip temporary files
                .ToArray();

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