using ConduitLLM.Admin.Interfaces;

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
    private readonly ILogger<SystemInfoController> _logger;

    /// <summary>
    /// Initializes a new instance of the SystemInfoController
    /// </summary>
    /// <param name="systemInfoService">The system info service</param>
    /// <param name="logger">The logger</param>
    public SystemInfoController(
        IAdminSystemInfoService systemInfoService,
        ILogger<SystemInfoController> logger)
    {
        _systemInfoService = systemInfoService ?? throw new ArgumentNullException(nameof(systemInfoService));
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
}
