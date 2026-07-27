using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Runs a resilient periodic collection loop for metrics and maintenance workers.
/// </summary>
public abstract class PeriodicCollectorBackgroundService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly TimeSpan _collectionInterval;
    private readonly TimeSpan _initialDelay;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a periodic collector.
    /// </summary>
    protected PeriodicCollectorBackgroundService(
        ILogger logger,
        TimeSpan collectionInterval,
        TimeSpan? initialDelay = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(collectionInterval, TimeSpan.Zero);

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _collectionInterval = collectionInterval;
        _initialDelay = initialDelay ?? TimeSpan.Zero;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Gets whether the collector should run.
    /// </summary>
    protected virtual bool IsCollectorEnabled => true;

    /// <summary>
    /// Executes one collection cycle.
    /// </summary>
    protected abstract Task CollectOnceAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Handles a failed collection cycle.
    /// </summary>
    protected virtual void OnCollectionFailed(Exception exception)
    {
        _logger.LogError(exception, "{Collector} collection failed", GetType().Name);
    }

    /// <summary>
    /// Called when configuration disables this collector.
    /// </summary>
    protected virtual void OnCollectorDisabled()
    {
        _logger.LogInformation("{Collector} is disabled", GetType().Name);
    }

    /// <inheritdoc />
    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsCollectorEnabled)
        {
            OnCollectorDisabled();
            return;
        }

        _logger.LogInformation(
            "{Collector} starting with collection interval {CollectionInterval}",
            GetType().Name,
            _collectionInterval);

        if (!await DelayAsync(_initialDelay, stoppingToken))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                OnCollectionFailed(exception);
            }

            if (!await DelayAsync(_collectionInterval, stoppingToken))
            {
                break;
            }
        }

        _logger.LogInformation("{Collector} stopped", GetType().Name);
    }

    private async Task<bool> DelayAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        if (delay <= TimeSpan.Zero)
        {
            return !stoppingToken.IsCancellationRequested;
        }

        try
        {
            await Task.Delay(delay, _timeProvider, stoppingToken);
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
