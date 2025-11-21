using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs.Monitoring;
using ConduitLLM.Core.Events;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Controller for system information
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "MasterKeyPolicy")]
public class SystemInfoController : ControllerBase
{
    private readonly IAdminSystemInfoService _systemInfoService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<SystemInfoController> _logger;

    /// <summary>
    /// Initializes a new instance of the SystemInfoController
    /// </summary>
    /// <param name="systemInfoService">The system info service</param>
    /// <param name="publishEndpoint">MassTransit publish endpoint for events</param>
    /// <param name="logger">The logger</param>
    public SystemInfoController(
        IAdminSystemInfoService systemInfoService,
        IPublishEndpoint publishEndpoint,
        ILogger<SystemInfoController> logger)
    {
        _systemInfoService = systemInfoService ?? throw new ArgumentNullException(nameof(systemInfoService));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets system information
    /// </summary>
    /// <returns>System information details</returns>
    [HttpGet("info")]
    [ProducesResponseType(typeof(SystemInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSystemInfo()
    {
        try
        {
            var systemInfo = await _systemInfoService.GetSystemInfoAsync();
            return Ok(systemInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system information");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets health status
    /// </summary>
    /// <returns>Health status information</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetHealthStatus()
    {
        try
        {
            var healthStatus = await _systemInfoService.GetHealthStatusAsync();
            return Ok(healthStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health status");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Invalidates all discovery cache entries by publishing an event to all Core API instances
    /// </summary>
    /// <returns>Success response with cache invalidation details</returns>
    [HttpPost("cache/invalidate-discovery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> InvalidateDiscoveryCache()
    {
        try
        {
            // Publish event to all Core API instances via MassTransit
            await _publishEndpoint.Publish(new DiscoveryCacheInvalidationRequested
            {
                Reason = "Manual invalidation via Admin API",
                RequestedBy = "Admin User",
                CorrelationId = Guid.NewGuid().ToString()
            });

            _logger.LogInformation("Published discovery cache invalidation event to all Core API instances");

            return Ok(new
            {
                message = "Discovery cache invalidation request published successfully",
                timestamp = DateTime.UtcNow,
                note = "Cache invalidation is being processed asynchronously across all Core API instances"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing discovery cache invalidation event");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An error occurred while requesting discovery cache invalidation",
                error = ex.Message
            });
        }
    }
}
