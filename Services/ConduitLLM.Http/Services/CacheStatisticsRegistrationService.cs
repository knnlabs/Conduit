using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Background service that registers the cache statistics collector instance on startup.
    /// </summary>
    public class CacheStatisticsRegistrationService : BackgroundService
    {
        private readonly IDistributedCacheStatisticsCollector? _distributedCollector;
        private readonly ILogger<CacheStatisticsRegistrationService> _logger;

        public CacheStatisticsRegistrationService(
            ICacheStatisticsCollector cacheStatisticsCollector,
            ILogger<CacheStatisticsRegistrationService> logger)
        {
            _distributedCollector = cacheStatisticsCollector as IDistributedCacheStatisticsCollector;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_distributedCollector == null)
            {
                _logger.LogInformation("Cache statistics collector is not distributed. Registration not required.");
                return;
            }

            try
            {
                await _distributedCollector.RegisterInstanceAsync(stoppingToken);
                _logger.LogInformation("Successfully registered distributed cache statistics collector instance: {InstanceId}", 
                    _distributedCollector.InstanceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register distributed cache statistics collector instance");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_distributedCollector != null)
            {
                try
                {
                    await _distributedCollector.UnregisterInstanceAsync(cancellationToken);
                    _logger.LogInformation("Successfully unregistered distributed cache statistics collector instance: {InstanceId}", 
                        _distributedCollector.InstanceId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to unregister distributed cache statistics collector instance");
                }
            }

            await base.StopAsync(cancellationToken);
        }
    }
}