namespace textToImage.Web.Services.Images;

public class ImageCacheBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ImageCacheBackgroundService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(6); // Cleanup every 6 hours
    private readonly TimeSpan _maxImageAge = TimeSpan.FromDays(7); // Keep images for 7 days

    public ImageCacheBackgroundService(IServiceProvider serviceProvider, ILogger<ImageCacheBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var imageCacheService = scope.ServiceProvider.GetRequiredService<IImageCacheService>();
                
                await imageCacheService.CleanupOldImagesAsync(_maxImageAge);
                
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during image cache cleanup");
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); // Wait before retrying
            }
        }
    }
}