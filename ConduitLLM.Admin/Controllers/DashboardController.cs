using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller providing optimized endpoints for dashboard data visualization.
    /// </summary>
    [ApiController]
    [Route("api/dashboard")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class DashboardController : ControllerBase
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<DashboardController> _logger;
        private readonly IMemoryCache _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="DashboardController"/> class.
        /// </summary>
        /// <param name="dbContextFactory">Database context factory.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="cache">Memory cache.</param>
        public DashboardController(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<DashboardController> logger,
            IMemoryCache cache)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>
        /// Gets real-time metrics for the metrics dashboard.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Aggregated metrics data optimized for dashboard display.</returns>
        [HttpGet("metrics/realtime")]
        public async Task<IActionResult> GetRealtimeMetrics(CancellationToken cancellationToken = default)
        {
            try
            {
                var cacheKey = "dashboard:metrics:realtime";
                if (_cache.TryGetValue(cacheKey, out var cachedData))
                {
                    return Ok(cachedData);
                }

                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                var now = DateTime.UtcNow;
                var oneHourAgo = now.AddHours(-1);
                var oneDayAgo = now.AddDays(-1);

                // Get request metrics for the last hour
                var recentRequests = await dbContext.RequestLogs
                    .Where(r => r.Timestamp >= oneHourAgo)
                    .GroupBy(r => r.ModelName)
                    .Select(g => new
                    {
                        Model = g.Key,
                        RequestCount = g.Count(),
                        AvgLatency = g.Average(r => r.ResponseTimeMs),
                        TotalTokens = g.Sum(r => r.InputTokens + r.OutputTokens),
                        TotalCost = g.Sum(r => r.Cost),
                        ErrorRate = g.Count(r => r.StatusCode >= 400) * 100.0 / g.Count()
                    })
                    .ToListAsync(cancellationToken);

                // Get provider status from health records
                var providerStatus = await dbContext.ProviderCredentials
                    .Select(p => new
                    {
                        p.ProviderName,
                        p.IsEnabled,
                        LastHealthCheck = dbContext.ProviderHealthRecords
                            .Where(h => h.ProviderName == p.ProviderName)
                            .OrderByDescending(h => h.TimestampUtc)
                            .Select(h => new { IsHealthy = h.IsOnline, CheckedAt = h.TimestampUtc, ResponseTime = h.ResponseTimeMs })
                            .FirstOrDefault()
                    })
                    .ToListAsync(cancellationToken);

                // Get virtual key metrics
                var keyMetrics = await dbContext.VirtualKeys
                    .Where(k => k.IsEnabled)
                    .Select(k => new
                    {
                        k.Id,
                        Name = k.KeyName,
                        RequestsToday = dbContext.RequestLogs
                            .Count(r => r.VirtualKeyId == k.Id && r.Timestamp >= DateTime.UtcNow.Date),
                        CostToday = dbContext.RequestLogs
                            .Where(r => r.VirtualKeyId == k.Id && r.Timestamp >= DateTime.UtcNow.Date)
                            .Sum(r => r.Cost),
                        BudgetUtilization = k.MaxBudget.HasValue && k.MaxBudget > 0 
                            ? (k.CurrentSpend / k.MaxBudget.Value) * 100
                            : 0
                    })
                    .ToListAsync(cancellationToken);

                // Calculate system-wide metrics
                var systemMetrics = new
                {
                    TotalRequestsHour = await dbContext.RequestLogs.CountAsync(r => r.Timestamp >= oneHourAgo, cancellationToken),
                    TotalRequestsDay = await dbContext.RequestLogs.CountAsync(r => r.Timestamp >= oneDayAgo, cancellationToken),
                    AvgLatencyHour = await dbContext.RequestLogs
                        .Where(r => r.Timestamp >= oneHourAgo)
                        .AverageAsync(r => (double?)r.ResponseTimeMs ?? 0, cancellationToken),
                    ErrorRateHour = await dbContext.RequestLogs
                        .Where(r => r.Timestamp >= oneHourAgo)
                        .CountAsync(r => r.StatusCode >= 400, cancellationToken) * 100.0 /
                        Math.Max(1, await dbContext.RequestLogs.CountAsync(r => r.Timestamp >= oneHourAgo, cancellationToken)),
                    ActiveProviders = providerStatus.Count(p => p.IsEnabled && p.LastHealthCheck?.IsHealthy == true),
                    ActiveKeys = await dbContext.VirtualKeys.CountAsync(k => k.IsEnabled, cancellationToken)
                };

                var result = new
                {
                    Timestamp = now,
                    System = systemMetrics,
                    ModelMetrics = recentRequests,
                    ProviderStatus = providerStatus,
                    TopKeys = keyMetrics.OrderByDescending(k => k.RequestsToday).Take(10),
                    RefreshIntervalSeconds = 10
                };

                // Cache for 10 seconds
                _cache.Set(cacheKey, result, TimeSpan.FromSeconds(10));

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve realtime metrics");
                return StatusCode(500, new { error = "Failed to retrieve metrics", message = ex.Message });
            }
        }

        /// <summary>
        /// Gets time-series data for dashboard charts.
        /// </summary>
        /// <param name="period">Time period (hour, day, week, month).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Time-series data optimized for charting.</returns>
        [HttpGet("metrics/timeseries")]
        public async Task<IActionResult> GetTimeSeriesMetrics(
            [FromQuery] string period = "hour",
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                var now = DateTime.UtcNow;
                DateTime startTime;
                int intervalMinutes;

                switch (period.ToLower())
                {
                    case "hour":
                        startTime = now.AddHours(-1);
                        intervalMinutes = 1;
                        break;
                    case "day":
                        startTime = now.AddDays(-1);
                        intervalMinutes = 30;
                        break;
                    case "week":
                        startTime = now.AddDays(-7);
                        intervalMinutes = 240; // 4 hours
                        break;
                    case "month":
                        startTime = now.AddMonths(-1);
                        intervalMinutes = 1440; // 1 day
                        break;
                    default:
                        return BadRequest(new { error = "Invalid period. Use: hour, day, week, or month" });
                }

                var timeSeriesData = await GetTimeSeriesData(dbContext, startTime, now, intervalMinutes, cancellationToken);

                return Ok(new
                {
                    Period = period,
                    StartTime = startTime,
                    EndTime = now,
                    IntervalMinutes = intervalMinutes,
                    Series = timeSeriesData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve time series metrics");
                return StatusCode(500, new { error = "Failed to retrieve metrics", message = ex.Message });
            }
        }

        /// <summary>
        /// Gets provider-specific performance metrics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Provider performance data.</returns>
        [HttpGet("metrics/providers")]
        public async Task<IActionResult> GetProviderMetrics(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                var oneDayAgo = DateTime.UtcNow.AddDays(-1);

                // Group by model name as proxy for provider metrics
                var modelMetrics = await dbContext.RequestLogs
                    .Where(r => r.Timestamp >= oneDayAgo)
                    .GroupBy(r => r.ModelName)
                    .Select(g => new
                    {
                        Model = g.Key,
                        Metrics = new
                        {
                            TotalRequests = g.Count(),
                            SuccessfulRequests = g.Count(r => r.StatusCode < 400),
                            FailedRequests = g.Count(r => r.StatusCode >= 400),
                            AvgLatency = g.Average(r => r.ResponseTimeMs),
                            P95Latency = 0.0, // Would need percentile calculation
                            TotalCost = g.Sum(r => r.Cost),
                            TotalTokens = g.Sum(r => r.InputTokens + r.OutputTokens)
                        }
                    })
                    .ToListAsync(cancellationToken);

                // Get health history
                var healthHistory = await dbContext.ProviderHealthRecords
                    .Where(h => h.TimestampUtc >= oneDayAgo)
                    .GroupBy(h => h.ProviderName)
                    .Select(g => new
                    {
                        Provider = g.Key,
                        HealthChecks = g.Count(),
                        SuccessRate = g.Count(h => h.IsOnline) * 100.0 / g.Count(),
                        AvgResponseTime = g.Average(h => h.ResponseTimeMs),
                        LastCheck = g.Max(h => h.TimestampUtc)
                    })
                    .ToListAsync(cancellationToken);

                return Ok(new
                {
                    Timestamp = DateTime.UtcNow,
                    ModelMetrics = modelMetrics,
                    HealthHistory = healthHistory
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve provider metrics");
                return StatusCode(500, new { error = "Failed to retrieve metrics", message = ex.Message });
            }
        }

        private async Task<List<object>> GetTimeSeriesData(
            ConfigurationDbContext dbContext,
            DateTime startTime,
            DateTime endTime,
            int intervalMinutes,
            CancellationToken cancellationToken)
        {
            var intervals = new List<object>();
            var currentTime = startTime;

            while (currentTime < endTime)
            {
                var intervalEnd = currentTime.AddMinutes(intervalMinutes);
                
                var intervalData = await dbContext.RequestLogs
                    .Where(r => r.Timestamp >= currentTime && r.Timestamp < intervalEnd)
                    .GroupBy(r => 1)
                    .Select(g => new
                    {
                        Timestamp = currentTime,
                        Requests = g.Count(),
                        AvgLatency = g.Average(r => (double?)r.ResponseTimeMs) ?? 0,
                        Errors = g.Count(r => r.StatusCode >= 400),
                        TotalCost = g.Sum(r => r.Cost),
                        TotalTokens = g.Sum(r => r.InputTokens + r.OutputTokens)
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                intervals.Add(intervalData ?? new
                {
                    Timestamp = currentTime,
                    Requests = 0,
                    AvgLatency = 0.0,
                    Errors = 0,
                    TotalCost = 0m,
                    TotalTokens = 0
                });

                currentTime = intervalEnd;
            }

            return intervals;
        }
    }
}