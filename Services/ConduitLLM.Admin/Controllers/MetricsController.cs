using System.Diagnostics;

using ConduitLLM.Configuration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for exposing application metrics including database connection pool statistics.
    /// </summary>
    [ApiController]
    [Route("metrics")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class MetricsController : AdminControllerBase
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsController"/> class.
        /// </summary>
        /// <param name="dbContextFactory">Database context factory.</param>
        /// <param name="logger">Logger instance.</param>
        public MetricsController(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<MetricsController> logger)
            : base(logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        }

        /// <summary>
        /// Gets database connection pool metrics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Connection pool metrics.</returns>
        [HttpGet("database/pool")]
        public Task<IActionResult> GetDatabasePoolMetrics(CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(
                async () =>
                {
                    using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                    var connection = dbContext.Database.GetDbConnection() as NpgsqlConnection;

                    if (connection == null)
                    {
                        return (object)new
                        {
                            provider = "non-postgresql",
                            message = "Connection pool metrics only available for PostgreSQL"
                        };
                    }

                    // Get connection string to extract pool settings
                    var connectionString = connection.ConnectionString;
                    var builder = new NpgsqlConnectionStringBuilder(connectionString);

                    // Measure connection acquisition time
                    var stopwatch = Stopwatch.StartNew();
                    await connection.OpenAsync(cancellationToken);
                    stopwatch.Stop();
                    await connection.CloseAsync();

                    // Note: Npgsql doesn't expose pool statistics directly in current versions
                    // We can only infer pool health from connection acquisition time
                    // For detailed monitoring, use PostgreSQL's pg_stat_activity or external monitoring tools

                    return (object)new
                    {
                        timestamp = DateTime.UtcNow,
                        provider = "postgresql",
                        connectionString = new
                        {
                            host = builder.Host,
                            port = builder.Port,
                            database = builder.Database,
                            applicationName = builder.ApplicationName ?? "Conduit Gateway API"
                        },
                        poolConfiguration = new
                        {
                            minPoolSize = builder.MinPoolSize,
                            maxPoolSize = builder.MaxPoolSize,
                            connectionLifetime = builder.ConnectionLifetime,
                            connectionIdleLifetime = builder.ConnectionIdleLifetime,
                            pooling = builder.Pooling
                        },
                        currentMetrics = new
                        {
                            connectionAcquisitionTimeMs = stopwatch.ElapsedMilliseconds,
                            healthStatus = GetHealthStatus(stopwatch.ElapsedMilliseconds),
                            // Additional metrics can be obtained from pg_stat_activity if needed
                            // but we avoid that here to prevent performance impact
                            note = "For detailed pool statistics, query pg_stat_activity directly or use monitoring tools"
                        }
                    };
                },
                Ok,
                "GetDatabasePoolMetrics");
        }

        /// <summary>
        /// Gets all application metrics including database, cache, and performance metrics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Comprehensive application metrics.</returns>
        [HttpGet]
        public Task<IActionResult> GetAllMetrics(CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(
                async () =>
                {
                    // Get database pool metrics
                    var poolMetricsResult = await GetDatabasePoolMetrics(cancellationToken);
                    var poolMetrics = (poolMetricsResult as OkObjectResult)?.Value;

                    return new
                    {
                        timestamp = DateTime.UtcNow,
                        application = new
                        {
                            name = "Conduit Gateway API",
                            version = typeof(MetricsController).Assembly.GetName().Version?.ToString() ?? "unknown",
                            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                        },
                        database = poolMetrics,
                        system = new
                        {
                            cpuCount = Environment.ProcessorCount,
                            workingSetMb = Environment.WorkingSet / 1024 / 1024,
                            gcMemoryMb = GC.GetTotalMemory(false) / 1024 / 1024,
                            threadCount = Process.GetCurrentProcess().Threads.Count,
                            uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()
                        }
                    };
                },
                Ok,
                "GetAllMetrics");
        }

        private static string GetHealthStatus(long acquisitionTimeMs)
        {
            if (acquisitionTimeMs < 50)
                return "healthy";
            else if (acquisitionTimeMs < 200)
                return "degraded";
            else
                return "unhealthy";
        }

        private static string SanitizeConnectionString(string connectionString)
        {
            // Remove sensitive information from connection string
            return System.Text.RegularExpressions.Regex.Replace(
                connectionString,
                @"(Password|pwd)=([^;]+)",
                "$1=*****",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
    }
}
