using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.DTOs.SignalR;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Manages model discovery subscriptions and filters for SignalR connections
    /// </summary>
    public interface IModelDiscoverySubscriptionManager
    {
        Task<ModelDiscoverySubscription> AddOrUpdateSubscriptionAsync(string connectionId, Guid virtualKeyId, ModelDiscoverySubscriptionFilter filter);
        Task RemoveSubscriptionAsync(string connectionId);
        Task<ModelDiscoverySubscription?> GetSubscriptionAsync(string connectionId);
        Task<IEnumerable<ModelDiscoverySubscription>> GetSubscriptionsByFilterAsync(Func<ModelDiscoverySubscription, bool> predicate);
        Task<bool> ShouldReceiveNotificationAsync(string connectionId, string provider, List<string>? capabilities, NotificationSeverity severity, decimal? priceChangePercentage = null);
        Task<Dictionary<string, int>> GetSubscriptionStatisticsAsync();
    }

    public class ModelDiscoverySubscriptionManager : IModelDiscoverySubscriptionManager
    {
        private readonly ConcurrentDictionary<string, ModelDiscoverySubscription> _subscriptions = new();
        private readonly IMemoryCache _cache;
        private readonly ILogger<ModelDiscoverySubscriptionManager> _logger;
        private readonly SemaphoreSlim _cleanupSemaphore = new(1, 1);
        private DateTime _lastCleanup = DateTime.UtcNow;

        public ModelDiscoverySubscriptionManager(
            IMemoryCache cache,
            ILogger<ModelDiscoverySubscriptionManager> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<ModelDiscoverySubscription> AddOrUpdateSubscriptionAsync(
            string connectionId, 
            Guid virtualKeyId, 
            ModelDiscoverySubscriptionFilter filter)
        {
            if (string.IsNullOrEmpty(connectionId))
                throw new ArgumentException("Connection ID cannot be null or empty", nameof(connectionId));

            ArgumentNullException.ThrowIfNull(filter);

            var subscription = new ModelDiscoverySubscription
            {
                ConnectionId = connectionId,
                VirtualKeyId = virtualKeyId,
                Filter = filter,
                CreatedAt = _subscriptions.ContainsKey(connectionId) 
                    ? _subscriptions[connectionId].CreatedAt 
                    : DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            _subscriptions.AddOrUpdate(connectionId, subscription, (_, _) => subscription);

            _logger.LogInformation(
                "Added/updated subscription for connection {ConnectionId} with filters: " +
                "Providers={Providers}, Capabilities={Capabilities}, MinSeverity={MinSeverity}",
                connectionId,
                filter.ProviderTypes?.Count ?? 0,
                filter.Capabilities?.Count ?? 0,
                filter.MinSeverityLevel);

            // Trigger cleanup if needed
            _ = TryCleanupStaleSubscriptionsAsync();

            return Task.FromResult(subscription);
        }

        public Task RemoveSubscriptionAsync(string connectionId)
        {
            if (_subscriptions.TryRemove(connectionId, out var removed))
            {
                _logger.LogInformation(
                    "Removed subscription for connection {ConnectionId}",
                    connectionId);
            }

            return Task.CompletedTask;
        }

        public Task<ModelDiscoverySubscription?> GetSubscriptionAsync(string connectionId)
        {
            _subscriptions.TryGetValue(connectionId, out var subscription);
            return Task.FromResult(subscription);
        }

        public Task<IEnumerable<ModelDiscoverySubscription>> GetSubscriptionsByFilterAsync(
            Func<ModelDiscoverySubscription, bool> predicate)
        {
            var matching = _subscriptions.Values.Where(predicate);
            return Task.FromResult(matching);
        }

        public async Task<bool> ShouldReceiveNotificationAsync(
            string connectionId, 
            string provider, 
            List<string>? capabilities,
            NotificationSeverity severity,
            decimal? priceChangePercentage = null)
        {
            var subscription = await GetSubscriptionAsync(connectionId);
            if (subscription == null)
                return false;

            var filter = subscription.Filter;

            // Check severity level
            if (severity < filter.MinSeverityLevel)
                return false;

            // Check provider filter
            if (filter.ProviderTypes?.Count > 0 && !filter.ProviderTypes.Any(pt => pt.ToString().Equals(provider, StringComparison.OrdinalIgnoreCase)))
                return false;

            // Check capability filter
            if (filter.Capabilities?.Count > 0 && capabilities?.Count > 0)
            {
                var hasMatchingCapability = capabilities.Any(cap => 
                    filter.Capabilities.Contains(cap, StringComparer.OrdinalIgnoreCase));
                
                if (!hasMatchingCapability)
                    return false;
            }

            // Check price change threshold
            if (priceChangePercentage.HasValue && filter.NotifyOnPriceChanges)
            {
                if (Math.Abs(priceChangePercentage.Value) < filter.MinPriceChangePercentage)
                    return false;
            }
            else if (priceChangePercentage.HasValue && !filter.NotifyOnPriceChanges)
            {
                return false;
            }

            return true;
        }

        public Task<Dictionary<string, int>> GetSubscriptionStatisticsAsync()
        {
            var stats = new Dictionary<string, int>
            {
                ["TotalSubscriptions"] = _subscriptions.Count(),
                ["ProvidersFiltered"] = _subscriptions.Values.Count(s => s.Filter.ProviderTypes?.Count > 0),
                ["CapabilitiesFiltered"] = _subscriptions.Values.Count(s => s.Filter.Capabilities?.Count > 0),
                ["BatchingEnabled"] = _subscriptions.Values.Count(s => s.Filter.EnableBatching),
                ["PriceNotificationsEnabled"] = _subscriptions.Values.Count(s => s.Filter.NotifyOnPriceChanges)
            };

            // Count by severity level
            foreach (var severity in Enum.GetValues<NotificationSeverity>())
            {
                stats[$"MinSeverity_{severity}"] = _subscriptions.Values.Count(s => s.Filter.MinSeverityLevel == severity);
            }

            return Task.FromResult(stats);
        }

        private async Task TryCleanupStaleSubscriptionsAsync()
        {
            // Only cleanup every 5 minutes
            if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromMinutes(5))
                return;

            if (!await _cleanupSemaphore.WaitAsync(0))
                return;

            try
            {
                var cutoff = DateTime.UtcNow.AddHours(-24);
                var staleConnections = _subscriptions
                    .Where(kvp => kvp.Value.LastUpdatedAt < cutoff)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var connectionId in staleConnections)
                {
                    await RemoveSubscriptionAsync(connectionId);
                }

                if (staleConnections.Count() > 0)
                {
                    _logger.LogInformation(
                        "Cleaned up {Count} stale subscriptions",
                        staleConnections.Count());
                }

                _lastCleanup = DateTime.UtcNow;
            }
            finally
            {
                _cleanupSemaphore.Release();
            }
        }
    }
}