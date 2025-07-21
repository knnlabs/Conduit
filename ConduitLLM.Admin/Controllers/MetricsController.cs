using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Npgsql;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for exposing Admin API metrics including database connection pool statistics.
    /// </summary>
    [ApiController]
    [Route("api/metrics")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class MetricsController : ControllerBase
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<MetricsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsController"/> class.
        /// </summary>
        /// <param name="dbContextFactory">Database context factory.</param>
        /// <param name="logger">Logger instance.</param>
        public MetricsController(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<MetricsController> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets database connection pool metrics for the Admin API.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Connection pool metrics.</returns>
        [HttpGet("database/pool")]
        public async Task<IActionResult> GetDatabasePoolMetrics(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var connection = dbContext.Database.GetDbConnection() as NpgsqlConnection;
                
                if (connection == null)
                {
                    return Ok(new
                    {
                        provider = "non-postgresql",
                        message = "Connection pool metrics only available for PostgreSQL"
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

                var metrics = new
                {
                    timestamp = DateTime.UtcNow,
                    provider = "postgresql",
                    service = "Admin API",
                    connectionString = new
                    {
                        host = builder.Host,
                        port = builder.Port,
                        database = builder.Database,
                        applicationName = builder.ApplicationName ?? "Conduit Admin API"
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
                        note = "For detailed pool statistics, query pg_stat_activity directly or use monitoring tools"
                    }
                };

                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve database pool metrics");
                return StatusCode(500, new { error = "Failed to retrieve metrics", message = ex.Message });
            }
        }

        /// <summary>
        /// Gets all Admin API metrics including database and system metrics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Comprehensive Admin API metrics.</returns>
        [HttpGet]
        public async Task<IActionResult> GetAllMetrics(CancellationToken cancellationToken = default)
        {
            try
            {
                // Get database pool metrics
                var poolMetricsResult = await GetDatabasePoolMetrics(cancellationToken);
                var poolMetrics = (poolMetricsResult as OkObjectResult)?.Value;

                var allMetrics = new
                {
                    timestamp = DateTime.UtcNow,
                    application = new
                    {
                        name = "Conduit Admin API",
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

                return Ok(allMetrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve Admin API metrics");
                return StatusCode(500, new { error = "Failed to retrieve metrics", message = ex.Message });
            }
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
    }
}