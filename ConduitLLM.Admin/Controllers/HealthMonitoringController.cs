using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller providing health monitoring data for dashboards.
    /// </summary>
    [ApiController]
    [Route("api/health")]
    public class HealthMonitoringController : ControllerBase
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
        private readonly ILogger<HealthMonitoringController> _logger;
        private readonly IMemoryCache _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthMonitoringController"/> class.
        /// </summary>
        /// <param name="dbContextFactory">Database context factory.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="cache">Memory cache.</param>
        public HealthMonitoringController(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<HealthMonitoringController> logger,
            IMemoryCache cache)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>
        /// Gets current service health status.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Service health information.</returns>
        [HttpGet("services")]
        public async Task<IActionResult> GetServiceHealth(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                var services = new List<object>();

                // Core API Service
                services.Add(new
                {
                    Id = "core-api",
                    Name = "Core API",
                    Status = "healthy",
                    Uptime = GetProcessUptime(),
                    LastCheck = DateTime.UtcNow,
                    ResponseTime = 15,
                    Details = new
                    {
                        Version = typeof(HealthMonitoringController).Assembly.GetName().Version?.ToString() ?? "unknown",
                        Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                        RequestsHandled = await dbContext.RequestLogs
                            .CountAsync(r => r.Timestamp >= DateTime.UtcNow.AddHours(-1), cancellationToken)
                    }
                });

                // Admin API Service
                services.Add(new
                {
                    Id = "admin-api",
                    Name = "Admin API",
                    Status = "healthy",
                    Uptime = GetProcessUptime(),
                    LastCheck = DateTime.UtcNow,
                    ResponseTime = 10,
                    Details = new
                    {
                        ActiveSessions = 1, // Current session
                        ConfiguredKeys = await dbContext.VirtualKeys.CountAsync(cancellationToken)
                    }
                });

                // Database Service
                var dbHealthCheck = await CheckDatabaseHealth(dbContext, cancellationToken);
                services.Add(new
                {
                    Id = "database",
                    Name = "PostgreSQL Database",
                    Status = dbHealthCheck.IsHealthy ? "healthy" : "unhealthy",
                    Uptime = TimeSpan.FromDays(30), // Would need actual DB uptime
                    LastCheck = DateTime.UtcNow,
                    ResponseTime = dbHealthCheck.ResponseTime,
                    Details = new
                    {
                        ConnectionPooling = true,
                        ActiveConnections = 5, // Would need actual connection count
                        DatabaseSize = await GetDatabaseSize(dbContext, cancellationToken)
                    }
                });


                // Calculate overall health
                var healthyCount = services.Count(s => ((dynamic)s).Status == "healthy");
                var degradedCount = services.Count(s => ((dynamic)s).Status == "degraded");
                var unhealthyCount = services.Count(s => ((dynamic)s).Status == "unhealthy");

                return Ok(new
                {
                    Timestamp = DateTime.UtcNow,
                    OverallStatus = unhealthyCount > 0 ? "unhealthy" : (degradedCount > 0 ? "degraded" : "healthy"),
                    Summary = new
                    {
                        Healthy = healthyCount,
                        Degraded = degradedCount,
                        Unhealthy = unhealthyCount,
                        Total = services.Count
                    },
                    Services = services
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve service health");
                return StatusCode(500, new { error = "Failed to retrieve service health", message = ex.Message });
            }
        }

        /// <summary>
        /// Gets incident history.
        /// </summary>
        /// <param name="days">Number of days to look back (default: 7).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Incident history data.</returns>
        [HttpGet("incidents")]
        public async Task<IActionResult> GetIncidents(
            [FromQuery] int days = 7,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                var startDate = DateTime.UtcNow.AddDays(-days);

                // Analyze request logs for incidents
                var errorSpikes = await dbContext.RequestLogs
                    .Where(r => r.Timestamp >= startDate && r.StatusCode >= 400)
                    .GroupBy(r => new 
                    { 
                        Date = r.Timestamp.Date,
                        Hour = r.Timestamp.Hour,
                        Model = r.ModelName 
                    })
                    .Select(g => new
                    {
                        Date = g.Key.Date,
                        Hour = g.Key.Hour,
                        Service = g.Key.Model, // Using ModelName as service identifier
                        ErrorCount = g.Count(),
                        ErrorTypes = g.Select(r => r.StatusCode).Distinct().Count()
                    })
                    .Where(g => g.ErrorCount >= 10) // Threshold for incident
                    .ToListAsync(cancellationToken);

                // Convert to incidents
                var incidents = errorSpikes.Select(spike => new
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = $"{spike.Service} Service Degradation",
                    Type = "service_degradation",
                    Severity = spike.ErrorCount >= 50 ? "critical" : (spike.ErrorCount >= 25 ? "major" : "minor"),
                    Status = spike.Date.Date == DateTime.UtcNow.Date ? "active" : "resolved",
                    StartTime = new DateTime(spike.Date.Year, spike.Date.Month, spike.Date.Day, spike.Hour, 0, 0),
                    EndTime = spike.Date.Date == DateTime.UtcNow.Date ? (DateTime?)null : 
                             new DateTime(spike.Date.Year, spike.Date.Month, spike.Date.Day, spike.Hour, 59, 59),
                    AffectedService = spike.Service,
                    Impact = $"{spike.ErrorCount} errors in 1 hour period",
                    Details = new
                    {
                        ErrorCount = spike.ErrorCount,
                        UniqueErrorTypes = spike.ErrorTypes
                    }
                }).ToList();

                // Health failures removed - no longer tracking provider health

                var allIncidents = incidents
                    .Cast<object>()
                    .OrderByDescending(i => ((dynamic)i).StartTime)
                    .ToList();

                return Ok(new
                {
                    Timestamp = DateTime.UtcNow,
                    TimeRange = new { Start = startDate, End = DateTime.UtcNow },
                    TotalIncidents = allIncidents.Count,
                    ActiveIncidents = allIncidents.Count(i => ((dynamic)i).Status == "active"),
                    IncidentsByType = allIncidents.GroupBy(i => ((dynamic)i).Type).Select(g => new
                    {
                        Type = g.Key,
                        Count = g.Count()
                    }),
                    IncidentsBySeverity = allIncidents.GroupBy(i => ((dynamic)i).Severity).Select(g => new
                    {
                        Severity = g.Key,
                        Count = g.Count()
                    }),
                    Incidents = allIncidents
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve incidents");
                return StatusCode(500, new { error = "Failed to retrieve incidents", message = ex.Message });
            }
        }

        /// <summary>
        /// Gets health history data.
        /// </summary>
        /// <param name="hours">Number of hours to look back (default: 24).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Health history time series.</returns>
        [HttpGet("history")]
        public async Task<IActionResult> GetHealthHistory(
            [FromQuery] int hours = 24,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                var startTime = DateTime.UtcNow.AddHours(-hours);
                var intervalMinutes = hours <= 24 ? 15 : 60; // 15 min intervals for 24h, 1h for longer

                var healthHistory = new List<object>();
                var currentTime = startTime;

                while (currentTime < DateTime.UtcNow)
                {
                    var intervalEnd = currentTime.AddMinutes(intervalMinutes);

                    // Provider health tracking has been removed

                    // Get error rates for this interval
                    var errorStats = await dbContext.RequestLogs
                        .Where(r => r.Timestamp >= currentTime && r.Timestamp < intervalEnd)
                        .GroupBy(r => 1)
                        .Select(g => new
                        {
                            TotalRequests = g.Count(),
                            ErrorCount = g.Count(r => r.StatusCode >= 400),
                            AvgLatency = g.Average(r => (double?)r.ResponseTimeMs) ?? 0
                        })
                        .FirstOrDefaultAsync(cancellationToken);

                    healthHistory.Add(new
                    {
                        Timestamp = currentTime,
                        SystemHealth = errorStats?.TotalRequests > 0 
                            ? 100 - (errorStats.ErrorCount * 100.0 / errorStats.TotalRequests) 
                            : 100,
                        ProviderHealth = 100, // Provider health tracking removed
                        ResponseTime = errorStats?.AvgLatency ?? 0,
                        RequestVolume = errorStats?.TotalRequests ?? 0,
                        ErrorRate = errorStats?.TotalRequests > 0 
                            ? errorStats.ErrorCount * 100.0 / errorStats.TotalRequests 
                            : 0
                    });

                    currentTime = intervalEnd;
                }

                return Ok(new
                {
                    Timestamp = DateTime.UtcNow,
                    TimeRange = new { Start = startTime, End = DateTime.UtcNow },
                    IntervalMinutes = intervalMinutes,
                    History = healthHistory
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve health history");
                return StatusCode(500, new { error = "Failed to retrieve health history", message = ex.Message });
            }
        }

        private async Task<(bool IsHealthy, int ResponseTime)> CheckDatabaseHealth(
            ConduitDbContext dbContext, 
            CancellationToken cancellationToken)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
                stopwatch.Stop();
                return (true, (int)stopwatch.ElapsedMilliseconds);
            }
            catch
            {
                return (false, -1);
            }
        }

        private async Task<string> GetDatabaseSize(ConduitDbContext dbContext, CancellationToken cancellationToken)
        {
            try
            {
                // This would vary by database provider
                // Added to ensure the method remains asynchronous and to avoid CS1998 warning
                await Task.CompletedTask;
                return "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private static TimeSpan GetProcessUptime()
        {
            return DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
        }
    }
}