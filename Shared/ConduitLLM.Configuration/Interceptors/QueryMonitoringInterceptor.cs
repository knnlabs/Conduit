using System.Data.Common;
using System.Diagnostics;

using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;

namespace ConduitLLM.Configuration.Interceptors;

/// <summary>
/// EF Core interceptor that monitors query execution for performance issues.
/// Logs warnings for slow queries and large result sets.
/// </summary>
public class QueryMonitoringInterceptor : DbCommandInterceptor
{
    private readonly ILogger<QueryMonitoringInterceptor> _logger;
    private readonly QueryMonitoringOptions _options;

    // Prometheus metrics for query monitoring
    private static readonly Counter QueryExecutions = Prometheus.Metrics
        .CreateCounter("conduit_db_query_executions_total", "Total database query executions",
            new CounterConfiguration
            {
                LabelNames = new[] { "type" } // type: select, non_query, scalar
            });

    private static readonly Histogram QueryDuration = Prometheus.Metrics
        .CreateHistogram("conduit_db_query_duration_seconds", "Database query duration",
            new HistogramConfiguration
            {
                LabelNames = new[] { "type" },
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 14) // 1ms to ~16s
            });

    private static readonly Counter SlowQueries = Prometheus.Metrics
        .CreateCounter("conduit_db_slow_queries_total", "Total slow database queries",
            new CounterConfiguration
            {
                LabelNames = new[] { "type" }
            });

    /// <summary>
    /// Creates a new instance of the QueryMonitoringInterceptor.
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="options">The monitoring options</param>
    public QueryMonitoringInterceptor(
        ILogger<QueryMonitoringInterceptor> logger,
        IOptions<QueryMonitoringOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        if (!_options.Enabled)
        {
            return result;
        }

        RecordQueryMetrics(command, eventData, IsSelectQuery(command) ? "select" : "non_query");

        // Only wrap SELECT queries - INSERT/UPDATE/DELETE with RETURNING clauses
        // return readers that Npgsql internally casts to NpgsqlDataReader, which fails
        // if wrapped. Row counting is only meaningful for SELECT anyway.
        if (IsSelectQuery(command))
        {
            return new RowCountingDataReader(result, _logger, _options, GetCommandSummary(command));
        }

        return result;
    }

    /// <inheritdoc/>
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return result;
        }

        RecordQueryMetrics(command, eventData, IsSelectQuery(command) ? "select" : "non_query");

        // Only wrap SELECT queries - INSERT/UPDATE/DELETE with RETURNING clauses
        // return readers that Npgsql internally casts to NpgsqlDataReader, which fails
        // if wrapped. Row counting is only meaningful for SELECT anyway.
        if (IsSelectQuery(command))
        {
            return new RowCountingDataReader(result, _logger, _options, GetCommandSummary(command));
        }

        return result;
    }

    /// <inheritdoc/>
    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        if (_options.Enabled)
        {
            RecordQueryMetrics(command, eventData, "non_query");
        }

        return result;
    }

    /// <inheritdoc/>
    public override async ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (_options.Enabled)
        {
            RecordQueryMetrics(command, eventData, "non_query");
        }

        return result;
    }

    /// <inheritdoc/>
    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        if (_options.Enabled)
        {
            RecordQueryMetrics(command, eventData, "scalar");
        }

        return result;
    }

    /// <inheritdoc/>
    public override async ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        if (_options.Enabled)
        {
            RecordQueryMetrics(command, eventData, "scalar");
        }

        return result;
    }

    private void RecordQueryMetrics(DbCommand command, CommandExecutedEventData eventData, string queryType)
    {
        var durationSeconds = eventData.Duration.TotalSeconds;
        QueryExecutions.WithLabels(queryType).Inc();
        QueryDuration.WithLabels(queryType).Observe(durationSeconds);

        var durationMs = eventData.Duration.TotalMilliseconds;
        if (durationMs >= _options.SlowQueryThresholdMs)
        {
            SlowQueries.WithLabels(queryType).Inc();
            var commandSummary = GetCommandSummary(command);
            _logger.LogWarning(
                "Slow query detected ({DurationMs:F1}ms, threshold: {ThresholdMs}ms). Command: {CommandSummary}",
                durationMs,
                _options.SlowQueryThresholdMs,
                commandSummary);
        }
    }

    private static bool IsSelectQuery(DbCommand command)
    {
        var text = command.CommandText.TrimStart();
        return text.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);
    }

    private string GetCommandSummary(DbCommand command)
    {
        if (_options.LogFullCommand)
        {
            return command.CommandText;
        }

        // Extract just the first part of the command (SELECT, INSERT, etc.) and table name
        var text = command.CommandText;
        var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 0)
        {
            var firstLine = lines[0].Trim();
            // Limit length for summary
            return firstLine.Length > 100 ? firstLine[..100] + "..." : firstLine;
        }

        return "[empty command]";
    }
}
