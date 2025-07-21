using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ConduitLLM.Admin.Services;
using ConduitLLM.Core.Services;
using ConduitLLM.Core.Alerts;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// API endpoints for cache monitoring and alerting configuration
    /// </summary>
    [ApiController]
    [Route("api/cache/monitoring")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [Produces("application/json")]
    public class CacheMonitoringController : ControllerBase
    {
        private readonly ICacheMonitoringService _monitoringService;
        private readonly ICacheManagementService _cacheManagementService;
        private readonly ILogger<CacheMonitoringController> _logger;

        /// <summary>
        /// Initializes a new instance of the CacheMonitoringController
        /// </summary>
        public CacheMonitoringController(
            ICacheMonitoringService monitoringService,
            ICacheManagementService cacheManagementService,
            ILogger<CacheMonitoringController> logger)
        {
            _monitoringService = monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));
            _cacheManagementService = cacheManagementService ?? throw new ArgumentNullException(nameof(cacheManagementService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the current cache monitoring status
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Current monitoring status including health metrics</returns>
        [HttpGet("status")]
        [ProducesResponseType(typeof(CacheMonitoringStatusDto), 200)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetMonitoringStatus(CancellationToken cancellationToken = default)
        {
            try
            {
                var status = await _monitoringService.GetStatusAsync(cancellationToken);
                
                var response = new CacheMonitoringStatusDto
                {
                    LastCheck = status.LastCheck,
                    IsHealthy = status.IsHealthy,
                    CurrentHitRate = status.CurrentHitRate,
                    CurrentMemoryUsagePercent = status.CurrentMemoryUsagePercent,
                    CurrentEvictionRate = status.CurrentEvictionRate,
                    CurrentResponseTimeMs = status.CurrentResponseTimeMs,
                    ActiveAlerts = status.ActiveAlerts,
                    Details = status.Details
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get cache monitoring status");
                return StatusCode(500, new { error = "Failed to retrieve monitoring status", message = ex.Message });
            }
        }

        /// <summary>
        /// Gets the current alert thresholds
        /// </summary>
        /// <returns>Current alert threshold configuration</returns>
        [HttpGet("thresholds")]
        [ProducesResponseType(typeof(CacheAlertThresholdsDto), 200)]
        public IActionResult GetAlertThresholds()
        {
            try
            {
                var thresholds = _monitoringService.GetThresholds();
                
                var response = new CacheAlertThresholdsDto
                {
                    MinHitRate = thresholds.MinHitRate,
                    MaxMemoryUsage = thresholds.MaxMemoryUsage,
                    MaxEvictionRate = thresholds.MaxEvictionRate,
                    MaxResponseTimeMs = thresholds.MaxResponseTimeMs,
                    MinRequestsForHitRateAlert = thresholds.MinRequestsForHitRateAlert
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get alert thresholds");
                return StatusCode(500, new { error = "Failed to retrieve alert thresholds", message = ex.Message });
            }
        }

        /// <summary>
        /// Updates the alert thresholds
        /// </summary>
        /// <param name="request">New threshold values</param>
        /// <returns>Updated threshold configuration</returns>
        [HttpPut("thresholds")]
        [ProducesResponseType(typeof(CacheAlertThresholdsDto), 200)]
        [ProducesResponseType(400)]
        public IActionResult UpdateAlertThresholds([FromBody] UpdateThresholdsRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { error = "Request body is required" });
            }

            try
            {
                var thresholds = new ConduitLLM.Core.Services.MonitoringAlertThresholds
                {
                    MinHitRate = request.MinHitRate ?? 0.5,
                    MaxMemoryUsage = request.MaxMemoryUsage ?? 0.85,
                    MaxEvictionRate = request.MaxEvictionRate ?? 100,
                    MaxResponseTimeMs = request.MaxResponseTimeMs ?? 100,
                    MinRequestsForHitRateAlert = request.MinRequestsForHitRateAlert ?? 100
                };

                _monitoringService.UpdateThresholds(thresholds);

                _logger.LogInformation("Cache alert thresholds updated: {@Thresholds}", thresholds);

                return Ok(new CacheAlertThresholdsDto
                {
                    MinHitRate = thresholds.MinHitRate,
                    MaxMemoryUsage = thresholds.MaxMemoryUsage,
                    MaxEvictionRate = thresholds.MaxEvictionRate,
                    MaxResponseTimeMs = thresholds.MaxResponseTimeMs,
                    MinRequestsForHitRateAlert = thresholds.MinRequestsForHitRateAlert
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update alert thresholds");
                return StatusCode(500, new { error = "Failed to update alert thresholds", message = ex.Message });
            }
        }

        /// <summary>
        /// Gets recent cache alerts
        /// </summary>
        /// <param name="count">Number of alerts to retrieve (default: 10, max: 100)</param>
        /// <returns>List of recent cache alerts</returns>
        [HttpGet("alerts")]
        [ProducesResponseType(typeof(List<CacheAlertDto>), 200)]
        public IActionResult GetRecentAlerts([FromQuery] int count = 10)
        {
            try
            {
                count = Math.Max(1, Math.Min(count, 100));
                var alerts = _monitoringService.GetRecentAlerts(count);
                
                var response = alerts.Select(a => new CacheAlertDto
                {
                    AlertType = a.AlertType,
                    Message = a.Message,
                    Severity = a.Severity,
                    Region = a.Region,
                    Details = a.Details,
                    Timestamp = a.Timestamp
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get recent alerts");
                return StatusCode(500, new { error = "Failed to retrieve recent alerts", message = ex.Message });
            }
        }

        /// <summary>
        /// Clears the alert history
        /// </summary>
        /// <returns>Success response</returns>
        [HttpDelete("alerts")]
        [ProducesResponseType(200)]
        public IActionResult ClearAlertHistory()
        {
            try
            {
                _monitoringService.ClearAlertHistory();
                _logger.LogInformation("Cache alert history cleared");
                
                return Ok(new { message = "Alert history cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear alert history");
                return StatusCode(500, new { error = "Failed to clear alert history", message = ex.Message });
            }
        }

        /// <summary>
        /// Forces an immediate monitoring check
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Status after check</returns>
        [HttpPost("check")]
        [ProducesResponseType(typeof(CacheMonitoringStatusDto), 200)]
        public async Task<IActionResult> ForceCheck(CancellationToken cancellationToken = default)
        {
            try
            {
                await _monitoringService.CheckNowAsync(cancellationToken);
                var status = await _monitoringService.GetStatusAsync(cancellationToken);
                
                var response = new CacheMonitoringStatusDto
                {
                    LastCheck = status.LastCheck,
                    IsHealthy = status.IsHealthy,
                    CurrentHitRate = status.CurrentHitRate,
                    CurrentMemoryUsagePercent = status.CurrentMemoryUsagePercent,
                    CurrentEvictionRate = status.CurrentEvictionRate,
                    CurrentResponseTimeMs = status.CurrentResponseTimeMs,
                    ActiveAlerts = status.ActiveAlerts,
                    Details = status.Details
                };

                _logger.LogInformation("Forced cache monitoring check completed");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to force monitoring check");
                return StatusCode(500, new { error = "Failed to execute monitoring check", message = ex.Message });
            }
        }

        /// <summary>
        /// Gets available alert definitions
        /// </summary>
        /// <returns>List of defined alert types and their configurations</returns>
        [HttpGet("alert-definitions")]
        [ProducesResponseType(typeof(List<AlertDefinitionDto>), 200)]
        public IActionResult GetAlertDefinitions()
        {
            try
            {
                var definitions = CacheAlertDefinitions.Alerts.Values.Select(d => new AlertDefinitionDto
                {
                    Type = d.Type.ToString(),
                    Name = d.Name,
                    DefaultSeverity = d.DefaultSeverity.ToString(),
                    Description = d.Description,
                    RecommendedActions = d.RecommendedActions,
                    NotificationEnabled = d.NotificationEnabled,
                    CooldownPeriodMinutes = (int)d.CooldownPeriod.TotalMinutes
                }).ToList();

                return Ok(definitions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get alert definitions");
                return StatusCode(500, new { error = "Failed to retrieve alert definitions", message = ex.Message });
            }
        }

        /// <summary>
        /// Gets cache health summary for monitoring dashboard
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Health summary with key metrics</returns>
        [HttpGet("health")]
        [ProducesResponseType(typeof(CacheHealthSummaryDto), 200)]
        public async Task<IActionResult> GetHealthSummary(CancellationToken cancellationToken = default)
        {
            try
            {
                var monitoringStatus = await _monitoringService.GetStatusAsync(cancellationToken);
                var statistics = await _cacheManagementService.GetStatisticsAsync(null, cancellationToken);
                var recentAlerts = _monitoringService.GetRecentAlerts(5);

                var response = new CacheHealthSummaryDto
                {
                    OverallHealth = monitoringStatus.IsHealthy ? "Healthy" : "Unhealthy",
                    HitRate = monitoringStatus.CurrentHitRate,
                    MemoryUsagePercent = monitoringStatus.CurrentMemoryUsagePercent,
                    ResponseTimeMs = monitoringStatus.CurrentResponseTimeMs,
                    EvictionRate = monitoringStatus.CurrentEvictionRate,
                    ActiveAlerts = monitoringStatus.ActiveAlerts,
                    TotalCacheSize = 0, // TODO: Calculate from memory usage
                    TotalEntries = 0, // TODO: Calculate from region statistics
                    LastCheck = monitoringStatus.LastCheck,
                    RecentAlerts = recentAlerts.Select(a => new CacheAlertDto
                    {
                        AlertType = a.AlertType,
                        Message = a.Message,
                        Severity = a.Severity,
                        Region = a.Region,
                        Timestamp = a.Timestamp
                    }).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get cache health summary");
                return StatusCode(500, new { error = "Failed to retrieve health summary", message = ex.Message });
            }
        }
    }

    #region DTOs

    /// <summary>
    /// Cache monitoring status DTO
    /// </summary>
    public class CacheMonitoringStatusDto
    {
                /// <summary>
        /// Timestamp of the last check (UTC).
        /// </summary>
        public DateTime LastCheck { get; set; }
                /// <summary>
        /// Indicates overall cache health status at the time of the check.
        /// </summary>
        public bool IsHealthy { get; set; }
                /// <summary>
        /// Current cache hit rate percentage.
        /// </summary>
        public double CurrentHitRate { get; set; }
                /// <summary>
        /// Current memory usage percentage.
        /// </summary>
        public double CurrentMemoryUsagePercent { get; set; }
                /// <summary>
        /// Current eviction rate percentage.
        /// </summary>
        public double CurrentEvictionRate { get; set; }
                /// <summary>
        /// Current average response time in milliseconds.
        /// </summary>
        public double CurrentResponseTimeMs { get; set; }
                /// <summary>
        /// Number of alerts currently active.
        /// </summary>
        public int ActiveAlerts { get; set; }
                /// <summary>
        /// Additional structured data providing context for the alert.
        /// </summary>
        public Dictionary<string, object> Details { get; set; } = new();
    }

    /// <summary>
    /// Cache monitoring alert thresholds DTO
    /// </summary>
    public class CacheAlertThresholdsDto
    {
                /// <summary>
        /// Minimum acceptable cache hit rate percentage before an alert is raised.
        /// </summary>
        public double MinHitRate { get; set; }
                /// <summary>
        /// Maximum allowed memory usage percentage before an alert is raised.
        /// </summary>
        public double MaxMemoryUsage { get; set; }
                /// <summary>
        /// Maximum allowed percentage of evictions over the sampling window.
        /// </summary>
        public double MaxEvictionRate { get; set; }
                /// <summary>
        /// Maximum average cache response time (in milliseconds) before an alert is raised.
        /// </summary>
        public double MaxResponseTimeMs { get; set; }
                /// <summary>
        /// Minimum number of requests required before evaluating hit-rate alert logic.
        /// </summary>
        public long MinRequestsForHitRateAlert { get; set; }
    }

    /// <summary>
    /// Update thresholds request
    /// </summary>
    public class UpdateThresholdsRequest
    {
                /// <summary>
        /// Updated minimum hit rate; <c>null</c> to keep current value.
        /// </summary>
        public double? MinHitRate { get; set; }
                /// <summary>
        /// Updated maximum memory usage; <c>null</c> to keep current value.
        /// </summary>
        public double? MaxMemoryUsage { get; set; }
                /// <summary>
        /// Updated maximum eviction rate; <c>null</c> to keep current value.
        /// </summary>
        public double? MaxEvictionRate { get; set; }
                /// <summary>
        /// Updated maximum response time in ms; <c>null</c> to keep current value.
        /// </summary>
        public double? MaxResponseTimeMs { get; set; }
                /// <summary>
        /// Updated minimum requests threshold; <c>null</c> to keep current value.
        /// </summary>
        public long? MinRequestsForHitRateAlert { get; set; }
    }

    /// <summary>
    /// Cache alert DTO
    /// </summary>
    public class CacheAlertDto
    {
                /// <summary>
        /// Machine-readable alert identifier (e.g., <c>cache_high_memory</c>).
        /// </summary>
        public string AlertType { get; set; } = string.Empty;
                /// <summary>
        /// Human-readable explanation of the alert.
        /// </summary>
        public string Message { get; set; } = string.Empty;
                /// <summary>
        /// Severity level of the alert (<c>info</c>, <c>warning</c>, <c>critical</c>).
        /// </summary>
        public string Severity { get; set; } = string.Empty;
                /// <summary>
        /// Optional cache region associated with the alert.
        /// </summary>
        public string? Region { get; set; }
                /// <summary>
        /// Additional structured data providing context for the alert.
        /// </summary>
        public Dictionary<string, object> Details { get; set; } = new();
                /// <summary>
        /// Time when the alert occurred (UTC).
        /// </summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Alert definition DTO
    /// </summary>
    public class AlertDefinitionDto
    {
                /// <summary>
        /// Unique identifier for the alert type (e.g., <c>cache_high_memory</c>).
        /// </summary>
        public string Type { get; set; } = string.Empty;
                /// <summary>
        /// Human-readable name for the alert.
        /// </summary>
        public string Name { get; set; } = string.Empty;
                /// <summary>
        /// The default severity level (e.g., <c>warning</c>, <c>critical</c>).
        /// </summary>
        public string DefaultSeverity { get; set; } = string.Empty;
                /// <summary>
        /// Detailed description of what the alert means.
        /// </summary>
        public string Description { get; set; } = string.Empty;
                /// <summary>
        /// Suggested remediation steps when the alert is triggered.
        /// </summary>
        public List<string> RecommendedActions { get; set; } = new();
                /// <summary>
        /// Indicates whether notifications for this alert are enabled.
        /// </summary>
        public bool NotificationEnabled { get; set; }
                /// <summary>
        /// Minimum number of minutes between successive notifications of the same alert.
        /// </summary>
        public int CooldownPeriodMinutes { get; set; }
    }

    /// <summary>
    /// Cache health summary DTO
    /// </summary>
    public class CacheHealthSummaryDto
    {
                /// <summary>
        /// Overall cache health status (e.g., <c>healthy</c>, <c>degraded</c>, <c>unhealthy</c>).
        /// </summary>
        public string OverallHealth { get; set; } = string.Empty;
                /// <summary>
        /// Fraction of cache look-ups that resulted in a hit (0-100).
        /// </summary>
        public double HitRate { get; set; }
                /// <summary>
        /// Percentage of allocated memory currently used by the cache.
        /// </summary>
        public double MemoryUsagePercent { get; set; }
                /// <summary>
        /// Average cache response time in milliseconds.
        /// </summary>
        public double ResponseTimeMs { get; set; }
                /// <summary>
        /// Percentage of entries evicted over the sampling period.
        /// </summary>
        public double EvictionRate { get; set; }
                /// <summary>
        /// Number of alerts currently active.
        /// </summary>
        public int ActiveAlerts { get; set; }
                /// <summary>
        /// Total size of cache in bytes.
        /// </summary>
        public long TotalCacheSize { get; set; }
                /// <summary>
        /// Total number of cache entries.
        /// </summary>
        public long TotalEntries { get; set; }
                /// <summary>
        /// Timestamp of the last check (UTC).
        /// </summary>
        public DateTime LastCheck { get; set; }
                /// <summary>
        /// List of recent cache alerts.
        /// </summary>
        public List<CacheAlertDto> RecentAlerts { get; set; } = new();
    }

    #endregion
}