using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.Data;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Background service that batches Virtual Key spend updates to reduce database writes
    /// Provides events for cache invalidation integration
    /// </summary>
    public class BatchSpendUpdateService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<BatchSpendUpdateService> _logger;
        private readonly ConcurrentDictionary<int, decimal> _pendingSpendUpdates = new();
        private readonly Timer _flushTimer;
        private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(30); // Flush every 30 seconds
        
        /// <summary>
        /// Event raised after successful batch spend updates with the key hashes that were updated
        /// Allows external cache invalidation without tight coupling
        /// </summary>
        public event Action<string[]>? SpendUpdatesCompleted;

        /// <summary>
        /// Initializes a new instance of the BatchSpendUpdateService
        /// </summary>
        /// <param name="serviceScopeFactory">Service scope factory for creating scoped services</param>
        /// <param name="logger">Logger instance</param>
        public BatchSpendUpdateService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<BatchSpendUpdateService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            
            // Create timer for periodic flushing (in addition to background service)
            _flushTimer = new Timer(FlushPendingUpdatesCallback, null, _flushInterval, _flushInterval);
        }

        /// <summary>
        /// Add a spend update to the batch queue
        /// </summary>
        /// <param name="virtualKeyId">Virtual Key ID to update</param>
        /// <param name="cost">Cost to add to the current spend</param>
        public void QueueSpendUpdate(int virtualKeyId, decimal cost)
        {
            _pendingSpendUpdates.AddOrUpdate(virtualKeyId, cost, (key, existingCost) => existingCost + cost);
            
            _logger.LogDebug("Queued spend update for Virtual Key {VirtualKeyId}: {Cost:C}", virtualKeyId, cost);
        }

        /// <summary>
        /// Get the current pending spend for a Virtual Key
        /// </summary>
        /// <param name="virtualKeyId">Virtual Key ID</param>
        /// <returns>Pending spend amount</returns>
        public decimal GetPendingSpend(int virtualKeyId)
        {
            return _pendingSpendUpdates.TryGetValue(virtualKeyId, out var pendingSpend) ? pendingSpend : 0;
        }

        /// <summary>
        /// Force flush all pending updates immediately
        /// </summary>
        /// <returns>Number of Virtual Keys updated</returns>
        public async Task<int> FlushPendingUpdatesAsync()
        {
            if (_pendingSpendUpdates.IsEmpty)
            {
                return 0;
            }

            // Snapshot current pending updates and clear the queue
            var updates = _pendingSpendUpdates.ToArray();
            foreach (var (keyId, _) in updates)
            {
                _pendingSpendUpdates.TryRemove(keyId, out _);
            }

            if (updates.Length == 0)
            {
                return 0;
            }

            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IConfigurationDbContext>();

                // Batch update all Virtual Keys with pending spend
                var virtualKeyIds = updates.Select(u => u.Key).ToList();
                var virtualKeys = await context.VirtualKeys
                    .Where(vk => virtualKeyIds.Contains(vk.Id))
                    .ToListAsync();

                foreach (var (keyId, cost) in updates)
                {
                    var virtualKey = virtualKeys.FirstOrDefault(vk => vk.Id == keyId);
                    if (virtualKey != null)
                    {
                        virtualKey.CurrentSpend += cost;
                        virtualKey.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        _logger.LogWarning("Virtual Key not found for spend update: {VirtualKeyId}", keyId);
                    }
                }

                var affectedRows = await context.SaveChangesAsync();
                
                _logger.LogInformation("Batch updated spend for {Count} Virtual Keys, {AffectedRows} rows modified", 
                    updates.Length, affectedRows);

                // Raise event for cache invalidation (if any subscribers)
                if (affectedRows > 0 && SpendUpdatesCompleted != null)
                {
                    var keyHashes = virtualKeys
                        .Where(vk => updates.Any(u => u.Key == vk.Id))
                        .Select(vk => vk.KeyHash)
                        .ToArray();
                    
                    if (keyHashes.Length > 0)
                    {
                        try
                        {
                            SpendUpdatesCompleted.Invoke(keyHashes);
                            _logger.LogDebug("Raised SpendUpdatesCompleted event for {Count} Virtual Keys", keyHashes.Length);
                        }
                        catch (Exception eventEx)
                        {
                            _logger.LogWarning(eventEx, "Error in SpendUpdatesCompleted event handler");
                            // Don't fail the operation if event handler fails
                        }
                    }
                }

                return updates.Length;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during batch spend update for {Count} Virtual Keys", updates.Length);
                
                // Re-queue failed updates
                foreach (var (keyId, cost) in updates)
                {
                    _pendingSpendUpdates.AddOrUpdate(keyId, cost, (key, existingCost) => existingCost + cost);
                }
                
                throw;
            }
        }

        /// <summary>
        /// Timer callback for periodic flushing
        /// </summary>
        private void FlushPendingUpdatesCallback(object? state)
        {
            // Fire and forget with proper error handling
            _ = Task.Run(async () =>
            {
                try
                {
                    await FlushPendingUpdatesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in periodic flush timer");
                }
            });
        }

        /// <summary>
        /// Background service execution
        /// </summary>
        /// <param name="stoppingToken">Cancellation token</param>
        /// <returns>Async task</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BatchSpendUpdateService started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Flush updates every interval
                    await Task.Delay(_flushInterval, stoppingToken);
                    await FlushPendingUpdatesAsync();
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in BatchSpendUpdateService background execution");
                    
                    // Continue running even if there's an error
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }

            _logger.LogInformation("BatchSpendUpdateService stopping");
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public override void Dispose()
        {
            _flushTimer?.Dispose();
            
            // Try to flush any remaining updates on shutdown
            try
            {
                FlushPendingUpdatesAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing pending updates during service disposal");
            }
            
            base.Dispose();
        }

        /// <summary>
        /// Get statistics about the batching service
        /// </summary>
        /// <returns>Dictionary with service statistics</returns>
        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["PendingUpdates"] = _pendingSpendUpdates.Count,
                ["TotalPendingCost"] = _pendingSpendUpdates.Values.Sum(),
                ["FlushIntervalSeconds"] = _flushInterval.TotalSeconds
            };
        }
    }
}