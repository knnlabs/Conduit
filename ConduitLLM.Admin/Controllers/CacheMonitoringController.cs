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
using ConduitLLM.Configuration.DTOs.Monitoring;

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
        public IActionResult UpdateAlertThresholds([FromBody] UpdateThresholdsRequestDto request)
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
}
