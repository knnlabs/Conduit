using System.Text.Json;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Services;

/// <summary>
/// Service for auditing pricing rule evaluations with batch writing and async processing.
/// </summary>
public class PricingAuditService : BatchAuditServiceBase<PricingAuditEvent>, IPricingAuditService
{
    /// <summary>
    /// Creates a new instance of the PricingAuditService.
    /// </summary>
    /// <param name="serviceProvider">Service provider for creating scoped DbContexts</param>
    /// <param name="logger">Logger instance</param>
    public PricingAuditService(
        IServiceProvider serviceProvider,
        ILogger<PricingAuditService> logger)
        : base(serviceProvider, logger)
    {
    }

    #region Template Method Implementations

    /// <inheritdoc/>
    protected override DbSet<PricingAuditEvent> GetDbSet(ConduitDbContext context)
        => context.PricingAuditEvents;

    /// <inheritdoc/>
    protected override string EntityName => "Pricing";

    #endregion

    #region IPricingAuditService Implementation (Wrapper Methods)

    /// <inheritdoc/>
    public Task LogAsync(PricingAuditEvent auditEvent)
        => LogEventAsync(auditEvent);

    /// <inheritdoc/>
    public void Log(PricingAuditEvent auditEvent)
        => LogEvent(auditEvent);

    /// <inheritdoc/>
    public Task CleanupOldAuditEventsAsync()
        => CleanupOldEventsAsync();

    #endregion

    #region Domain-Specific Query Methods

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
        return await GetPagedEventsAsync(from, to, pageNumber, pageSize, query =>
        {
            if (virtualKeyId.HasValue)
                query = query.Where(e => e.VirtualKeyId == virtualKeyId.Value);
            if (!string.IsNullOrEmpty(modelId))
                query = query.Where(e => e.ModelId == modelId);
            if (!string.IsNullOrEmpty(pricingType))
                query = query.Where(e => e.PricingType == pricingType);
            return query;
        });
    }

    /// <inheritdoc/>
    public async Task<List<PricingAuditEvent>> GetByRequestIdAsync(string requestId)
    {
        if (string.IsNullOrEmpty(requestId))
            return new List<PricingAuditEvent>();

        return await ExecuteQueryAsync(context =>
            context.PricingAuditEvents
                .AsNoTracking()
                .Where(e => e.RequestId == requestId)
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync());
    }

    /// <inheritdoc/>
    public async Task<PricingAuditSummary> GetSummaryAsync(
        DateTime from,
        DateTime to,
        int? virtualKeyId = null)
    {
        var events = await GetFilteredEventsAsync(from, to, query =>
        {
            if (virtualKeyId.HasValue)
                query = query.Where(e => e.VirtualKeyId == virtualKeyId.Value);
            return query;
        });

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

    #endregion

    #region Private Helpers

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

    #endregion
}
