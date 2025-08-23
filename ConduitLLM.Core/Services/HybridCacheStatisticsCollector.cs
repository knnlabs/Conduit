using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Hybrid cache statistics collector that supports both local and distributed modes.
    /// </summary>
    public class HybridCacheStatisticsCollector : IDistributedCacheStatisticsCollector
    {
        private readonly ICacheStatisticsCollector? _localCollector;
        private readonly IDistributedCacheStatisticsCollector? _distributedCollector;
        private readonly ILogger<HybridCacheStatisticsCollector> _logger;
        private readonly bool _isDistributed;

        public string InstanceId => _distributedCollector?.InstanceId ?? "local";

        public event EventHandler<CacheStatisticsUpdatedEventArgs>? StatisticsUpdated;
        public event EventHandler<CacheAlertEventArgs>? AlertTriggered;
        public event EventHandler<DistributedCacheStatisticsEventArgs>? DistributedStatisticsUpdated;

        /// <summary>
        /// Creates a hybrid collector that automatically selects between local and distributed modes.
        /// </summary>
        public HybridCacheStatisticsCollector(
            ICacheStatisticsCollector? localCollector,
            IDistributedCacheStatisticsCollector? distributedCollector,
            ILogger<HybridCacheStatisticsCollector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (distributedCollector != null)
            {
                _distributedCollector = distributedCollector;
                _isDistributed = true;
                
                // Subscribe to distributed events
                _distributedCollector.StatisticsUpdated += OnStatisticsUpdated;
                _distributedCollector.AlertTriggered += OnAlertTriggered;
                _distributedCollector.DistributedStatisticsUpdated += OnDistributedStatisticsUpdated;
                
                _logger.LogInformation("HybridCacheStatisticsCollector initialized in distributed mode with instance {InstanceId}", 
                    _distributedCollector.InstanceId);
            }
            else if (localCollector != null)
            {
                _localCollector = localCollector;
                _isDistributed = false;
                
                // Subscribe to local events
                _localCollector.StatisticsUpdated += OnStatisticsUpdated;
                _localCollector.AlertTriggered += OnAlertTriggered;
                
                _logger.LogInformation("HybridCacheStatisticsCollector initialized in local mode");
            }
            else
            {
                throw new ArgumentException("At least one collector (local or distributed) must be provided");
            }
        }

        public async Task RecordOperationAsync(CacheOperation operation, CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                await _distributedCollector.RecordOperationAsync(operation, cancellationToken);
            }
            else if (_localCollector != null)
            {
                await _localCollector.RecordOperationAsync(operation, cancellationToken);
            }
        }

        public async Task RecordOperationBatchAsync(IEnumerable<CacheOperation> operations, CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                await _distributedCollector.RecordOperationBatchAsync(operations, cancellationToken);
            }
            else if (_localCollector != null)
            {
                await _localCollector.RecordOperationBatchAsync(operations, cancellationToken);
            }
        }

        public async Task<CacheStatistics> GetStatisticsAsync(CacheRegion region, CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                // In distributed mode, return aggregated statistics by default
                return await _distributedCollector.GetAggregatedStatisticsAsync(region, cancellationToken);
            }
            else if (_localCollector != null)
            {
                return await _localCollector.GetStatisticsAsync(region, cancellationToken);
            }
            
            return new CacheStatistics { Region = region };
        }

        public async Task<Dictionary<CacheRegion, CacheStatistics>> GetAllStatisticsAsync(CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                // In distributed mode, return aggregated statistics by default
                return await _distributedCollector.GetAllAggregatedStatisticsAsync(cancellationToken);
            }
            else if (_localCollector != null)
            {
                return await _localCollector.GetAllStatisticsAsync(cancellationToken);
            }
            
            return new Dictionary<CacheRegion, CacheStatistics>();
        }

        public async Task<CacheStatistics> GetAggregatedStatisticsAsync(CacheRegion region, CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                return await _distributedCollector.GetAggregatedStatisticsAsync(region, cancellationToken);
            }
            else if (_localCollector != null)
            {
                // In local mode, aggregated is the same as regular statistics
                return await _localCollector.GetStatisticsAsync(region, cancellationToken);
            }
            
            return new CacheStatistics { Region = region };
        }

        public async Task<Dictionary<CacheRegion, CacheStatistics>> GetAllAggregatedStatisticsAsync(CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                return await _distributedCollector.GetAllAggregatedStatisticsAsync(cancellationToken);
            }
            else if (_localCollector != null)
            {
                // In local mode, aggregated is the same as regular statistics
                return await _localCollector.GetAllStatisticsAsync(cancellationToken);
            }
            
            return new Dictionary<CacheRegion, CacheStatistics>();
        }

        public async Task<Dictionary<string, CacheStatistics>> GetPerInstanceStatisticsAsync(CacheRegion region, CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                return await _distributedCollector.GetPerInstanceStatisticsAsync(region, cancellationToken);
            }
            else if (_localCollector != null)
            {
                // In local mode, return single instance statistics
                var stats = await _localCollector.GetStatisticsAsync(region, cancellationToken);
                return new Dictionary<string, CacheStatistics> { ["local"] = stats };
            }
            
            return new Dictionary<string, CacheStatistics>();
        }

        public async Task<IEnumerable<string>> GetActiveInstancesAsync(CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                return await _distributedCollector.GetActiveInstancesAsync(cancellationToken);
            }
            
            // In local mode, only one instance
            return new[] { "local" };
        }

        public async Task SynchronizeAsync(CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                await _distributedCollector.SynchronizeAsync(cancellationToken);
            }
            // No-op for local collector
        }

        public async Task RegisterInstanceAsync(CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                await _distributedCollector.RegisterInstanceAsync(cancellationToken);
            }
            // No-op for local collector
        }

        public async Task UnregisterInstanceAsync(CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                await _distributedCollector.UnregisterInstanceAsync(cancellationToken);
            }
            // No-op for local collector
        }

        public async Task<CacheStatistics> GetStatisticsForWindowAsync(CacheRegion region, TimeWindow window, CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                return await _distributedCollector.GetStatisticsForWindowAsync(region, window, cancellationToken);
            }
            else if (_localCollector != null)
            {
                return await _localCollector.GetStatisticsForWindowAsync(region, window, cancellationToken);
            }
            
            return new CacheStatistics { Region = region };
        }

        public async Task<IEnumerable<TimeSeriesStatistics>> GetHistoricalStatisticsAsync(
            CacheRegion region,
            DateTime startTime,
            DateTime endTime,
            TimeSpan interval,
            CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                return await _distributedCollector.GetHistoricalStatisticsAsync(region, startTime, endTime, interval, cancellationToken);
            }
            else if (_localCollector != null)
            {
                return await _localCollector.GetHistoricalStatisticsAsync(region, startTime, endTime, interval, cancellationToken);
            }
            
            return Array.Empty<TimeSeriesStatistics>();
        }

        public async Task ResetStatisticsAsync(CacheRegion region, CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                await _distributedCollector.ResetStatisticsAsync(region, cancellationToken);
            }
            else if (_localCollector != null)
            {
                await _localCollector.ResetStatisticsAsync(region, cancellationToken);
            }
        }

        public async Task ResetAllStatisticsAsync(CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                await _distributedCollector.ResetAllStatisticsAsync(cancellationToken);
            }
            else if (_localCollector != null)
            {
                await _localCollector.ResetAllStatisticsAsync(cancellationToken);
            }
        }

        public async Task<string> ExportStatisticsAsync(string format, CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                return await _distributedCollector.ExportStatisticsAsync(format, cancellationToken);
            }
            else if (_localCollector != null)
            {
                return await _localCollector.ExportStatisticsAsync(format, cancellationToken);
            }
            
            return "{}";
        }

        public async Task ConfigureAlertsAsync(CacheRegion region, CacheAlertThresholds thresholds, CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                await _distributedCollector.ConfigureAlertsAsync(region, thresholds, cancellationToken);
            }
            else if (_localCollector != null)
            {
                await _localCollector.ConfigureAlertsAsync(region, thresholds, cancellationToken);
            }
        }

        public async Task<IEnumerable<CacheAlert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                return await _distributedCollector.GetActiveAlertsAsync(cancellationToken);
            }
            else if (_localCollector != null)
            {
                return await _localCollector.GetActiveAlertsAsync(cancellationToken);
            }
            
            return Array.Empty<CacheAlert>();
        }

        /// <summary>
        /// Gets whether the collector is operating in distributed mode.
        /// </summary>
        public bool IsDistributed => _isDistributed;

        /// <summary>
        /// Gets local instance statistics when in distributed mode.
        /// </summary>
        public async Task<CacheStatistics?> GetLocalStatisticsAsync(CacheRegion region, CancellationToken cancellationToken = default)
        {
            if (_isDistributed && _distributedCollector != null)
            {
                // Get statistics for this specific instance
                return await _distributedCollector.GetStatisticsAsync(region, cancellationToken);
            }
            else if (_localCollector != null)
            {
                return await _localCollector.GetStatisticsAsync(region, cancellationToken);
            }
            
            return null;
        }

        private void OnStatisticsUpdated(object? sender, CacheStatisticsUpdatedEventArgs e)
        {
            StatisticsUpdated?.Invoke(this, e);
        }

        private void OnAlertTriggered(object? sender, CacheAlertEventArgs e)
        {
            AlertTriggered?.Invoke(this, e);
        }

        private void OnDistributedStatisticsUpdated(object? sender, DistributedCacheStatisticsEventArgs e)
        {
            DistributedStatisticsUpdated?.Invoke(this, e);
        }

        public void Dispose()
        {
            if (_distributedCollector is IDisposable distributedDisposable)
            {
                distributedDisposable.Dispose();
            }
            
            if (_localCollector is IDisposable localDisposable)
            {
                localDisposable.Dispose();
            }
        }
    }
}