using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.SignalR;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Http.Hubs;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service that batches model discovery notifications to reduce message traffic
    /// </summary>
    public interface IModelDiscoveryNotificationBatcher
    {
        Task QueueNotificationAsync(string group, object notification, NotificationSeverity severity);
        Task FlushBatchAsync(string group);
        Task FlushAllBatchesAsync();
    }

    public class ModelDiscoveryNotificationBatcher : IModelDiscoveryNotificationBatcher, IHostedService, IDisposable
    {
        private readonly IHubContext<ModelDiscoveryHub> _hubContext;
        private readonly IOptions<NotificationBatchingOptions> _options;
        private readonly ILogger<ModelDiscoveryNotificationBatcher> _logger;
        private readonly ConcurrentDictionary<string, NotificationBatch> _batches = new();
        private Timer? _flushTimer;
        private readonly SemaphoreSlim _flushSemaphore = new(1, 1);
        private bool _disposed = false;

        public ModelDiscoveryNotificationBatcher(
            IHubContext<ModelDiscoveryHub> hubContext,
            IOptions<NotificationBatchingOptions> options,
            ILogger<ModelDiscoveryNotificationBatcher> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Timer will be created in StartAsync
        }

        public async Task QueueNotificationAsync(string group, object notification, NotificationSeverity severity)
        {
            if (!_options.Value.EnableBatching || _options.Value.ImmediateSeverityLevels.Contains(severity))
            {
                // Send immediately for critical notifications or when batching is disabled
                await SendImmediateNotificationAsync(group, notification);
                return;
            }

            var batch = _batches.GetOrAdd(group, _ => new NotificationBatch
            {
                Group = group,
                StartTime = DateTime.UtcNow
            });

            lock (batch.SyncRoot)
            {
                // Add notification to appropriate collection based on type
                switch (notification)
                {
                    case NewModelsDiscoveredNotification newModels:
                        AddNewModelsToBatch(batch, newModels);
                        break;
                    case ModelCapabilitiesChangedNotification capChange:
                        AddCapabilityChangeToBatch(batch, capChange, severity);
                        break;
                    case ModelPricingUpdatedNotification priceUpdate:
                        AddPriceUpdateToBatch(batch, priceUpdate);
                        break;
                    default:
                        _logger.LogWarning("Unknown notification type: {Type}", notification.GetType().Name);
                        break;
                }

                batch.NotificationCount++;
            }

            // Check if batch should be flushed
            if (ShouldFlushBatch(batch))
            {
                await FlushBatchAsync(group);
            }
        }

        public async Task FlushBatchAsync(string group)
        {
            if (!_batches.TryRemove(group, out var batch))
                return;

            if (batch.NotificationCount == 0)
                return;

            await _flushSemaphore.WaitAsync();
            try
            {
                var batchedNotification = CreateBatchedNotification(batch);
                await _hubContext.Clients.Group(group).SendAsync("BatchedModelDiscoveryUpdate", batchedNotification);

                _logger.LogInformation(
                    "Flushed batch for group {Group} with {Count} notifications",
                    group, batch.NotificationCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing batch for group {Group}", group);
            }
            finally
            {
                _flushSemaphore.Release();
            }
        }

        public async Task FlushAllBatchesAsync()
        {
            var groups = _batches.Keys.ToList();
            var flushTasks = groups.Select(FlushBatchAsync);
            await Task.WhenAll(flushTasks);
        }

        private async Task SendImmediateNotificationAsync(string group, object notification)
        {
            var methodName = notification switch
            {
                NewModelsDiscoveredNotification => "NewModelsDiscovered",
                ModelCapabilitiesChangedNotification => "ModelCapabilitiesChanged",
                ModelPricingUpdatedNotification => "ModelPricingUpdated",
                _ => "ModelDiscoveryUpdate"
            };

            await _hubContext.Clients.Group(group).SendAsync(methodName, notification);
        }

        private void AddNewModelsToBatch(NotificationBatch batch, NewModelsDiscoveredNotification notification)
        {
            if (!batch.NewModelsByProvider.ContainsKey(notification.Provider))
            {
                batch.NewModelsByProvider[notification.Provider] = new List<DiscoveredModelInfo>();
            }
            batch.NewModelsByProvider[notification.Provider].AddRange(notification.NewModels);
            batch.AffectedProviders.Add(notification.Provider);
        }

        private void AddCapabilityChangeToBatch(NotificationBatch batch, ModelCapabilitiesChangedNotification notification, NotificationSeverity severity)
        {
            if (!batch.CapabilityChanges.ContainsKey(severity))
            {
                batch.CapabilityChanges[severity] = new List<ModelCapabilitiesChangedNotification>();
            }
            batch.CapabilityChanges[severity].Add(notification);
            batch.AffectedProviders.Add(notification.Provider);
        }

        private void AddPriceUpdateToBatch(NotificationBatch batch, ModelPricingUpdatedNotification notification)
        {
            if (!batch.PriceUpdatesByProvider.ContainsKey(notification.Provider))
            {
                batch.PriceUpdatesByProvider[notification.Provider] = new List<ModelPricingUpdatedNotification>();
            }
            batch.PriceUpdatesByProvider[notification.Provider].Add(notification);
            batch.AffectedProviders.Add(notification.Provider);
        }

        private bool ShouldFlushBatch(NotificationBatch batch)
        {
            lock (batch.SyncRoot)
            {
                // Flush if batch size limit reached
                if (batch.NotificationCount >= _options.Value.MaxBatchSize)
                    return true;

                // Flush if max delay reached
                var age = DateTime.UtcNow - batch.StartTime;
                if (age.TotalSeconds >= _options.Value.MaxBatchingDelaySeconds)
                    return true;

                return false;
            }
        }

        private BatchedModelDiscoveryNotification CreateBatchedNotification(NotificationBatch batch)
        {
            var notification = new BatchedModelDiscoveryNotification
            {
                TimeWindow = new BatchTimeWindow
                {
                    StartTime = batch.StartTime,
                    EndTime = DateTime.UtcNow
                },
                NewModelsByProvider = batch.NewModelsByProvider,
                CapabilityChanges = batch.CapabilityChanges,
                PriceUpdatesByProvider = batch.PriceUpdatesByProvider
            };

            // Calculate summary
            notification.Summary = new BatchSummary
            {
                TotalNotifications = batch.NotificationCount,
                NewModelsCount = batch.NewModelsByProvider.Values.Sum(list => list.Count),
                CapabilityChangesCount = batch.CapabilityChanges.Values.Sum(list => list.Count),
                PriceUpdatesCount = batch.PriceUpdatesByProvider.Values.Sum(list => list.Count),
                AffectedProvidersCount = batch.AffectedProviders.Count,
                AffectedProviders = batch.AffectedProviders.ToList(),
                NotificationsBySeverity = batch.CapabilityChanges
                    .GroupBy(kvp => kvp.Key)
                    .ToDictionary(g => g.Key, g => g.Sum(kvp => kvp.Value.Count))
            };

            return notification;
        }

        private async Task FlushExpiredBatchesAsync()
        {
            if (_disposed)
                return;
                
            try
            {
                var now = DateTime.UtcNow;
                var expiredGroups = _batches
                    .Where(kvp =>
                    {
                        lock (kvp.Value.SyncRoot)
                        {
                            var age = now - kvp.Value.StartTime;
                            return age.TotalSeconds >= _options.Value.DefaultBatchingWindowSeconds;
                        }
                    })
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var group in expiredGroups)
                {
                    await FlushBatchAsync(group);
                }
            }
            catch (Exception ex)
            {
                if (!_disposed)
                    _logger.LogError(ex, "Error in flush timer");
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Model discovery notification batcher started");
            
            // Create and start the timer now
            _flushTimer = new Timer(
                async _ => await FlushExpiredBatchesAsync(),
                null,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1));
            
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Model discovery notification batcher stopping");
            
            _flushTimer?.Change(Timeout.Infinite, 0);
            
            // Flush all pending batches
            await FlushAllBatchesAsync();
            
            _logger.LogInformation("Model discovery notification batcher stopped");
        }

        public void Dispose()
        {
            _disposed = true;
            _flushTimer?.Dispose();
            _flushSemaphore?.Dispose();
        }

        private class NotificationBatch
        {
            public string Group { get; set; } = string.Empty;
            public DateTime StartTime { get; set; }
            public int NotificationCount { get; set; }
            public object SyncRoot { get; } = new object();
            public HashSet<string> AffectedProviders { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, List<DiscoveredModelInfo>> NewModelsByProvider { get; } = new();
            public Dictionary<NotificationSeverity, List<ModelCapabilitiesChangedNotification>> CapabilityChanges { get; } = new();
            public Dictionary<string, List<ModelPricingUpdatedNotification>> PriceUpdatesByProvider { get; } = new();
        }
    }
}