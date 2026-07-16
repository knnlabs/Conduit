using ConduitLLM.Admin.Filters;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs.Monitoring;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Controller for system information
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "MasterKeyPolicy")]
[ServiceFilter(typeof(OperationLoggingFilter))]
public class SystemInfoController : AdminControllerBase
{
    private readonly IAdminSystemInfoService _systemInfoService;
    private readonly IEventBus _eventBus;
    private readonly IFunctionDiscoveryCacheService? _functionDiscoveryCacheService;

    /// <summary>
    /// Initializes a new instance of the SystemInfoController
    /// </summary>
    /// <param name="systemInfoService">The system info service</param>
    /// <param name="eventBus">Event bus for publishing domain events</param>
    /// <param name="logger">The logger</param>
    /// <param name="functionDiscoveryCacheService">Optional function discovery cache service</param>
    public SystemInfoController(
        IAdminSystemInfoService systemInfoService,
        IEventBus eventBus,
        ILogger<SystemInfoController> logger,
        IFunctionDiscoveryCacheService? functionDiscoveryCacheService = null)
        : base(eventBus, logger)
    {
        _systemInfoService = systemInfoService ?? throw new ArgumentNullException(nameof(systemInfoService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _functionDiscoveryCacheService = functionDiscoveryCacheService;
    }

    /// <summary>
    /// Gets system information
    /// </summary>
    /// <returns>System information details</returns>
    [HttpGet("info")]
    [ProducesResponseType(typeof(SystemInfoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSystemInfo()
    {
        var result = await _systemInfoService.GetSystemInfoAsync();
        return Ok(result);
    }

    /// <summary>
    /// Gets health status
    /// </summary>
    /// <returns>Health status information</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHealthStatus()
    {
        var result = await _systemInfoService.GetHealthStatusAsync();
        return Ok(result);
    }

    /// <summary>
    /// Invalidates all discovery cache entries by publishing an event to all Gateway API instances
    /// </summary>
    /// <returns>Success response with cache invalidation details</returns>
    [HttpPost("cache/invalidate-discovery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> InvalidateDiscoveryCache()
    {
        // Publish event to all Gateway API instances via the event bus
        await _eventBus.PublishAsync(new DiscoveryCacheInvalidationRequested
        {
            Reason = "Manual invalidation via Admin API",
            RequestedBy = "Admin User",
            CorrelationId = Guid.NewGuid().ToString()
        });

        LogAdminAudit("Invalidated", "DiscoveryCache");

        return Ok(new
        {
            message = "Discovery cache invalidation request published successfully",
            timestamp = DateTime.UtcNow,
            note = "Cache invalidation is being processed asynchronously across all Gateway API instances"
        });
    }

    /// <summary>
    /// Gets function discovery cache statistics
    /// </summary>
    /// <returns>Cache statistics including hit rate, entry count, and memory usage</returns>
    [HttpGet("cache/function-discovery/stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFunctionDiscoveryCacheStats()
    {
        if (_functionDiscoveryCacheService == null)
        {
            return NotFound(new
            {
                message = "Function discovery cache service is not configured",
                note = "The cache service must be registered in the DI container"
            });
        }

        var result = await _functionDiscoveryCacheService.GetStatisticsAsync();
        return Ok(result);
    }

    /// <summary>
    /// Invalidates all function discovery cache entries by publishing an event to all Gateway API instances
    /// </summary>
    /// <returns>Success response with cache invalidation details</returns>
    [HttpPost("cache/invalidate-function-discovery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> InvalidateFunctionDiscoveryCache()
    {
        // Publish event to all Gateway API instances via the event bus
        await _eventBus.PublishAsync(new FunctionDiscoveryCacheInvalidationRequested
        {
            Reason = "Manual invalidation via Admin API",
            RequestedBy = "Admin User",
            CorrelationId = Guid.NewGuid().ToString()
        });

        LogAdminAudit("Invalidated", "FunctionDiscoveryCache");

        return Ok(new
        {
            message = "Function discovery cache invalidation request published successfully",
            timestamp = DateTime.UtcNow,
            note = "Cache invalidation is being processed asynchronously across all Gateway API instances"
        });
    }
}
