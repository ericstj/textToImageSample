using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using textToImage.Web.Services.Images;

namespace textToImage.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImagesController : ControllerBase
{
    private readonly IImageCacheService _imageCacheService;
    private readonly ILogger<ImagesController> _logger;

    public ImagesController(IImageCacheService imageCacheService, ILogger<ImagesController> logger)
    {
        _imageCacheService = imageCacheService;
        _logger = logger;
    }

    [HttpGet("{imageId}")]
    public async Task<IActionResult> GetImage(string imageId)
    {
        try
        {
            var cachedImage = await _imageCacheService.GetCachedImageAsync(imageId);
            
            if (cachedImage == null)
            {
                return NotFound();
            }

            var (imageBytes, contentType) = cachedImage.Value;
            
            // Set appropriate cache headers
            Response.Headers.CacheControl = "public, max-age=86400"; // 1 day
            Response.Headers.ETag = $"\"{imageId}\"";
            
            return File(imageBytes, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving image {ImageId}", imageId);
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<IActionResult> CacheImage([FromBody] CacheImageRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Base64Data) || string.IsNullOrEmpty(request.ContentType))
            {
                return BadRequest("Base64Data and ContentType are required");
            }

            var imageBytes = Convert.FromBase64String(request.Base64Data);
            var relativeImageUri = await _imageCacheService.CacheImageAsync(imageBytes, request.ContentType, request.FileName);

            // Convert relative URI to absolute URI
            var baseUri = new Uri($"{Request.Scheme}://{Request.Host}");
            return Ok(new { ImageUri = new Uri(baseUri, relativeImageUri) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching image via API");
            return StatusCode(500, "Error caching image");
        }
    }

    [HttpDelete("{imageId}")]
    public async Task<IActionResult> DeleteImage(string imageId)
    {
        try
        {
            await _imageCacheService.RemoveCachedImageAsync(imageId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image {ImageId}", imageId);
            return StatusCode(500);
        }
    }

    public record CacheImageRequest
    {
        public required string Base64Data { get; init; }
        public required string ContentType { get; init; }
        public string? FileName { get; init; }
    }
}