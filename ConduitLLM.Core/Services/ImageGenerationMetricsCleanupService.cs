using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Background service that periodically cleans up old image generation metrics.
    /// </summary>
    public class ImageGenerationMetricsCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ImageGenerationPerformanceConfiguration _config;
        private readonly ILogger<ImageGenerationMetricsCleanupService> _logger;
        
        public ImageGenerationMetricsCleanupService(
            IServiceProvider serviceProvider,
            IOptions<ImageGenerationPerformanceConfiguration> options,
            ILogger<ImageGenerationMetricsCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _config = options.Value;
            _logger = logger;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.EnableMetricsCollection)
            {
                _logger.LogInformation("Image generation metrics collection is disabled. Cleanup service will not run.");
                return;
            }
            
            _logger.LogInformation("Image generation metrics cleanup service started");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromHours(_config.MetricsCleanupIntervalHours), stoppingToken);
                    
                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    
                    await CleanupOldMetricsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation, just exit
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during metrics cleanup");
                }
            }
            
            _logger.LogInformation("Image generation metrics cleanup service stopped");
        }
        
        private async Task CleanupOldMetricsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var metricsService = scope.ServiceProvider.GetRequiredService<IImageGenerationMetricsService>();
            
            _logger.LogInformation("Starting cleanup of image generation metrics older than {Days} days", 
                _config.MetricsRetentionDays);
            
            try
            {
                var removedCount = await metricsService.CleanupOldMetricsAsync(
                    _config.MetricsRetentionDays, 
                    cancellationToken);
                
                _logger.LogInformation("Cleaned up {Count} old image generation metrics", removedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old metrics");
            }
        }
    }
}