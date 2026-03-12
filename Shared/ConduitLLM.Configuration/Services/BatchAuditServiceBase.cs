using System.Collections.Concurrent;
using ConduitLLM.Functions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Services;

/// <summary>
/// Abstract base class for audit services that use batch processing for database writes.
/// Implements the template method pattern for common batch processing functionality.
/// </summary>
/// <typeparam name="TEvent">The type of audit event entity that implements IAuditEvent</typeparam>
public abstract class BatchAuditServiceBase<TEvent> : IHostedService, IDisposable
    where TEvent : class, IAuditEvent
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<TEvent> _eventQueue;
    private readonly Timer _flushTimer;
    private readonly SemaphoreSlim _flushSemaphore;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of the batch audit service base.
    /// </summary>
    /// <param name="serviceProvider">Service provider for creating scoped DbContexts</param>
    /// <param name="logger">Logger instance</param>
    protected BatchAuditServiceBase(
        IServiceProvider serviceProvider,
        ILogger logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventQueue = new ConcurrentQueue<TEvent>();
        _flushSemaphore = new SemaphoreSlim(1, 1);
        _flushTimer = new Timer(FlushTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    #region Template Methods (Abstract - Must be implemented by derived classes)

    /// <summary>
    /// Gets the DbSet for the event type from the database context.
    /// </summary>
    /// <param name="context">The database context</param>
    /// <returns>The DbSet for the event type</returns>
    protected abstract DbSet<TEvent> GetDbSet(ConduitDbContext context);

    /// <summary>
    /// Gets the entity name for logging purposes (e.g., "Billing", "Pricing", "FunctionCall", "RequestLog").
    /// </summary>
    protected abstract string EntityName { get; }

    #endregion

    #region Virtual Properties (Can be overridden by derived classes)

    /// <summary>
    /// Number of events to process in a single batch. Default: 100
    /// </summary>
    protected virtual int BatchSize => 100;

    /// <summary>
    /// Interval in seconds between automatic flush operations. Default: 10
    /// </summary>
    protected virtual int FlushIntervalSeconds => 10;

    /// <summary>
    /// Number of days to retain audit events before cleanup. Default: 90
    /// </summary>
    protected virtual int RetentionDays => 90;

    /// <summary>
    /// Number of events to delete in a single batch during cleanup. Default: 1000
    /// </summary>
    protected virtual int CleanupBatchSize => 1000;

    /// <summary>
    /// If true, uses ExecuteDeleteAsync for bulk deletion. If false, uses batch loop with RemoveRange.
    /// Default: false
    /// </summary>
    protected virtual bool UseBulkDelete => false;

    #endregion

    #region Public API

    /// <summary>
    /// Logs an audit event asynchronously, waiting for flush if batch size is reached.
    /// </summary>
    /// <param name="auditEvent">The event to log</param>
    /// <exception cref="ArgumentNullException">Thrown when auditEvent is null</exception>
    public async Task LogEventAsync(TEvent auditEvent)
    {
        if (auditEvent == null)
            throw new ArgumentNullException(nameof(auditEvent));

        _eventQueue.Enqueue(auditEvent);

        if (_eventQueue.Count >= BatchSize)
        {
            await FlushEventsInternalAsync(wait: true);
        }
    }

    /// <summary>
    /// Logs an audit event without waiting (fire-and-forget).
    /// Events are queued and flushed in batches.
    /// </summary>
    /// <param name="auditEvent">The event to log</param>
    public void LogEvent(TEvent auditEvent)
    {
        if (auditEvent == null)
        {
            _logger.LogWarning("Attempted to log null {EntityName} audit event", EntityName);
            return;
        }

        _eventQueue.Enqueue(auditEvent);

        if (_eventQueue.Count >= BatchSize)
        {
            _ = Task.Run(async () => await FlushEventsInternalAsync());
        }
    }

    /// <summary>
    /// Forces a flush of all pending audit events to the database.
    /// </summary>
    public async Task FlushEventsAsync()
    {
        await FlushEventsInternalAsync(wait: true);
    }

    /// <summary>
    /// Removes audit events older than the retention period.
    /// </summary>
    public async Task CleanupOldEventsAsync()
    {
        _logger.LogInformation("Starting cleanup of {EntityName} audit events older than {RetentionDays} days",
            EntityName, RetentionDays);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

            var cutoffDate = DateTime.UtcNow.AddDays(-RetentionDays);

            if (UseBulkDelete)
            {
                var deletedCount = await GetDbSet(context)
                    .Where(e => e.Timestamp < cutoffDate)
                    .ExecuteDeleteAsync();

                if (deletedCount > 0)
                {
                    _logger.LogInformation("Cleanup completed: Deleted {TotalDeleted} {EntityName} audit events older than {CutoffDate}",
                        deletedCount, EntityName, cutoffDate);
                }
            }
            else
            {
                int totalDeleted = 0;
                int batchDeleted;

                do
                {
                    var oldEvents = await GetDbSet(context)
                        .Where(e => e.Timestamp < cutoffDate)
                        .OrderBy(e => e.Timestamp)
                        .Take(CleanupBatchSize)
                        .ToListAsync();

                    if (oldEvents.Count == 0)
                        break;

                    GetDbSet(context).RemoveRange(oldEvents);
                    await context.SaveChangesAsync();

                    batchDeleted = oldEvents.Count;
                    totalDeleted += batchDeleted;

                    _logger.LogDebug("Deleted {BatchCount} old {EntityName} audit events", batchDeleted, EntityName);

                    if (batchDeleted == CleanupBatchSize)
                        await Task.Delay(100);

                } while (batchDeleted == CleanupBatchSize);

                if (totalDeleted > 0)
                {
                    _logger.LogInformation("Cleanup completed: Deleted {TotalDeleted} {EntityName} audit events older than {CutoffDate}",
                        totalDeleted, EntityName, cutoffDate);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old {EntityName} audit events", EntityName);
            throw;
        }
    }

    #endregion

    #region IHostedService Implementation

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting {EntityName}AuditService with batch size {BatchSize} and flush interval {FlushInterval}s",
            EntityName, BatchSize, FlushIntervalSeconds);

        // Start the flush timer
        _flushTimer.Change(
            TimeSpan.FromSeconds(FlushIntervalSeconds),
            TimeSpan.FromSeconds(FlushIntervalSeconds));

        // Schedule data retention cleanup
        _ = Task.Run(async () => await ScheduleDataRetentionAsync(cancellationToken), cancellationToken);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping {EntityName}AuditService, flushing remaining events...", EntityName);

        // Stop the timer
        _flushTimer?.Change(Timeout.Infinite, 0);

        // Final flush - drain all remaining events
        await _flushSemaphore.WaitAsync(cancellationToken);
        try
        {
            var events = new List<TEvent>();

            while (_eventQueue.TryDequeue(out var auditEvent))
            {
                events.Add(auditEvent);
            }

            if (events.Count > 0)
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

                await GetDbSet(context).AddRangeAsync(events);
                await context.SaveChangesAsync();

                _logger.LogDebug("Final flush of {Count} {EntityName} audit events to database", events.Count, EntityName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush remaining {EntityName} audit events to database", EntityName);
        }
        finally
        {
            _flushSemaphore.Release();
        }

        _logger.LogInformation("{EntityName}AuditService stopped", EntityName);
    }

    #endregion

    #region IDisposable Implementation

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if from finalizer</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _flushTimer?.Dispose();
            _flushSemaphore?.Dispose();
        }

        _disposed = true;
    }

    #endregion

    #region Protected Methods (Can be overridden by derived classes)

    /// <summary>
    /// Schedules periodic data retention cleanup.
    /// Default behavior: 5-minute initial delay, then daily cleanup.
    /// Override in derived classes to customize (e.g., startup-only cleanup).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    protected virtual async Task ScheduleDataRetentionAsync(CancellationToken cancellationToken)
    {
        // Wait for initial delay before first cleanup
        await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldEventsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during {EntityName} audit event cleanup", EntityName);
            }

            // Run cleanup daily
            await Task.Delay(TimeSpan.FromDays(1), cancellationToken);
        }
    }

    /// <summary>
    /// Gets the service provider for creating scoped services.
    /// </summary>
    protected IServiceProvider ServiceProvider => _serviceProvider;

    /// <summary>
    /// Gets the logger instance.
    /// </summary>
    protected ILogger Logger => _logger;

    #endregion

    #region Query Template Methods

    /// <summary>
    /// Executes a paginated query with time-range filtering and optional domain-specific filters.
    /// Handles scope creation, AsNoTracking, count, ordering by Timestamp desc, and Skip/Take.
    /// </summary>
    /// <param name="from">Start date (inclusive)</param>
    /// <param name="to">End date (inclusive)</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="additionalFilters">Optional function to apply domain-specific filters</param>
    /// <returns>Tuple of paged events and total count</returns>
    protected async Task<(List<TEvent> Events, int TotalCount)> GetPagedEventsAsync(
        DateTime from,
        DateTime to,
        int pageNumber,
        int pageSize,
        Func<IQueryable<TEvent>, IQueryable<TEvent>>? additionalFilters = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

        var query = GetDbSet(context)
            .AsNoTracking()
            .Where(e => e.Timestamp >= from && e.Timestamp <= to);

        if (additionalFilters != null)
            query = additionalFilters(query);

        var totalCount = await query.CountAsync();

        var events = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (events, totalCount);
    }

    /// <summary>
    /// Executes a query with time-range filtering and optional domain-specific filters,
    /// returning all matching events materialized to a list. Useful for summary aggregation.
    /// </summary>
    /// <param name="from">Start date (inclusive)</param>
    /// <param name="to">End date (inclusive)</param>
    /// <param name="additionalFilters">Optional function to apply domain-specific filters</param>
    /// <returns>List of matching events</returns>
    protected async Task<List<TEvent>> GetFilteredEventsAsync(
        DateTime from,
        DateTime to,
        Func<IQueryable<TEvent>, IQueryable<TEvent>>? additionalFilters = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

        var query = GetDbSet(context)
            .AsNoTracking()
            .Where(e => e.Timestamp >= from && e.Timestamp <= to);

        if (additionalFilters != null)
            query = additionalFilters(query);

        return await query.ToListAsync();
    }

    /// <summary>
    /// Executes an arbitrary query against the DbContext with automatic scope management.
    /// Use for domain-specific queries that don't fit the paginated/filtered patterns.
    /// </summary>
    /// <typeparam name="TResult">The query result type</typeparam>
    /// <param name="queryFunc">Function that executes the query against the context</param>
    /// <returns>The query result</returns>
    protected async Task<TResult> ExecuteQueryAsync<TResult>(
        Func<ConduitDbContext, Task<TResult>> queryFunc)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();
        return await queryFunc(context);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Timer callback for periodic flushing.
    /// </summary>
    private void FlushTimerCallback(object? state)
    {
        _ = Task.Run(async () => await FlushEventsInternalAsync());
    }

    /// <summary>
    /// Internal flush implementation with optional waiting.
    /// </summary>
    /// <param name="wait">If true, waits for semaphore. If false, returns immediately if already flushing.</param>
    private async Task FlushEventsInternalAsync(bool wait = false)
    {
        var timeout = wait ? Timeout.InfiniteTimeSpan : TimeSpan.Zero;

        if (!await _flushSemaphore.WaitAsync(timeout))
            return; // Already flushing and not waiting

        try
        {
            var events = new List<TEvent>();

            // Dequeue up to BatchSize events
            while (events.Count < BatchSize && _eventQueue.TryDequeue(out var auditEvent))
            {
                events.Add(auditEvent);
            }

            if (events.Count == 0)
                return;

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ConduitDbContext>();

            await GetDbSet(context).AddRangeAsync(events);
            await context.SaveChangesAsync();

            _logger.LogDebug("Flushed {Count} {EntityName} audit events to database", events.Count, EntityName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush {EntityName} audit events to database", EntityName);
        }
        finally
        {
            _flushSemaphore.Release();
        }
    }

    #endregion
}
