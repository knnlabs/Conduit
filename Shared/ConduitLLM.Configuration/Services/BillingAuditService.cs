using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Services;

/// <summary>
/// Service for auditing billing events with batch writing and async processing.
/// </summary>
public class BillingAuditService : BatchAuditServiceBase<BillingAuditEvent>, IBillingAuditService
{
    /// <summary>
    /// Creates a new instance of the BillingAuditService.
    /// </summary>
    /// <param name="serviceProvider">Service provider for creating scoped DbContexts</param>
    /// <param name="logger">Logger instance</param>
    public BillingAuditService(
        IServiceProvider serviceProvider,
        ILogger<BillingAuditService> logger)
        : base(serviceProvider, logger)
    {
    }

    #region Template Method Implementations

    /// <inheritdoc/>
    protected override DbSet<BillingAuditEvent> GetDbSet(ConduitDbContext context)
        => context.BillingAuditEvents;

    /// <inheritdoc/>
    protected override string EntityName => "Billing";

    #endregion

    #region IBillingAuditService Implementation (Wrapper Methods)

    /// <inheritdoc/>
    public Task LogBillingEventAsync(BillingAuditEvent auditEvent)
        => LogEventAsync(auditEvent);

    /// <inheritdoc/>
    public void LogBillingEvent(BillingAuditEvent auditEvent)
        => LogEvent(auditEvent);

    /// <inheritdoc/>
    public Task CleanupOldAuditEventsAsync()
        => CleanupOldEventsAsync();

    #endregion

    #region Domain-Specific Query Methods

    /// <inheritdoc/>
    public async Task<(List<BillingAuditEvent> Events, int TotalCount)> GetAuditEventsAsync(
        DateTime from,
        DateTime to,
        BillingAuditEventType? eventType = null,
        int? virtualKeyId = null,
        int pageNumber = 1,
        int pageSize = 100)
    {
        using var scope = ServiceProvider.CreateScope();
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
        using var scope = ServiceProvider.CreateScope();
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
        using var scope = ServiceProvider.CreateScope();
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
        using var scope = ServiceProvider.CreateScope();
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

    #endregion
}
