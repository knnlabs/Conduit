using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs.Monitoring;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
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
public class SystemInfoController : AdminControllerBase
{
    private readonly IAdminSystemInfoService _systemInfoService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IFunctionDiscoveryCacheService? _functionDiscoveryCacheService;

    /// <summary>
    /// Initializes a new instance of the SystemInfoController
    /// </summary>
    /// <param name="systemInfoService">The system info service</param>
    /// <param name="publishEndpoint">MassTransit publish endpoint for events</param>
    /// <param name="logger">The logger</param>
    /// <param name="functionDiscoveryCacheService">Optional function discovery cache service</param>
    public SystemInfoController(
        IAdminSystemInfoService systemInfoService,
        IPublishEndpoint publishEndpoint,
        ILogger<SystemInfoController> logger,
        IFunctionDiscoveryCacheService? functionDiscoveryCacheService = null)
        : base(publishEndpoint, logger)
    {
        _systemInfoService = systemInfoService ?? throw new ArgumentNullException(nameof(systemInfoService));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _functionDiscoveryCacheService = functionDiscoveryCacheService;
    }

    /// <summary>
    /// Gets system information
    /// </summary>
    /// <returns>System information details</returns>
    [HttpGet("info")]
    [ProducesResponseType(typeof(SystemInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetSystemInfo()
    {
        return ExecuteAsync(
            () => _systemInfoService.GetSystemInfoAsync(),
            result => Ok(result),
            "GetSystemInfo");
    }

    /// <summary>
    /// Gets health status
    /// </summary>
    /// <returns>Health status information</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetHealthStatus()
    {
        return ExecuteAsync(
            () => _systemInfoService.GetHealthStatusAsync(),
            result => Ok(result),
            "GetHealthStatus");
    }

    /// <summary>
    /// Invalidates all discovery cache entries by publishing an event to all Gateway API instances
    /// </summary>
    /// <returns>Success response with cache invalidation details</returns>
    [HttpPost("cache/invalidate-discovery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> InvalidateDiscoveryCache()
    {
        return ExecuteAsync(
            async () =>
            {
                // Publish event to all Gateway API instances via MassTransit
                await _publishEndpoint.Publish(new DiscoveryCacheInvalidationRequested
                {
                    Reason = "Manual invalidation via Admin API",
                    RequestedBy = "Admin User",
                    CorrelationId = Guid.NewGuid().ToString()
                });

                LogAdminAudit("Invalidated", "DiscoveryCache");

                return new
                {
                    message = "Discovery cache invalidation request published successfully",
                    timestamp = DateTime.UtcNow,
                    note = "Cache invalidation is being processed asynchronously across all Gateway API instances"
                };
            },
            result => Ok(result),
            "InvalidateDiscoveryCache");
    }

    /// <summary>
    /// Gets function discovery cache statistics
    /// </summary>
    /// <returns>Cache statistics including hit rate, entry count, and memory usage</returns>
    [HttpGet("cache/function-discovery/stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetFunctionDiscoveryCacheStats()
    {
        if (_functionDiscoveryCacheService == null)
        {
            return Task.FromResult<IActionResult>(NotFound(new
            {
                message = "Function discovery cache service is not configured",
                note = "The cache service must be registered in the DI container"
            }));
        }

        return ExecuteAsync(
            () => _functionDiscoveryCacheService.GetStatisticsAsync(),
            result => Ok(result),
            "GetFunctionDiscoveryCacheStats");
    }

    /// <summary>
    /// Invalidates all function discovery cache entries by publishing an event to all Gateway API instances
    /// </summary>
    /// <returns>Success response with cache invalidation details</returns>
    [HttpPost("cache/invalidate-function-discovery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> InvalidateFunctionDiscoveryCache()
    {
        return ExecuteAsync(
            async () =>
            {
                // Publish event to all Gateway API instances via MassTransit
                await _publishEndpoint.Publish(new FunctionDiscoveryCacheInvalidationRequested
                {
                    Reason = "Manual invalidation via Admin API",
                    RequestedBy = "Admin User",
                    CorrelationId = Guid.NewGuid().ToString()
                });

                LogAdminAudit("Invalidated", "FunctionDiscoveryCache");

                return new
                {
                    message = "Function discovery cache invalidation request published successfully",
                    timestamp = DateTime.UtcNow,
                    note = "Cache invalidation is being processed asynchronously across all Gateway API instances"
                };
            },
            result => Ok(result),
            "InvalidateFunctionDiscoveryCache");
    }
}
