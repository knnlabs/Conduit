using System.Collections.Concurrent;
using ConduitLLM.Configuration;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Services;

/// <summary>
/// Service for logging function call audit events with batch processing.
/// Implements IHostedService for background batch flushing.
/// </summary>
public class FunctionCallAuditService : IFunctionCallAuditService, IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FunctionCallAuditService> _logger;
    private readonly ConcurrentQueue<FunctionCallAudit> _eventQueue = new();
    private readonly SemaphoreSlim _flushSemaphore = new(1, 1);
    private readonly Timer _flushTimer;

    private const int BatchSize = 100;
    private const int FlushIntervalSeconds = 10;
    private const int DataRetentionDays = 90;

    public FunctionCallAuditService(
        IServiceProvider serviceProvider,
        ILogger<FunctionCallAuditService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _flushTimer = new Timer(
            callback: async _ => await FlushEventsInternalAsync(),
            state: null,
            dueTime: TimeSpan.FromSeconds(FlushIntervalSeconds),
            period: TimeSpan.FromSeconds(FlushIntervalSeconds));
    }

    public void LogFunctionCallEvent(FunctionCallAudit auditEvent)
    {
        if (auditEvent == null)
        {
            _logger.LogWarning("Attempted to log null function call audit event");
            return;
        }

        _eventQueue.Enqueue(auditEvent);

        // Auto-flush if batch size reached (fire-and-forget)
        if (_eventQueue.Count >= BatchSize)
        {
            _ = Task.Run(async () => await FlushEventsInternalAsync());
        }
    }

    public async Task LogFunctionCallEventAsync(FunctionCallAudit auditEvent)
    {
        if (auditEvent == null)
        {
            _logger.LogWarning("Attempted to log null function call audit event");
            return;
        }

        _eventQueue.Enqueue(auditEvent);

        // Auto-flush if batch size reached (wait for completion)
        if (_eventQueue.Count >= BatchSize)
        {
            await FlushEventsInternalAsync(wait: true);
        }
    }

    public async Task FlushEventsAsync()
    {
        await FlushEventsInternalAsync(wait: true);
    }

    private async Task FlushEventsInternalAsync(bool wait = false)
    {
        var timeout = wait ? Timeout.InfiniteTimeSpan : TimeSpan.Zero;

        if (!await _flushSemaphore.WaitAsync(timeout))
        {
            // Another flush is in progress
            return;
        }

        try
        {
            var events = new List<FunctionCallAudit>();

            // Dequeue up to BatchSize events
            while (events.Count < BatchSize && _eventQueue.TryDequeue(out var auditEvent))
            {
                events.Add(auditEvent);
            }

            if (events.Count == 0)
            {
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

            await dbContext.FunctionCallAudits.AddRangeAsync(events);
            await dbContext.SaveChangesAsync();

            _logger.LogDebug("Flushed {Count} function call audit events to database", events.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing function call audit events to database");
        }
        finally
        {
            _flushSemaphore.Release();
        }
    }

    public async Task<(List<FunctionCallAudit> Events, int TotalCount)> GetAuditEventsAsync(
        DateTime from,
        DateTime to,
        FunctionCallAuditEventType? eventType = null,
        int? virtualKeyId = null,
        int? functionConfigurationId = null,
        Guid? chatCompletionId = null,
        int pageNumber = 1,
        int pageSize = 100)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

        var query = dbContext.FunctionCallAudits
            .AsNoTracking()
            .Where(e => e.Timestamp >= from && e.Timestamp <= to);

        if (eventType.HasValue)
        {
            query = query.Where(e => e.EventType == eventType.Value);
        }

        if (virtualKeyId.HasValue)
        {
            query = query.Where(e => e.VirtualKeyId == virtualKeyId.Value);
        }

        if (functionConfigurationId.HasValue)
        {
            query = query.Where(e => e.FunctionConfigurationId == functionConfigurationId.Value);
        }

        if (chatCompletionId.HasValue)
        {
            query = query.Where(e => e.ChatCompletionId == chatCompletionId.Value);
        }

        var totalCount = await query.CountAsync();

        var events = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (events, totalCount);
    }

    public async Task<FunctionCallAuditSummary> GetAuditSummaryAsync(
        DateTime from,
        DateTime to,
        int? virtualKeyId = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

        var query = dbContext.FunctionCallAudits
            .AsNoTracking()
            .Where(e => e.Timestamp >= from && e.Timestamp <= to);

        if (virtualKeyId.HasValue)
        {
            query = query.Where(e => e.VirtualKeyId == virtualKeyId.Value);
        }

        var events = await query.ToListAsync();

        var summary = new FunctionCallAuditSummary
        {
            TotalFunctionCalls = events.Count,
            SuccessfulCalls = events.Count(e => e.EventType == FunctionCallAuditEventType.FunctionCallExecutionCompleted),
            FailedCalls = events.Count(e => e.EventType == FunctionCallAuditEventType.FunctionCallExecutionFailed),
            TotalCost = events.Where(e => e.Cost.HasValue).Sum(e => e.Cost!.Value),
            CallsByEventType = events.GroupBy(e => e.EventType)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        // Get calls by function configuration
        var functionConfigs = await dbContext.FunctionConfigurations
            .Where(fc => events.Select(e => e.FunctionConfigurationId).Contains(fc.Id))
            .ToDictionaryAsync(fc => fc.Id, fc => fc.ConfigurationName);

        summary.CallsByFunction = events
            .GroupBy(e => e.FunctionConfigurationId)
            .ToDictionary(
                g => functionConfigs.TryGetValue(g.Key, out var name) ? name : $"Unknown ({g.Key})",
                g => g.Count());

        return summary;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("FunctionCallAuditService started. Batch size: {BatchSize}, Flush interval: {Interval}s",
            BatchSize, FlushIntervalSeconds);

        // Clean up old audit records on startup
        await CleanupOldRecordsAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("FunctionCallAuditService stopping. Flushing remaining events...");

        // Flush any remaining events
        await FlushEventsInternalAsync(wait: true);

        _logger.LogInformation("FunctionCallAuditService stopped");
    }

    private async Task CleanupOldRecordsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

            var cutoffDate = DateTime.UtcNow.AddDays(-DataRetentionDays);

            var deletedCount = await dbContext.FunctionCallAudits
                .Where(e => e.Timestamp < cutoffDate)
                .ExecuteDeleteAsync();

            if (deletedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} function call audit records older than {Days} days",
                    deletedCount, DataRetentionDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old function call audit records");
        }
    }

    public void Dispose()
    {
        _flushTimer?.Dispose();
        _flushSemaphore?.Dispose();
    }
}
