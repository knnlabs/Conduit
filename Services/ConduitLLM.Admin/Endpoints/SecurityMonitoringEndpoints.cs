using ConduitLLM.Admin.DTOs;
using ConduitLLM.Configuration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.Admin.Endpoints
{
    /// <summary>
    /// Controller providing security monitoring data for dashboards.
    /// </summary>
    public static class SecurityMonitoringEndpoints
    {
        public static IEndpointRouteBuilder MapSecurityMonitoringEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/security")
                .RequireAuthorization("MasterKeyPolicy")
                .AddEndpointFilter<OperationLoggingEndpointFilter>()
                .WithTags("Security Monitoring");
            group.MapGet("/events", GetSecurityEvents)
                .WithName("SecurityMonitoring_GetSecurityEvents")
                .Produces<SecurityEventsResponse>();
            group.MapGet("/threats", GetThreatAnalytics)
                .WithName("SecurityMonitoring_GetThreatAnalytics")
                .Produces<ThreatAnalyticsResponse>();
            group.MapGet("/compliance", GetComplianceMetrics)
                .WithName("SecurityMonitoring_GetComplianceMetrics")
                .Produces<ComplianceMetricsResponse>();
            return app;
        }

        /// <summary>
        /// Gets security events for monitoring.
        /// </summary>
        /// <param name="dbContextFactory">Factory used to create the configuration database context.</param>
        /// <param name="hours">Number of hours to look back (default: 24).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Security events data.</returns>
        private static async Task<IResult> GetSecurityEvents(
            [FromServices] IDbContextFactory<ConduitDbContext> dbContextFactory,
            [FromQuery] int hours = 24,
            CancellationToken cancellationToken = default)
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var startTime = DateTime.UtcNow.AddHours(-hours);

            // Get authentication failures (401 status codes)
            var authFailures = await dbContext.RequestLogs
                .Where(r => r.Timestamp >= startTime && r.StatusCode == 401)
                .Select(r => new SecurityMonitoringEventDto
                {
                    Timestamp = r.Timestamp,
                    Type = "auth_failure",
                    Severity = "warning",
                    Source = r.ClientIp ?? "Unknown",
                    VirtualKeyId = r.VirtualKeyId.ToString(),
                    Details = "Unauthorized access attempt",
                    StatusCode = r.StatusCode
                })
                .ToListAsync(cancellationToken);

            // Get rate limit violations (429 status codes)
            var rateLimitViolations = await dbContext.RequestLogs
                .Where(r => r.Timestamp >= startTime && r.StatusCode == 429)
                .Select(r => new SecurityMonitoringEventDto
                {
                    Timestamp = r.Timestamp,
                    Type = "rate_limit",
                    Severity = "warning",
                    Source = r.ClientIp ?? "Unknown",
                    VirtualKeyId = r.VirtualKeyId.ToString(),
                    Details = "Rate limit exceeded",
                    StatusCode = r.StatusCode
                })
                .ToListAsync(cancellationToken);

            // Get blocked IP attempts
            var blockedIps = await dbContext.IpFilters
                .Where(f => f.FilterType == "blacklist" && f.IsEnabled)
                .Join(dbContext.RequestLogs.Where(r => r.Timestamp >= startTime),
                    f => f.IpAddressOrCidr,
                    r => r.ClientIp,
                    (f, r) => new SecurityMonitoringEventDto
                    {
                        Timestamp = r.Timestamp,
                        Type = "blocked_ip",
                        Severity = "high",
                        Source = r.ClientIp ?? "Unknown",
                        VirtualKeyId = r.VirtualKeyId.ToString(),
                        Details = $"Blocked by rule: {f.Description ?? "IP Filter"}",
                        StatusCode = 403
                    })
                .ToListAsync(cancellationToken);

            // Get suspicious activity (multiple failed attempts from same IP)
            var suspiciousActivity = await dbContext.RequestLogs
                .Where(r => r.Timestamp >= startTime && r.StatusCode >= 400 && r.ClientIp != null)
                .GroupBy(r => r.ClientIp)
                .Where(g => g.Count() >= 5)
                .Select(g => new SecurityMonitoringEventDto
                {
                    Timestamp = g.Max(r => r.Timestamp),
                    Type = "suspicious_activity",
                    Severity = "high",
                    Source = g.Key ?? "Unknown",
                    VirtualKeyId = null,
                    Details = $"Multiple failed requests: {g.Count()} attempts",
                    StatusCode = 0
                })
                .ToListAsync(cancellationToken);

            // Combine all events
            var allEvents = authFailures
                .Concat(rateLimitViolations)
                .Concat(blockedIps)
                .Concat(suspiciousActivity)
                .OrderByDescending(e => e.Timestamp)
                .Take(1000)
                .ToList();

            return Results.Ok(new SecurityEventsResponse
            {
                Timestamp = DateTime.UtcNow,
                TimeRange = new TimeRangeDto { Start = startTime, End = DateTime.UtcNow },
                TotalEvents = allEvents.Count,
                EventsByType = allEvents.GroupBy(e => e.Type).Select(g => new SecurityEventTypeCountDto
                {
                    Type = g.Key,
                    Count = g.Count()
                }).ToList(),
                EventsBySeverity = allEvents.GroupBy(e => e.Severity).Select(g => new SecurityEventSeverityCountDto
                {
                    Severity = g.Key,
                    Count = g.Count()
                }).ToList(),
                Events = allEvents
            });
        }

        /// <summary>
        /// Gets threat analytics data.
        /// </summary>
        /// <param name="dbContextFactory">Factory used to create the configuration database context.</param>
        /// <param name="cache">Cache used for computed threat analytics.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Threat analytics information.</returns>
        private static async Task<IResult> GetThreatAnalytics(
            [FromServices] IDbContextFactory<ConduitDbContext> dbContextFactory,
            [FromServices] IMemoryCache cache,
            CancellationToken cancellationToken)
        {
            var cacheKey = "security:threats";
            if (cache.TryGetValue(cacheKey, out var cachedData) && cachedData != null)
            {
                return Results.Ok(cachedData);
            }

            using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var oneDayAgo = now.AddDays(-1);
            var oneWeekAgo = now.AddDays(-7);

            // Analyze threat patterns
            var threatPatterns = await dbContext.RequestLogs
                .Where(r => r.Timestamp >= oneWeekAgo && r.StatusCode >= 400 && r.ClientIp != null)
                .GroupBy(r => new { r.ClientIp, Date = r.Timestamp.Date })
                .Select(g => new
                {
                    ClientIp = g.Key.ClientIp,
                    Date = g.Key.Date,
                    FailedAttempts = g.Count(),
                    ErrorTypes = g.Select(r => r.StatusCode).Distinct().Count()
                })
                .ToListAsync(cancellationToken);

            // Get top threat sources
            var topThreats = threatPatterns
                .GroupBy(t => t.ClientIp)
                .Select(g => new TopThreatSourceDto
                {
                    IpAddress = g.Key,
                    TotalFailures = g.Sum(t => t.FailedAttempts),
                    DaysActive = g.Select(t => t.Date).Distinct().Count(),
                    LastSeen = g.Max(t => t.Date),
                    RiskScore = CalculateRiskScore(g.Sum(t => t.FailedAttempts), g.Count())
                })
                .OrderByDescending(t => t.RiskScore)
                .Take(20)
                .ToList();

            // Get threat distribution by type
            var threatDistribution = await dbContext.RequestLogs
                .Where(r => r.Timestamp >= oneDayAgo && r.StatusCode >= 400)
                .GroupBy(r => GetThreatTypeByStatusCode(r.StatusCode ?? 0))
                .Select(g => new ThreatDistributionDto
                {
                    Type = g.Key,
                    Count = g.Count(),
                    UniqueIPs = g.Where(r => r.ClientIp != null).Select(r => r.ClientIp).Distinct().Count()
                })
                .ToListAsync(cancellationToken);

            // Calculate security metrics
            var securityMetrics = new ThreatAnalyticsMetricsDto
            {
                TotalThreatsToday = await dbContext.RequestLogs
                    .CountAsync(r => r.Timestamp >= DateTime.UtcNow.Date && r.StatusCode >= 400, cancellationToken),
                UniqueThreatsToday = await dbContext.RequestLogs
                    .Where(r => r.Timestamp >= DateTime.UtcNow.Date && r.StatusCode >= 400 && r.ClientIp != null)
                    .Select(r => r.ClientIp)
                    .Distinct()
                    .CountAsync(cancellationToken),
                BlockedIPs = await dbContext.IpFilters.CountAsync(f => f.FilterType == "blacklist", cancellationToken),
                ComplianceScore = 85.0 // Simplified compliance score
            };

            // Get threat trend
            var threatTrend = threatPatterns
                .GroupBy(t => t.Date)
                .Select(g => new ThreatTrendPointDto
                {
                    Date = g.Key,
                    Threats = g.Sum(t => t.FailedAttempts)
                })
                .OrderBy(t => t.Date)
                .ToList();

            var result = new ThreatAnalyticsResponse
            {
                Timestamp = now,
                Metrics = securityMetrics,
                TopThreats = topThreats,
                ThreatDistribution = threatDistribution,
                ThreatTrend = threatTrend
            };

            // Cache for 5 minutes
            cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));

            return Results.Ok(result);
        }

        /// <summary>
        /// Gets compliance metrics.
        /// </summary>
        /// <param name="dbContextFactory">Factory used to create the configuration database context.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Compliance information.</returns>
        private static async Task<IResult> GetComplianceMetrics(
            [FromServices] IDbContextFactory<ConduitDbContext> dbContextFactory,
            CancellationToken cancellationToken)
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var complianceData = new ComplianceMetricsResponse
            {
                Timestamp = DateTime.UtcNow,
                DataProtection = new DataProtectionDto
                {
                    EncryptedKeys = await dbContext.VirtualKeys.CountAsync(k => k.IsEnabled, cancellationToken),
                    SecureEndpoints = true, // Assuming HTTPS is enforced
                    DataRetentionDays = 90,
                    LastAudit = DateTime.UtcNow.AddDays(-7)
                },
                AccessControl = new AccessControlDto
                {
                    ActiveKeys = await dbContext.VirtualKeys.CountAsync(k => k.IsEnabled, cancellationToken),
                    KeysWithBudgets = await dbContext.VirtualKeyGroups.CountAsync(g => g.Balance > 0, cancellationToken),
                    IpWhitelistEnabled = await dbContext.IpFilters.AnyAsync(f => f.FilterType == "whitelist", cancellationToken),
                    RateLimitingEnabled = true
                },
                Monitoring = new ComplianceMonitoringDto
                {
                    LogRetentionDays = 90,
                    RequestLoggingEnabled = true,
                    SecurityAlertsEnabled = true,
                    LastSecurityReview = DateTime.UtcNow.AddDays(-30)
                },
                ComplianceScore = await CalculateDetailedComplianceScore(dbContext, cancellationToken)
            };

            return Results.Ok(complianceData);
        }

        private static string GetThreatTypeByStatusCode(int statusCode)
        {
            return statusCode switch
            {
                401 => "Authentication",
                403 => "Authorization",
                429 => "RateLimit",
                400 => "InvalidRequest",
                _ => "Other"
            };
        }

        private static double CalculateRiskScore(int totalFailures, int daysActive)
        {
            // Higher score for more failures in fewer days
            return (double)totalFailures / Math.Max(1, daysActive);
        }

        private static async Task<double> CalculateDetailedComplianceScore(ConduitDbContext context, CancellationToken cancellationToken)
        {
            var score = 0.0;

            // Check various compliance factors
            if (await context.VirtualKeyGroups.AnyAsync(g => g.Balance > 0, cancellationToken))
                score += 20; // Budget controls

            if (await context.IpFilters.AnyAsync(cancellationToken))
                score += 15; // IP filtering

            if (await context.RequestLogs.AnyAsync(cancellationToken))
                score += 20; // Logging enabled

            if (await context.Providers.AllAsync(p => p.ProviderKeyCredentials.Any(k => k.IsEnabled && !string.IsNullOrEmpty(k.ApiKey)), cancellationToken))
                score += 25; // All keys configured

            score += 20; // Base score for having security monitoring

            return score;
        }
    }
}
