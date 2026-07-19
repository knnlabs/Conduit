using System.Diagnostics;

using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Filters;
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
    [ServiceFilter(typeof(OperationLoggingFilter))]
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
        [ProducesResponseType(typeof(DatabasePoolMetricsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDatabasePoolMetrics(CancellationToken cancellationToken = default)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var connection = dbContext.Database.GetDbConnection() as NpgsqlConnection;

            if (connection == null)
            {
                return Ok(new DatabasePoolMetricsUnavailableDto
                {
                    Provider = "non-postgresql",
                    Message = "Connection pool metrics only available for PostgreSQL"
                });
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

            return Ok(new DatabasePoolMetricsDto
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
                    HealthStatus = GetHealthStatus(stopwatch.ElapsedMilliseconds),
                    // Additional metrics can be obtained from pg_stat_activity if needed
                    // but we avoid that here to prevent performance impact
                    Note = "For detailed pool statistics, query pg_stat_activity directly or use monitoring tools"
                }
            });
        }

        /// <summary>
        /// Gets all application metrics including database, cache, and performance metrics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Comprehensive application metrics.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(AllMetricsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllMetrics(CancellationToken cancellationToken = default)
        {
            // Get database pool metrics
            var poolMetricsResult = await GetDatabasePoolMetrics(cancellationToken);
            var poolMetrics = (poolMetricsResult as OkObjectResult)?.Value;

            return Ok(new AllMetricsDto
            {
                Timestamp = DateTime.UtcNow,
                Application = new ApplicationInfoDto
                {
                    Name = "Conduit Gateway API",
                    Version = typeof(MetricsController).Assembly.GetName().Version?.ToString() ?? "unknown",
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
