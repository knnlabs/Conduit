using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Options;

using Microsoft.Extensions.Options;

namespace ConduitLLM.Gateway.Services;

/// <summary>Periodically drains archived and stale rows from the durable async-task store.</summary>
public sealed class AsyncTaskRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<AsyncTaskRetentionOptions> _options;
    private readonly ILogger<AsyncTaskRetentionService> _logger;

    public AsyncTaskRetentionService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<AsyncTaskRetentionOptions> options,
        ILogger<AsyncTaskRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.CurrentValue;
            if (options.Enabled)
            {
                await RunRetentionAsync(options, stoppingToken);
            }

            var interval = options.Interval > TimeSpan.Zero
                ? options.Interval
                : TimeSpan.FromHours(1);
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    internal async Task RunRetentionAsync(
        AsyncTaskRetentionOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var taskService = scope.ServiceProvider.GetRequiredService<IAsyncTaskService>();
            var result = await taskService.CleanupOldTasksAsync(
                new AsyncTaskRetentionPolicy(
                    options.ArchiveCompletedAfter,
                    options.DeleteArchivedAfter,
                    options.ArchiveStaleAfter,
                    options.BatchSize),
                cancellationToken);

            _logger.LogInformation(
                "Automatic async-task retention archived {ArchivedCount} and deleted {DeletedCount} rows",
                result.Archived,
                result.Deleted);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Automatic async-task retention failed");
        }
    }
}
