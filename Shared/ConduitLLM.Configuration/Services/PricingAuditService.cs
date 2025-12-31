using System.Collections.Concurrent;
using System.Text.Json;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Services;

/// <summary>
/// Service for auditing pricing rule evaluations with batch writing and async processing.
/// </summary>
public class PricingAuditService : IPricingAuditService, IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PricingAuditService> _logger;
    private readonly ConcurrentQueue<PricingAuditEvent> _eventQueue;
    private readonly Timer _flushTimer;
    private readonly SemaphoreSlim _flushSemaphore;
    private bool _disposed;

    private const int BatchSize = 100;
    private const int FlushIntervalSeconds = 10;
    private const int RetentionDays = 90;

    public PricingAuditService(
        IServiceProvider serviceProvider,
        ILogger<PricingAuditService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventQueue = new ConcurrentQueue<PricingAuditEvent>();
        _flushSemaphore = new SemaphoreSlim(1, 1);
        _flushTimer = new Timer(FlushEvents, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <inheritdoc/>
    public async Task LogAsync(PricingAuditEvent auditEvent)
    {
        if (auditEvent == null)
            throw new ArgumentNullException(nameof(auditEvent));

        _eventQueue.Enqueue(auditEvent);

        if (_eventQueue.Count >= BatchSize)
        {
            await FlushEventsAsync(wait: true);
        }
    }

    /// <inheritdoc/>
    public void Log(PricingAuditEvent auditEvent)
    {
        if (auditEvent == null)
            return;

        _eventQueue.Enqueue(auditEvent);

        if (_eventQueue.Count >= BatchSize)
        {
            _ = Task.Run(async () => await FlushEventsAsync());
        }
    }

    /// <inheritdoc/>
    public async Task<(List<PricingAuditEvent> Events, int TotalCount)> GetAuditEventsAsync(
        DateTime from,
        DateTime to,
        int? virtualKeyId = null,
        string? modelId = null,
        string? pricingType = null,
        int pageNumber = 1,
        int pageSize = 100)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

        var query = context.PricingAuditEvents
            .AsNoTracking()
            .Where(e => e.Timestamp >= from && e.Timestamp <= to);

        if (virtualKeyId.HasValue)
            query = query.Where(e => e.VirtualKeyId == virtualKeyId.Value);

        if (!string.IsNullOrEmpty(modelId))
            query = query.Where(e => e.ModelId == modelId);

        if (!string.IsNullOrEmpty(pricingType))
            query = query.Where(e => e.PricingType == pricingType);

        var totalCount = await query.CountAsync();

        var events = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (events, totalCount);
    }

    /// <inheritdoc/>
    public async Task<List<PricingAuditEvent>> GetByRequestIdAsync(string requestId)
    {
        if (string.IsNullOrEmpty(requestId))
            return new List<PricingAuditEvent>();

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

        return await context.PricingAuditEvents
            .AsNoTracking()
            .Where(e => e.RequestId == requestId)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<PricingAuditSummary> GetSummaryAsync(
        DateTime from,
        DateTime to,
        int? virtualKeyId = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

        var query = context.PricingAuditEvents
            .AsNoTracking()
            .Where(e => e.Timestamp >= from && e.Timestamp <= to);

        if (virtualKeyId.HasValue)
            query = query.Where(e => e.VirtualKeyId == virtualKeyId.Value);

        var events = await query.ToListAsync();

        var summary = new PricingAuditSummary
        {
            TotalEvaluations = events.Count,
            DefaultRateUsed = events.Count(e => e.UsedDefaultRate),
            RulesMatched = events.Count(e => !e.UsedDefaultRate),
            TotalRevenue = events.Sum(e => e.CalculatedCost),
            AverageRate = events.Count > 0 ? events.Average(e => e.AppliedRate) : 0
        };

        // Pricing type breakdown
        summary.PricingTypeBreakdown = events
            .GroupBy(e => e.PricingType)
            .ToDictionary(g => g.Key, g => (long)g.Count());

        // Model breakdown
        summary.ModelBreakdown = events
            .Where(e => !string.IsNullOrEmpty(e.ModelId))
            .GroupBy(e => e.ModelId)
            .ToDictionary(g => g.Key, g => (long)g.Count());

        // Top matched rules
        summary.TopMatchedRules = events
            .Where(e => !e.UsedDefaultRate && !string.IsNullOrEmpty(e.MatchedRule))
            .GroupBy(e => ExtractRuleDescription(e.MatchedRule))
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new RuleMatchSummary
            {
                RuleDescription = g.Key,
                MatchCount = g.Count(),
                TotalRevenue = g.Sum(e => e.CalculatedCost)
            })
            .ToList();

        return summary;
    }

    /// <inheritdoc/>
    public async Task CleanupOldAuditEventsAsync()
    {
        const int DeleteBatchSize = 1000;

        _logger.LogInformation("Starting cleanup of pricing audit events older than {RetentionDays} days", RetentionDays);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

            var cutoffDate = DateTime.UtcNow.AddDays(-RetentionDays);
            int totalDeleted = 0;
            int batchDeleted;

            do
            {
                var oldEvents = await context.PricingAuditEvents
                    .Where(e => e.Timestamp < cutoffDate)
                    .OrderBy(e => e.Timestamp)
                    .Take(DeleteBatchSize)
                    .ToListAsync();

                if (oldEvents.Count == 0)
                    break;

                context.PricingAuditEvents.RemoveRange(oldEvents);
                await context.SaveChangesAsync();

                batchDeleted = oldEvents.Count;
                totalDeleted += batchDeleted;

                _logger.LogDebug("Deleted {BatchCount} old pricing audit events", batchDeleted);

                if (batchDeleted == DeleteBatchSize)
                    await Task.Delay(100);

            } while (batchDeleted == DeleteBatchSize);

            if (totalDeleted > 0)
            {
                _logger.LogInformation("Cleanup completed: Deleted {TotalDeleted} pricing audit events older than {CutoffDate}",
                    totalDeleted, cutoffDate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old pricing audit events");
            throw;
        }
    }

    /// <summary>
    /// Flushes queued events to the database.
    /// </summary>
    private async Task FlushEventsAsync(bool wait = false)
    {
        var timeout = wait ? Timeout.InfiniteTimeSpan : TimeSpan.Zero;
        if (!await _flushSemaphore.WaitAsync(timeout))
            return;

        try
        {
            var events = new List<PricingAuditEvent>();

            while (events.Count < BatchSize && _eventQueue.TryDequeue(out var auditEvent))
            {
                events.Add(auditEvent);
            }

            if (events.Count == 0)
                return;

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

            await context.PricingAuditEvents.AddRangeAsync(events);
            await context.SaveChangesAsync();

            _logger.LogDebug("Flushed {Count} pricing audit events to database", events.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush pricing audit events to database");
        }
        finally
        {
            _flushSemaphore.Release();
        }
    }

    /// <summary>
    /// Timer callback for periodic flushing.
    /// </summary>
    private void FlushEvents(object? state)
    {
        _ = Task.Run(async () => await FlushEventsAsync());
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting PricingAuditService with batch size {BatchSize} and flush interval {FlushInterval}s",
            BatchSize, FlushIntervalSeconds);

        _flushTimer.Change(TimeSpan.FromSeconds(FlushIntervalSeconds), TimeSpan.FromSeconds(FlushIntervalSeconds));

        _ = Task.Run(async () => await ScheduleDataRetentionAsync(cancellationToken), cancellationToken);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping PricingAuditService, flushing remaining events...");

        _flushTimer?.Change(Timeout.Infinite, 0);

        await _flushSemaphore.WaitAsync(cancellationToken);
        try
        {
            var events = new List<PricingAuditEvent>();

            while (_eventQueue.TryDequeue(out var auditEvent))
            {
                events.Add(auditEvent);
            }

            if (events.Count > 0)
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

                await context.PricingAuditEvents.AddRangeAsync(events);
                await context.SaveChangesAsync();

                _logger.LogDebug("Final flush of {Count} pricing audit events to database", events.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush remaining pricing audit events to database");
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
    /// Schedules periodic data retention cleanup.
    /// </summary>
    private async Task ScheduleDataRetentionAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldAuditEventsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during pricing audit event cleanup");
            }

            await Task.Delay(TimeSpan.FromDays(1), cancellationToken);
        }
    }

    /// <summary>
    /// Extracts the rule description from serialized rule JSON.
    /// </summary>
    private static string ExtractRuleDescription(string? matchedRuleJson)
    {
        if (string.IsNullOrEmpty(matchedRuleJson))
            return "Unknown";

        try
        {
            using var doc = JsonDocument.Parse(matchedRuleJson);
            if (doc.RootElement.TryGetProperty("description", out var descElement) ||
                doc.RootElement.TryGetProperty("Description", out descElement))
            {
                return descElement.GetString() ?? "Unnamed Rule";
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return "Unnamed Rule";
    }
}
