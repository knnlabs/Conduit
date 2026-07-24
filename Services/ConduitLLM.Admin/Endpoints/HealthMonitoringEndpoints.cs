using System.Diagnostics;

using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Messaging.Wolverine;
using ConduitLLM.Configuration.Utilities;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using StackExchange.Redis;

namespace ConduitLLM.Admin.Endpoints
{
    /// <summary>
    /// Controller providing health monitoring data for dashboards.
    /// </summary>
    public static class HealthMonitoringEndpoints
    {
        public static IEndpointRouteBuilder MapHealthMonitoringEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/v1/admin/health-status")
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
        /// <param name="healthCheckService">The Admin's registered ASP.NET health checks.</param>
        /// <param name="heartbeatStore">Store of cross-service liveness heartbeats.</param>
        /// <param name="configuration">Runtime configuration used to identify messaging mode.</param>
        /// <param name="hostEnvironment">Hosting environment used to assess in-memory messaging.</param>
        /// <param name="mediaStorageProbe">Non-mutating probe for the configured media store.</param>
        /// <param name="serviceProvider">Service provider used to resolve optional Redis connectivity.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Service health information.</returns>
        private static async Task<IResult> GetServiceHealth(
            [FromServices] IDbContextFactory<ConduitDbContext> dbContextFactory,
            [FromServices] HealthCheckService healthCheckService,
            [FromServices] IServiceHeartbeatStore heartbeatStore,
            [FromServices] IConfiguration configuration,
            [FromServices] IHostEnvironment hostEnvironment,
            [FromServices] IMediaStorageHealthProbe mediaStorageProbe,
            [FromServices] IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var services = new List<ServiceStatusDto>();

            var gatewayHeartbeatsTask = heartbeatStore.GetAllAsync(
                RedisKeys.ServiceHeartbeat.GatewayServiceId, cancellationToken);
            var adminHeartbeatsTask = heartbeatStore.GetAllAsync(
                RedisKeys.ServiceHeartbeat.AdminServiceId, cancellationToken);
            await Task.WhenAll(gatewayHeartbeatsTask, adminHeartbeatsTask);

            services.Add(BuildClusterServiceStatus(
                "core-api",
                "Gateway API",
                gatewayHeartbeatsTask.Result));
            services.Add(BuildClusterServiceStatus(
                "admin-api",
                "Admin API",
                adminHeartbeatsTask.Result));

            // Database — genuinely probed (SELECT 1) with a real response time and best-effort
            // server uptime (replacing the previous 30-day placeholder).
            var dbHealthCheck = await CheckDatabaseHealth(dbContext, cancellationToken);
            var dbUptime = await GetDatabaseUptimeAsync(dbContext, cancellationToken);
            var dbServerVersion = await GetDatabaseServerVersionAsync(dbContext, cancellationToken);
            services.Add(new ServiceStatusDto
            {
                Id = "database",
                Name = "PostgreSQL Database",
                Status = dbHealthCheck.IsHealthy ? "healthy" : "unhealthy",
                Version = dbServerVersion,
                Uptime = dbUptime,
                LastCheck = DateTime.UtcNow,
                ResponseTime = dbHealthCheck.ResponseTime,
                Details = new
                {
                    ConnectionPooling = true,
                    Provider = dbContext.Database.ProviderName ?? "unknown"
                }
            });

            services.Add(await BuildRedisStatusAsync(
                serviceProvider.GetService<IConnectionMultiplexer>(),
                cancellationToken));
            services.Add(await BuildMessagingStatusAsync(
                healthCheckService,
                configuration,
                hostEnvironment,
                cancellationToken));
            services.Add(await BuildMediaStorageStatusAsync(mediaStorageProbe, cancellationToken));

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

        internal static ServiceStatusDto BuildClusterServiceStatus(
            string id,
            string name,
            IReadOnlyList<ServiceHeartbeatSnapshot> heartbeats,
            DateTime? nowUtc = null)
        {
            var now = nowUtc ?? DateTime.UtcNow;
            if (heartbeats.Count == 0)
            {
                return new ServiceStatusDto
                {
                    Id = id,
                    Name = name,
                    Status = "unknown",
                    LastCheck = now,
                    Details = new
                    {
                        Source = "heartbeat",
                        HealthyInstances = 0,
                        TotalInstances = 0,
                        Reason = "No instance heartbeat has been received"
                    }
                };
            }

            var instances = heartbeats.Select(heartbeat =>
            {
                var interval = heartbeat.IntervalSeconds > 0
                    ? heartbeat.IntervalSeconds
                    : ServiceHeartbeatEvaluator.DefaultIntervalSeconds;
                var age = Math.Max(0, (now - heartbeat.ReceivedAtUtc).TotalSeconds);
                var freshness = ServiceHeartbeatEvaluator.EvaluateStatus(age, interval);
                var reported = NormalizeReportedStatus(heartbeat.Status);
                return new ServiceInstanceStatusDto
                {
                    InstanceId = heartbeat.InstanceId,
                    Status = WorstStatus(freshness, reported),
                    Version = heartbeat.Version,
                    CommitSha = heartbeat.CommitSha,
                    BuildTimestamp = heartbeat.BuildTimestamp,
                    Uptime = TimeSpan.FromSeconds(Math.Max(0, heartbeat.UptimeSeconds)),
                    LastHeartbeat = heartbeat.ReceivedAtUtc,
                    HeartbeatAgeSeconds = Math.Round(age, 1),
                    HeartbeatIntervalSeconds = interval
                };
            }).OrderBy(instance => instance.InstanceId, StringComparer.Ordinal).ToList();

            var healthy = instances.Count(instance => instance.Status == "healthy");
            var versions = instances.Select(instance => instance.Version).Distinct(StringComparer.Ordinal).ToArray();
            return new ServiceStatusDto
            {
                Id = id,
                Name = name,
                Status = healthy == instances.Count
                    ? "healthy"
                    : healthy > 0
                        ? "degraded"
                        : "unhealthy",
                Version = versions.Length == 1 ? versions[0] : "mixed",
                Instances = instances,
                Uptime = instances.Max(instance => instance.Uptime),
                LastCheck = instances.Max(instance => instance.LastHeartbeat),
                ResponseTime = null,
                Details = new
                {
                    Source = "heartbeat",
                    HealthyInstances = healthy,
                    TotalInstances = instances.Count
                }
            };
        }

        internal static async Task<ServiceStatusDto> BuildRedisStatusAsync(
            IConnectionMultiplexer? redis,
            CancellationToken cancellationToken,
            bool? configuredOverride = null)
        {
            var configured = configuredOverride
                ?? !string.IsNullOrWhiteSpace(RedisUrlParser.ResolveConnectionString());
            if (!configured)
            {
                return new ServiceStatusDto
                {
                    Id = "redis",
                    Name = "Redis",
                    Status = "degraded",
                    LastCheck = DateTime.UtcNow,
                    Details = new
                    {
                        Configured = false,
                        Mode = "in-memory fallback",
                        Description = "Redis is not configured; cluster-wide ephemeral state is unavailable"
                    }
                };
            }

            if (redis == null)
            {
                return new ServiceStatusDto
                {
                    Id = "redis",
                    Name = "Redis",
                    Status = "unhealthy",
                    LastCheck = DateTime.UtcNow,
                    Details = new { Configured = true, Error = "Redis connection is unavailable" }
                };
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var ping = await redis.GetDatabase().PingAsync();
                var endpoint = redis.GetEndPoints().FirstOrDefault();
                var version = endpoint == null ? null : redis.GetServer(endpoint).Version?.ToString();
                stopwatch.Stop();
                return new ServiceStatusDto
                {
                    Id = "redis",
                    Name = "Redis",
                    Status = "healthy",
                    Version = version,
                    LastCheck = DateTime.UtcNow,
                    ResponseTime = (int)stopwatch.ElapsedMilliseconds,
                    Details = new
                    {
                        Configured = true,
                        PingMilliseconds = Math.Round(ping.TotalMilliseconds, 1)
                    }
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                stopwatch.Stop();
                return new ServiceStatusDto
                {
                    Id = "redis",
                    Name = "Redis",
                    Status = "unhealthy",
                    LastCheck = DateTime.UtcNow,
                    ResponseTime = (int)stopwatch.ElapsedMilliseconds,
                    Details = new { Configured = true, Error = ex.Message }
                };
            }
        }

        internal static async Task<ServiceStatusDto> BuildMessagingStatusAsync(
            HealthCheckService healthCheckService,
            IConfiguration configuration,
            IHostEnvironment hostEnvironment,
            CancellationToken cancellationToken)
        {
            if (WolverineMessagingExtensions.UsesInMemoryTransport(configuration))
            {
                return new ServiceStatusDto
                {
                    Id = "messaging",
                    Name = "Messaging",
                    Status = hostEnvironment.IsDevelopment() ? "healthy" : "degraded",
                    LastCheck = DateTime.UtcNow,
                    Details = new
                    {
                        Backend = "Wolverine",
                        Transport = "in-memory",
                        Durable = false,
                        Description = hostEnvironment.IsDevelopment()
                            ? "In-memory transport is expected in Development"
                            : "In-memory transport is non-durable outside Development"
                    }
                };
            }

            try
            {
                var report = await healthCheckService.CheckHealthAsync(
                    registration => registration.Tags.Contains("messaging"),
                    cancellationToken);
                var hasChecks = report.Entries.Count > 0;
                return new ServiceStatusDto
                {
                    Id = "messaging",
                    Name = "Messaging",
                    Status = hasChecks ? MapHealthStatus(report.Status) : "unhealthy",
                    LastCheck = DateTime.UtcNow,
                    ResponseTime = (int)report.TotalDuration.TotalMilliseconds,
                    Details = new
                    {
                        Backend = "Wolverine",
                        Transport = "PostgreSQL",
                        Durable = true,
                        Checks = report.Entries.Select(entry => new
                        {
                            Name = entry.Key,
                            Status = entry.Value.Status.ToString(),
                            entry.Value.Description
                        }).ToArray(),
                        Error = hasChecks ? null : "No messaging readiness check is registered"
                    }
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new ServiceStatusDto
                {
                    Id = "messaging",
                    Name = "Messaging",
                    Status = "unhealthy",
                    LastCheck = DateTime.UtcNow,
                    Details = new { Backend = "Wolverine", Error = ex.Message }
                };
            }
        }

        internal static async Task<ServiceStatusDto> BuildMediaStorageStatusAsync(
            IMediaStorageHealthProbe probe,
            CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = await probe.ProbeAsync(timeout.Token);
                stopwatch.Stop();
                return new ServiceStatusDto
                {
                    Id = "media-storage",
                    Name = "Media Storage",
                    Status = result.Status,
                    LastCheck = DateTime.UtcNow,
                    ResponseTime = (int)stopwatch.ElapsedMilliseconds,
                    Details = new
                    {
                        result.Mode,
                        result.Description,
                        result.Ephemeral,
                        result.Bucket,
                        result.Endpoint
                    }
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException
                || !cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                return new ServiceStatusDto
                {
                    Id = "media-storage",
                    Name = "Media Storage",
                    Status = "unhealthy",
                    LastCheck = DateTime.UtcNow,
                    ResponseTime = (int)stopwatch.ElapsedMilliseconds,
                    Details = new
                    {
                        Mode = "s3",
                        Error = timeout.IsCancellationRequested
                            ? "S3 bucket probe timed out after 3 seconds"
                            : ex.Message
                    }
                };
            }
        }

        private static string NormalizeReportedStatus(string? status) =>
            status?.ToLowerInvariant() is "healthy" or "degraded" or "unhealthy"
                ? status.ToLowerInvariant()
                : "healthy";

        private static string WorstStatus(string left, string right) =>
            StatusRank(left) >= StatusRank(right) ? left : right;

        private static int StatusRank(string status) => status switch
        {
            "unhealthy" => 2,
            "degraded" => 1,
            _ => 0
        };

        private static string MapHealthStatus(
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus status) => status switch
        {
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy => "healthy",
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded => "degraded",
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
        /// Best-effort database server version. <see cref="System.Data.Common.DbConnection.ServerVersion"/>
        /// requires an <b>open</b> connection; the preceding SELECT 1 / uptime probes let EF close the
        /// pooled connection again, so we must reopen before reading it. Returns <c>null</c> on any
        /// failure rather than throwing — a version string is informational and must never fail the
        /// whole health response (previously surfaced as an HTTP 400 for the entire endpoint).
        /// </summary>
        private static async Task<string?> GetDatabaseServerVersionAsync(
            ConduitDbContext dbContext,
            CancellationToken cancellationToken)
        {
            try
            {
                var connection = dbContext.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken);
                }

                return connection.ServerVersion;
            }
            catch
            {
                return null; // best effort — version is informational only
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

    }
}
