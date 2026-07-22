using System.Diagnostics;

using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Constants;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConduitLLM.Admin.Endpoints
{
    /// <summary>
    /// Controller providing health monitoring data for dashboards.
    /// </summary>
    public static class HealthMonitoringEndpoints
    {
        public static IEndpointRouteBuilder MapHealthMonitoringEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/health")
                .AddEndpointFilter<OperationLoggingEndpointFilter>()
                .WithTags("Health Monitoring");
            group.MapGet("/services", GetServiceHealth)
                .WithName("HealthMonitoring_GetServiceHealth")
                .Produces<ServiceHealthResponse>();
            group.MapGet("/incidents", GetIncidents)
                .WithName("HealthMonitoring_GetIncidents")
                .Produces<IncidentsResponse>();
            group.MapGet("/history", GetHealthHistory)
                .WithName("HealthMonitoring_GetHealthHistory")
                .Produces<HealthHistoryResponse>();
            return app;
        }

        /// <summary>
        /// Gets current service health status.
        /// </summary>
        /// <param name="dbContextFactory">Factory for the configuration database context.</param>
        /// <param name="systemInfoService">System information service (database size).</param>
        /// <param name="healthCheckService">The Admin's registered ASP.NET health checks.</param>
        /// <param name="heartbeatStore">Store of cross-service liveness heartbeats.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Service health information.</returns>
        private static async Task<IResult> GetServiceHealth(
            [FromServices] IDbContextFactory<ConduitDbContext> dbContextFactory,
            [FromServices] IAdminSystemInfoService systemInfoService,
            [FromServices] HealthCheckService healthCheckService,
            [FromServices] IServiceHeartbeatStore heartbeatStore,
            CancellationToken cancellationToken)
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var services = new List<ServiceStatusDto>();

            // Gateway API — real liveness from the Gateway's heartbeat (#1067). The Admin never
            // probes the Gateway over HTTP; the Gateway publishes a heartbeat event that the
            // GatewayHeartbeatHandler records, and we derive status from how stale it is.
            var gatewayHeartbeat = await heartbeatStore.GetAsync(
                RedisKeys.ServiceHeartbeat.GatewayServiceId, cancellationToken);
            services.Add(BuildGatewayStatus(gatewayHeartbeat));

            // Admin API — real readiness from this process's registered health checks (mirrors
            // /health/ready), replacing the previous hardcoded "healthy".
            var configuredKeys = await dbContext.VirtualKeys.CountAsync(cancellationToken);
            services.Add(await BuildAdminStatusAsync(healthCheckService, configuredKeys, cancellationToken));

            // Database — genuinely probed (SELECT 1) with a real response time and best-effort
            // server uptime (replacing the previous 30-day placeholder).
            var dbHealthCheck = await CheckDatabaseHealth(dbContext, cancellationToken);
            var dbUptime = await GetDatabaseUptimeAsync(dbContext, cancellationToken);
            services.Add(new ServiceStatusDto
            {
                Id = "database",
                Name = "PostgreSQL Database",
                Status = dbHealthCheck.IsHealthy ? "healthy" : "unhealthy",
                Uptime = dbUptime,
                LastCheck = DateTime.UtcNow,
                ResponseTime = dbHealthCheck.ResponseTime,
                Details = new
                {
                    ConnectionPooling = true,
                    DatabaseSize = await GetDatabaseSize(systemInfoService)
                }
            });

            // Calculate overall health. "unknown" services (e.g. a Gateway not seen yet) are
            // not counted as healthy — they pull the rollup down to at least "degraded".
            var healthyCount = services.Count(s => s.Status == "healthy");
            var degradedCount = services.Count(s => s.Status == "degraded");
            var unhealthyCount = services.Count(s => s.Status == "unhealthy");
            var unknownCount = services.Count(s => s.Status == "unknown");

            return Results.Ok(new ServiceHealthResponse
            {
                Timestamp = DateTime.UtcNow,
                OverallStatus = unhealthyCount > 0
                    ? "unhealthy"
                    : (degradedCount > 0 || unknownCount > 0) ? "degraded" : "healthy",
                Summary = new ServiceHealthSummary
                {
                    Healthy = healthyCount,
                    Degraded = degradedCount,
                    Unhealthy = unhealthyCount,
                    Unknown = unknownCount,
                    Total = services.Count
                },
                Services = services
            });
        }

        /// <summary>
        /// Gets incident history.
        /// </summary>
        /// <param name="dbContextFactory">Factory for the configuration database context.</param>
        /// <param name="days">Number of days to look back (default: 7).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Incident history data.</returns>
        private static async Task<IResult> GetIncidents(
            [FromServices] IDbContextFactory<ConduitDbContext> dbContextFactory,
            [FromQuery] int days = 7,
            CancellationToken cancellationToken = default)
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

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
            var incidents = errorSpikes.Select(spike => new IncidentDto
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
                Details = new IncidentDetailsDto
                {
                    ErrorCount = spike.ErrorCount,
                    UniqueErrorTypes = spike.ErrorTypes
                }
            }).ToList();

            var allIncidents = incidents
                .OrderByDescending(i => i.StartTime)
                .ToList();

            return Results.Ok(new IncidentsResponse
            {
                Timestamp = DateTime.UtcNow,
                TimeRange = new TimeRangeDto { Start = startDate, End = DateTime.UtcNow },
                TotalIncidents = allIncidents.Count,
                ActiveIncidents = allIncidents.Count(i => i.Status == "active"),
                IncidentsByType = allIncidents.GroupBy(i => i.Type).Select(g => new IncidentTypeCountDto
                {
                    Type = g.Key,
                    Count = g.Count()
                }).ToList(),
                IncidentsBySeverity = allIncidents.GroupBy(i => i.Severity).Select(g => new IncidentSeverityCountDto
                {
                    Severity = g.Key,
                    Count = g.Count()
                }).ToList(),
                Incidents = allIncidents
            });
        }

        /// <summary>
        /// Gets health history data.
        /// </summary>
        /// <param name="dbContextFactory">Factory for the configuration database context.</param>
        /// <param name="hours">Number of hours to look back (default: 24).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Health history time series.</returns>
        private static async Task<IResult> GetHealthHistory(
            [FromServices] IDbContextFactory<ConduitDbContext> dbContextFactory,
            [FromQuery] int hours = 24,
            CancellationToken cancellationToken = default)
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var startTime = DateTime.UtcNow.AddHours(-hours);
            var intervalMinutes = hours <= 24 ? 15 : 60; // 15 min intervals for 24h, 1h for longer

            var healthHistory = new List<HealthHistoryPointDto>();
            var currentTime = startTime;

            while (currentTime < DateTime.UtcNow)
            {
                var intervalEnd = currentTime.AddMinutes(intervalMinutes);

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

                healthHistory.Add(new HealthHistoryPointDto
                {
                    Timestamp = currentTime,
                    SystemHealth = errorStats?.TotalRequests > 0
                        ? 100 - (errorStats.ErrorCount * 100.0 / errorStats.TotalRequests)
                        : 100,
                    ResponseTime = errorStats?.AvgLatency ?? 0,
                    RequestVolume = errorStats?.TotalRequests ?? 0,
                    ErrorRate = errorStats?.TotalRequests > 0
                        ? errorStats.ErrorCount * 100.0 / errorStats.TotalRequests
                        : 0
                });

                currentTime = intervalEnd;
            }

            return Results.Ok(new HealthHistoryResponse
            {
                Timestamp = DateTime.UtcNow,
                TimeRange = new TimeRangeDto { Start = startTime, End = DateTime.UtcNow },
                IntervalMinutes = intervalMinutes,
                History = healthHistory
            });
        }

        /// <summary>
        /// Builds the Gateway API status from its most recent heartbeat (#1067). Freshness
        /// thresholds are derived from the interval the Gateway itself reported, so the two
        /// services need not share configuration. Staleness is measured against the Admin's
        /// receive time to avoid cross-service clock skew.
        /// </summary>
        private static ServiceStatusDto BuildGatewayStatus(ServiceHeartbeatSnapshot? heartbeat)
        {
            const string id = "core-api";
            const string name = "Gateway API";

            if (heartbeat == null)
            {
                return new ServiceStatusDto
                {
                    Id = id,
                    Name = name,
                    Status = "unknown",
                    Uptime = null,
                    LastCheck = DateTime.UtcNow,
                    ResponseTime = null,
                    Details = new
                    {
                        Source = "heartbeat",
                        Reason = "No heartbeat received from the Gateway yet"
                    }
                };
            }

            var intervalSeconds = heartbeat.IntervalSeconds > 0
                ? heartbeat.IntervalSeconds
                : ServiceHeartbeatEvaluator.DefaultIntervalSeconds;
            var ageSeconds = Math.Max(0, (DateTime.UtcNow - heartbeat.ReceivedAtUtc).TotalSeconds);

            // Fresh within 2 intervals → healthy; within 4 → degraded (heartbeats delayed);
            // older → unhealthy (heartbeats lost — the Gateway is likely down or unreachable).
            var status = ServiceHeartbeatEvaluator.EvaluateStatus(ageSeconds, intervalSeconds);

            return new ServiceStatusDto
            {
                Id = id,
                Name = name,
                Status = status,
                Uptime = TimeSpan.FromSeconds(heartbeat.UptimeSeconds),
                LastCheck = heartbeat.ReceivedAtUtc,
                ResponseTime = null, // liveness signal — no request/response latency to report
                Details = new
                {
                    Source = "heartbeat",
                    heartbeat.InstanceId,
                    heartbeat.Version,
                    LastHeartbeatUtc = heartbeat.ReceivedAtUtc,
                    heartbeat.ReportedAtUtc,
                    HeartbeatAgeSeconds = Math.Round(ageSeconds, 1),
                    HeartbeatIntervalSeconds = intervalSeconds
                }
            };
        }

        /// <summary>
        /// Builds the Admin API status from this process's registered readiness health checks,
        /// mirroring the <c>/health/ready</c> endpoint (#1067). Replaces the previous hardcoded
        /// "healthy" with a real status and a measured response time.
        /// </summary>
        private static async Task<ServiceStatusDto> BuildAdminStatusAsync(
            HealthCheckService healthCheckService,
            int configuredKeys,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            HealthReport report;
            try
            {
                // Same predicate as MapHealthChecks("/health/ready"): "ready"-tagged (or untagged) checks.
                report = await healthCheckService.CheckHealthAsync(
                    registration => registration.Tags.Contains("ready") || registration.Tags.Count == 0,
                    cancellationToken);
            }
            catch (Exception)
            {
                stopwatch.Stop();
                return new ServiceStatusDto
                {
                    Id = "admin-api",
                    Name = "Admin API",
                    Status = "unhealthy",
                    Uptime = GetProcessUptime(),
                    LastCheck = DateTime.UtcNow,
                    ResponseTime = (int)stopwatch.ElapsedMilliseconds,
                    Details = new { ConfiguredKeys = configuredKeys, Error = "Health check execution failed" }
                };
            }
            stopwatch.Stop();

            return new ServiceStatusDto
            {
                Id = "admin-api",
                Name = "Admin API",
                Status = MapHealthStatus(report.Status),
                Uptime = GetProcessUptime(),
                LastCheck = DateTime.UtcNow,
                ResponseTime = (int)stopwatch.ElapsedMilliseconds,
                Details = new
                {
                    ConfiguredKeys = configuredKeys,
                    Checks = report.Entries.Select(entry => new
                    {
                        Name = entry.Key,
                        Status = entry.Value.Status.ToString(),
                        entry.Value.Description,
                        DurationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1)
                    }).ToArray()
                }
            };
        }

        private static string MapHealthStatus(HealthStatus status) => status switch
        {
            HealthStatus.Healthy => "healthy",
            HealthStatus.Degraded => "degraded",
            _ => "unhealthy"
        };

        private static async Task<(bool IsHealthy, int ResponseTime)> CheckDatabaseHealth(
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

        /// <summary>
        /// Best-effort PostgreSQL server uptime via <c>pg_postmaster_start_time()</c>. Returns
        /// <c>null</c> on any failure or for a non-PostgreSQL provider, replacing the previous
        /// hardcoded 30-day placeholder.
        /// </summary>
        private static async Task<TimeSpan?> GetDatabaseUptimeAsync(
            ConduitDbContext dbContext,
            CancellationToken cancellationToken)
        {
            try
            {
                var providerName = dbContext.Database.ProviderName ?? string.Empty;
                if (!providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var seconds = await dbContext.Database
                    .SqlQueryRaw<double>(
                        "SELECT EXTRACT(EPOCH FROM (now() - pg_postmaster_start_time()))::double precision AS \"Value\"")
                    .FirstOrDefaultAsync(cancellationToken);

                return seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;
            }
            catch
            {
                return null; // best effort — uptime is informational only
            }
        }

        private static async Task<string> GetDatabaseSize(IAdminSystemInfoService systemInfoService)
        {
            try
            {
                var systemInfo = await systemInfoService.GetSystemInfoAsync();
                var size = systemInfo.Database.Size;
                return !string.IsNullOrEmpty(size) ? size : "Unknown";
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
