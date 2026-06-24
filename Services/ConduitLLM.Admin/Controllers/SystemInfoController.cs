using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs.Monitoring;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Configuration.Messaging;
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
    private readonly IEventBus _eventBus;
    private readonly ILogger<SystemInfoController> _logger;
    private readonly IFunctionDiscoveryCacheService? _functionDiscoveryCacheService;

    /// <summary>
    /// Initializes a new instance of the SystemInfoController
    /// </summary>
    /// <param name="systemInfoService">The system info service</param>
    /// <param name="eventBus">event bus for events</param>
    /// <param name="logger">The logger</param>
    /// <param name="functionDiscoveryCacheService">Optional function discovery cache service</param>
    public SystemInfoController(
        IAdminSystemInfoService systemInfoService,
        IEventBus eventBus,
        ILogger<SystemInfoController> logger,
        IFunctionDiscoveryCacheService? functionDiscoveryCacheService = null)
    {
        _systemInfoService = systemInfoService ?? throw new ArgumentNullException(nameof(systemInfoService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _functionDiscoveryCacheService = functionDiscoveryCacheService;
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
    /// Invalidates all discovery cache entries by publishing an event to all Gateway API instances
    /// </summary>
    /// <returns>Success response with cache invalidation details</returns>
    [HttpPost("cache/invalidate-discovery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> InvalidateDiscoveryCache()
    {
        try
        {
            // Publish event to all Gateway API instances via MassTransit
            await _eventBus.PublishAsync(new DiscoveryCacheInvalidationRequested
            {
                Reason = "Manual invalidation via Admin API",
                RequestedBy = "Admin User",
                CorrelationId = Guid.NewGuid().ToString()
            });

            _logger.LogInformation("Published discovery cache invalidation event to all Gateway API instances");

            return Ok(new
            {
                message = "Discovery cache invalidation request published successfully",
                timestamp = DateTime.UtcNow,
                note = "Cache invalidation is being processed asynchronously across all Gateway API instances"
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

    /// <summary>
    /// Gets function discovery cache statistics
    /// </summary>
    /// <returns>Cache statistics including hit rate, entry count, and memory usage</returns>
    [HttpGet("cache/function-discovery/stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFunctionDiscoveryCacheStats()
    {
        try
        {
            if (_functionDiscoveryCacheService == null)
            {
                return NotFound(new
                {
                    message = "Function discovery cache service is not configured",
                    note = "The cache service must be registered in the DI container"
                });
            }

            var stats = await _functionDiscoveryCacheService.GetStatisticsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function discovery cache statistics");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An error occurred while retrieving cache statistics",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Invalidates all function discovery cache entries by publishing an event to all Gateway API instances
    /// </summary>
    /// <returns>Success response with cache invalidation details</returns>
    [HttpPost("cache/invalidate-function-discovery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> InvalidateFunctionDiscoveryCache()
    {
        try
        {
            // Publish event to all Gateway API instances via MassTransit
            await _eventBus.PublishAsync(new FunctionDiscoveryCacheInvalidationRequested
            {
                Reason = "Manual invalidation via Admin API",
                RequestedBy = "Admin User",
                CorrelationId = Guid.NewGuid().ToString()
            });

            _logger.LogInformation("Published function discovery cache invalidation event to all Gateway API instances");

            return Ok(new
            {
                message = "Function discovery cache invalidation request published successfully",
                timestamp = DateTime.UtcNow,
                note = "Cache invalidation is being processed asynchronously across all Gateway API instances"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing function discovery cache invalidation event");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An error occurred while requesting function discovery cache invalidation",
                error = ex.Message
            });
        }
    }
}
