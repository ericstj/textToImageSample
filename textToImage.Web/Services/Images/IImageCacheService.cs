namespace textToImage.Web.Services.Images;

public interface IImageCacheService
{
    /// <summary>
    /// Stores image bytes and returns a URI to access the cached image
    /// </summary>
    /// <param name="imageBytes">The image data to cache</param>
    /// <param name="contentType">The MIME type of the image (e.g., "image/png", "image/jpeg")</param>
    /// <param name="fileName">Optional filename for the cached image</param>
    /// <returns>A URI that can be used to retrieve the cached image</returns>
    Task<string> CacheImageAsync(byte[] imageBytes, string contentType, string? fileName = null);
    /// <summary>
    /// Stores image stream and returns a URI to access the cached image
    /// </summary>
    /// <param name="imageStream">The image data to cache</param>
    /// <param name="contentType">The MIME type of the image (e.g., "image/png", "image/jpeg")</param>
    /// <param name="fileName">Optional filename for the cached image</param>
    /// <returns>A URI that can be used to retrieve the cached image</returns>
    Task<string> CacheImageAsync(Stream imageStream, string contentType, string? fileName = null);
    /// <summary>
    /// Retrieves cached image data
    /// </summary>
    /// <param name="imageIdOrUri">The unique identifier for the cached image, or Uri for the image</param>
    /// <returns>The cached image data and content type, or null if not found</returns>
    Task<(byte[] imageBytes, string contentType)?> GetCachedImageAsync(string imageIdOrUri);

    /// <summary>
    /// Removes a cached image
    /// </summary>
    /// <param name="imageId">The unique identifier for the cached image</param>
    Task RemoveCachedImageAsync(string imageId);

    /// <summary>
    /// Clears all cached images older than the specified age
    /// </summary>
    /// <param name="maxAge">Maximum age of images to keep</param>
    Task CleanupOldImagesAsync(TimeSpan maxAge);
}