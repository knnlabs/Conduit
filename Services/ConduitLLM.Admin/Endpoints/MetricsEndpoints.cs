using System.Diagnostics;

using ConduitLLM.Admin.DTOs;
using ConduitLLM.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

using Npgsql;

namespace ConduitLLM.Admin.Endpoints;

public static class MetricsEndpoints
{
    public static IEndpointRouteBuilder MapAdminMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/metrics")
            .RequireAuthorization("MasterKeyPolicy")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Metrics");
        group.MapGet("/", GetAllMetrics).WithName("Metrics_GetAllMetrics")
            .Produces<AllMetricsDto>(StatusCodes.Status200OK);

        app.MapGet("/v1/admin/database-pool-metrics", GetDatabasePoolMetrics)
            .RequireAuthorization("MasterKeyPolicy")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Metrics")
            .WithName("Metrics_GetDatabasePoolMetrics")
            .Produces<DatabasePoolMetricsDto>(StatusCodes.Status200OK);
        return app;
    }

    private static async Task<IResult> GetDatabasePoolMetrics(
        [FromServices] IDbContextFactory<ConduitDbContext> factory,
        CancellationToken cancellationToken) =>
        Results.Ok(await GetPoolMetrics(factory, cancellationToken));

    private static async Task<IResult> GetAllMetrics(
        [FromServices] IDbContextFactory<ConduitDbContext> factory,
        CancellationToken cancellationToken)
    {
        var poolMetrics = await GetPoolMetrics(factory, cancellationToken);
        return Results.Ok(new AllMetricsDto
        {
            Timestamp = DateTime.UtcNow,
            Application = new ApplicationInfoDto
            {
                Name = "Conduit Gateway API",
                Version = typeof(MetricsEndpoints).Assembly.GetName().Version?.ToString() ?? "unknown",
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
            },
            Database = poolMetrics,
            System = new SystemMetricsDto
            {
                CpuCount = Environment.ProcessorCount,
                WorkingSetMb = Environment.WorkingSet / 1024 / 1024,
                GcMemoryMb = GC.GetTotalMemory(false) / 1024 / 1024,
                ThreadCount = Process.GetCurrentProcess().Threads.Count,
                Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()
            }
        });
    }

    private static async Task<object> GetPoolMetrics(
        IDbContextFactory<ConduitDbContext> factory,
        CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        if (context.Database.GetDbConnection() is not NpgsqlConnection connection)
        {
            return new DatabasePoolMetricsUnavailableDto
            {
                Provider = "non-postgresql",
                Message = "Connection pool metrics only available for PostgreSQL"
            };
        }
        var builder = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        var stopwatch = Stopwatch.StartNew();
        await connection.OpenAsync(cancellationToken);
        stopwatch.Stop();
        await connection.CloseAsync();
        return new DatabasePoolMetricsDto
        {
            Timestamp = DateTime.UtcNow,
            Provider = "postgresql",
            ConnectionString = new DatabasePoolConnectionInfoDto
            {
                Host = builder.Host,
                Port = builder.Port,
                Database = builder.Database,
                ApplicationName = builder.ApplicationName ?? "Conduit Gateway API"
            },
            PoolConfiguration = new DatabasePoolConfigurationDto
            {
                MinPoolSize = builder.MinPoolSize,
                MaxPoolSize = builder.MaxPoolSize,
                ConnectionLifetime = builder.ConnectionLifetime,
                ConnectionIdleLifetime = builder.ConnectionIdleLifetime,
                Pooling = builder.Pooling
            },
            CurrentMetrics = new DatabasePoolCurrentMetricsDto
            {
                ConnectionAcquisitionTimeMs = stopwatch.ElapsedMilliseconds,
                HealthStatus = stopwatch.ElapsedMilliseconds < 50 ? "healthy"
                    : stopwatch.ElapsedMilliseconds < 200 ? "degraded" : "unhealthy",
                Note = "For detailed pool statistics, query pg_stat_activity directly or use monitoring tools"
            }
        };
    }
}
