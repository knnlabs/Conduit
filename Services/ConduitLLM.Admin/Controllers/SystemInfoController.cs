using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs.Monitoring;
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
public class SystemInfoController : ControllerBase
{
    private readonly IAdminSystemInfoService _systemInfoService;
    private readonly IDiscoveryCacheService? _discoveryCacheService;
    private readonly ILogger<SystemInfoController> _logger;

    /// <summary>
    /// Initializes a new instance of the SystemInfoController
    /// </summary>
    /// <param name="systemInfoService">The system info service</param>
    /// <param name="discoveryCacheService">The discovery cache service</param>
    /// <param name="logger">The logger</param>
    public SystemInfoController(
        IAdminSystemInfoService systemInfoService,
        ILogger<SystemInfoController> logger,
        IDiscoveryCacheService? discoveryCacheService = null)
    {
        _systemInfoService = systemInfoService ?? throw new ArgumentNullException(nameof(systemInfoService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _discoveryCacheService = discoveryCacheService;
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
    /// Invalidates all discovery cache entries
    /// </summary>
    /// <returns>Success response with cache invalidation details</returns>
    [HttpPost("cache/invalidate-discovery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> InvalidateDiscoveryCache()
    {
        try
        {
            if (_discoveryCacheService == null)
            {
                _logger.LogWarning("Discovery cache service is not available");
                return StatusCode(StatusCodes.Status501NotImplemented, new 
                { 
                    message = "Discovery cache service is not configured",
                    hint = "Ensure IDiscoveryCacheService is registered in dependency injection"
                });
            }

            await _discoveryCacheService.InvalidateAllDiscoveryAsync();
            
            _logger.LogInformation("Discovery cache invalidated by admin user");
            
            return Ok(new 
            { 
                message = "Discovery cache invalidated successfully",
                timestamp = DateTime.UtcNow,
                note = "It may take a moment for all instances to clear their caches"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating discovery cache");
            return StatusCode(StatusCodes.Status500InternalServerError, new 
            { 
                message = "An error occurred while invalidating the discovery cache",
                error = ex.Message 
            });
        }
    }
}
