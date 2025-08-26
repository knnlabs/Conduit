using System.Collections.Concurrent;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Service for auditing billing events with batch writing and async processing
    /// </summary>
    public class BillingAuditService : IBillingAuditService, IHostedService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BillingAuditService> _logger;
        private readonly ConcurrentQueue<BillingAuditEvent> _eventQueue;
        private readonly Timer _flushTimer;
        private readonly SemaphoreSlim _flushSemaphore;
        private bool _disposed;

        private const int BatchSize = 100;
        private const int FlushIntervalSeconds = 10;

        public BillingAuditService(
            IServiceProvider serviceProvider,
            ILogger<BillingAuditService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventQueue = new ConcurrentQueue<BillingAuditEvent>();
            _flushSemaphore = new SemaphoreSlim(1, 1);
            _flushTimer = new Timer(FlushEvents, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <inheritdoc/>
        public async Task LogBillingEventAsync(BillingAuditEvent auditEvent)
        {
            if (auditEvent == null)
                throw new ArgumentNullException(nameof(auditEvent));

            // Queue the event for batch processing
            _eventQueue.Enqueue(auditEvent);

            // If we've reached the batch size, flush immediately and wait for it
            if (_eventQueue.Count >= BatchSize)
            {
                await FlushEventsAsync(wait: true);
            }
        }

        /// <inheritdoc/>
        public void LogBillingEvent(BillingAuditEvent auditEvent)
        {
            if (auditEvent == null)
                return;

            // Fire and forget - queue the event
            _eventQueue.Enqueue(auditEvent);

            // Trigger flush if batch size reached
            if (_eventQueue.Count >= BatchSize)
            {
                _ = Task.Run(async () => await FlushEventsAsync());
            }
        }

        /// <inheritdoc/>
        public async Task<(List<BillingAuditEvent> Events, int TotalCount)> GetAuditEventsAsync(
            DateTime from,
            DateTime to,
            BillingAuditEventType? eventType = null,
            int? virtualKeyId = null,
            int pageNumber = 1,
            int pageSize = 100)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

            var query = context.BillingAuditEvents
                .AsNoTracking()
                .Where(e => e.Timestamp >= from && e.Timestamp <= to);

            if (eventType.HasValue)
                query = query.Where(e => e.EventType == eventType.Value);

            if (virtualKeyId.HasValue)
                query = query.Where(e => e.VirtualKeyId == virtualKeyId.Value);

            var totalCount = await query.CountAsync();

            var events = await query
                .OrderByDescending(e => e.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (events, totalCount);
        }

        /// <inheritdoc/>
        public async Task<BillingAuditSummary> GetAuditSummaryAsync(
            DateTime from,
            DateTime to,
            int? virtualKeyId = null)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

            var query = context.BillingAuditEvents
                .AsNoTracking()
                .Where(e => e.Timestamp >= from && e.Timestamp <= to);

            if (virtualKeyId.HasValue)
                query = query.Where(e => e.VirtualKeyId == virtualKeyId.Value);

            var events = await query.ToListAsync();

            var summary = new BillingAuditSummary
            {
                TotalEvents = events.Count,
                SuccessfulBillings = events.Count(e => e.EventType == BillingAuditEventType.UsageTracked),
                ZeroCostSkipped = events.Count(e => e.EventType == BillingAuditEventType.ZeroCostSkipped),
                EstimatedUsages = events.Count(e => e.EventType == BillingAuditEventType.UsageEstimated),
                FailedUpdates = events.Count(e => e.EventType == BillingAuditEventType.SpendUpdateFailed),
                ErrorResponsesSkipped = events.Count(e => e.EventType == BillingAuditEventType.ErrorResponseSkipped),
                MissingUsageData = events.Count(e => e.EventType == BillingAuditEventType.MissingUsageData),
                TotalBilledAmount = events
                    .Where(e => e.EventType == BillingAuditEventType.UsageTracked && e.CalculatedCost.HasValue)
                    .Sum(e => e.CalculatedCost!.Value),
                PotentialRevenueLoss = events
                    .Where(e => e.EventType != BillingAuditEventType.UsageTracked && 
                           e.EventType != BillingAuditEventType.ErrorResponseSkipped &&
                           e.CalculatedCost.HasValue)
                    .Sum(e => e.CalculatedCost!.Value)
            };

            // Event type breakdown
            summary.EventTypeBreakdown = events
                .GroupBy(e => e.EventType)
                .ToDictionary(g => g.Key, g => (long)g.Count());

            // Provider type breakdown
            summary.ProviderTypeBreakdown = events
                .Where(e => !string.IsNullOrEmpty(e.ProviderType))
                .GroupBy(e => e.ProviderType!)
                .ToDictionary(g => g.Key, g => (long)g.Count());

            return summary;
        }

        /// <inheritdoc/>
        public async Task<decimal> GetPotentialRevenueLossAsync(DateTime from, DateTime to)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

            return await context.BillingAuditEvents
                .AsNoTracking()
                .Where(e => e.Timestamp >= from && e.Timestamp <= to)
                .Where(e => e.EventType != BillingAuditEventType.UsageTracked)
                .Where(e => e.EventType != BillingAuditEventType.ErrorResponseSkipped)
                .Where(e => e.CalculatedCost.HasValue)
                .SumAsync(e => e.CalculatedCost ?? 0);
        }

        /// <inheritdoc/>
        public async Task<List<BillingAnomaly>> DetectAnomaliesAsync(DateTime from, DateTime to)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

            var anomalies = new List<BillingAnomaly>();

            // Check for high failure rate
            var failureRate = await context.BillingAuditEvents
                .AsNoTracking()
                .Where(e => e.Timestamp >= from && e.Timestamp <= to)
                .GroupBy(e => e.EventType)
                .Select(g => new { EventType = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalEvents = failureRate.Sum(f => f.Count);
            var failures = failureRate
                .Where(f => f.EventType == BillingAuditEventType.SpendUpdateFailed ||
                           f.EventType == BillingAuditEventType.MissingCostConfig)
                .Sum(f => f.Count);

            if (totalEvents > 0 && (double)failures / totalEvents > 0.05) // More than 5% failure rate
            {
                anomalies.Add(new BillingAnomaly
                {
                    AnomalyType = "HighFailureRate",
                    Description = $"Billing failure rate is {(double)failures / totalEvents:P} which exceeds 5% threshold",
                    Severity = "High",
                    DetectedAt = DateTime.UtcNow,
                    EstimatedImpact = await GetPotentialRevenueLossAsync(from, to)
                });
            }

            // Check for sudden spike in zero-cost calculations
            var zeroCostEvents = await context.BillingAuditEvents
                .AsNoTracking()
                .Where(e => e.Timestamp >= from && e.Timestamp <= to)
                .Where(e => e.EventType == BillingAuditEventType.ZeroCostSkipped)
                .GroupBy(e => e.Timestamp.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(g => g.Date)
                .ToListAsync();

            if (zeroCostEvents.Count > 1)
            {
                var avgZeroCost = zeroCostEvents.Average(z => z.Count);
                var maxZeroCost = zeroCostEvents.Max(z => z.Count);

                if (maxZeroCost > avgZeroCost * 3) // Spike is 3x average
                {
                    anomalies.Add(new BillingAnomaly
                    {
                        AnomalyType = "ZeroCostSpike",
                        Description = $"Zero-cost calculations spiked to {maxZeroCost} events, 3x the average of {avgZeroCost:F0}",
                        Severity = "Medium",
                        DetectedAt = zeroCostEvents.First(z => z.Count == maxZeroCost).Date
                    });
                }
            }

            // Check for missing configurations
            var missingConfigs = await context.BillingAuditEvents
                .AsNoTracking()
                .Where(e => e.Timestamp >= from && e.Timestamp <= to)
                .Where(e => e.EventType == BillingAuditEventType.MissingCostConfig)
                .GroupBy(e => e.Model)
                .Select(g => new { Model = g.Key, Count = g.Count() })
                .Where(g => g.Count > 10) // More than 10 occurrences
                .ToListAsync();

            foreach (var config in missingConfigs)
            {
                anomalies.Add(new BillingAnomaly
                {
                    AnomalyType = "MissingModelConfiguration",
                    Description = $"Model '{config.Model}' has no cost configuration ({config.Count} requests)",
                    Severity = "Medium",
                    DetectedAt = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object> { ["model"] = config.Model ?? "unknown", ["count"] = config.Count }
                });
            }

            return anomalies;
        }

        /// <summary>
        /// Flushes queued events to the database
        /// </summary>
        /// <param name="wait">If true, wait for semaphore. If false, return immediately if semaphore is busy.</param>
        private async Task FlushEventsAsync(bool wait = false)
        {
            var timeout = wait ? Timeout.InfiniteTimeSpan : TimeSpan.Zero;
            if (!await _flushSemaphore.WaitAsync(timeout))
                return; // Already flushing and not waiting
            
            try
            {
                var events = new List<BillingAuditEvent>();

                // Dequeue up to BatchSize events
                while (events.Count < BatchSize && _eventQueue.TryDequeue(out var auditEvent))
                {
                    events.Add(auditEvent);
                }

                if (events.Count == 0)
                    return;

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

                await context.BillingAuditEvents.AddRangeAsync(events);
                await context.SaveChangesAsync();

                _logger.LogDebug("Flushed {Count} billing audit events to database", events.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to flush billing audit events to database");
            }
            finally
            {
                _flushSemaphore.Release();
            }
        }

        /// <summary>
        /// Timer callback for periodic flushing
        /// </summary>
        private void FlushEvents(object? state)
        {
            _ = Task.Run(async () => await FlushEventsAsync());
        }

        /// <inheritdoc/>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting BillingAuditService with batch size {BatchSize} and flush interval {FlushInterval}s", 
                BatchSize, FlushIntervalSeconds);

            // Start the flush timer
            _flushTimer.Change(TimeSpan.FromSeconds(FlushIntervalSeconds), TimeSpan.FromSeconds(FlushIntervalSeconds));
            
            // Schedule daily cleanup of old audit events
            _ = Task.Run(async () => await ScheduleDataRetentionAsync(cancellationToken), cancellationToken);
            
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping BillingAuditService, flushing remaining events...");

            // Stop the timer
            _flushTimer?.Change(Timeout.Infinite, 0);

            // Force flush any remaining events - wait for semaphore to ensure all events are flushed
            await _flushSemaphore.WaitAsync(cancellationToken);
            try
            {
                var events = new List<BillingAuditEvent>();

                // Dequeue ALL remaining events
                while (_eventQueue.TryDequeue(out var auditEvent))
                {
                    events.Add(auditEvent);
                }

                if (events.Count > 0)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

                    await context.BillingAuditEvents.AddRangeAsync(events);
                    await context.SaveChangesAsync();

                    _logger.LogDebug("Final flush of {Count} billing audit events to database", events.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to flush remaining billing audit events to database");
            }
            finally
            {
                _flushSemaphore.Release();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;

            _flushTimer?.Dispose();
            _flushSemaphore?.Dispose();
            _disposed = true;
        }

        /// <summary>
        /// Schedules periodic data retention cleanup
        /// </summary>
        private async Task ScheduleDataRetentionAsync(CancellationToken cancellationToken)
        {
            // Wait for initial delay before first cleanup
            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupOldAuditEventsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during audit event cleanup");
                }

                // Run cleanup daily
                await Task.Delay(TimeSpan.FromDays(1), cancellationToken);
            }
        }

        /// <summary>
        /// Removes audit events older than retention period
        /// </summary>
        public async Task CleanupOldAuditEventsAsync()
        {
            const int RetentionDays = 90; // Keep audit events for 90 days
            const int BatchSize = 1000; // Delete in batches to avoid locking

            _logger.LogInformation("Starting cleanup of audit events older than {RetentionDays} days", RetentionDays);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

                var cutoffDate = DateTime.UtcNow.AddDays(-RetentionDays);
                int totalDeleted = 0;
                int batchDeleted;

                do
                {
                    // Get a batch of old events to delete
                    var oldEvents = await context.BillingAuditEvents
                        .Where(e => e.Timestamp < cutoffDate)
                        .OrderBy(e => e.Timestamp)
                        .Take(BatchSize)
                        .ToListAsync();

                    if (oldEvents.Count == 0)
                        break;

                    context.BillingAuditEvents.RemoveRange(oldEvents);
                    await context.SaveChangesAsync();

                    batchDeleted = oldEvents.Count;
                    totalDeleted += batchDeleted;

                    _logger.LogDebug("Deleted {BatchCount} old audit events", batchDeleted);

                    // Brief pause between batches to reduce database load
                    if (batchDeleted == BatchSize)
                        await Task.Delay(100);

                } while (batchDeleted == BatchSize);

                if (totalDeleted > 0)
                {
                    _logger.LogInformation("Cleanup completed: Deleted {TotalDeleted} audit events older than {CutoffDate}",
                        totalDeleted, cutoffDate);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old audit events");
                throw;
            }
        }
    }
}