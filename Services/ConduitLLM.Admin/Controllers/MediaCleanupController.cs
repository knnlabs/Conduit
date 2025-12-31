using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Interfaces;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for media cleanup service status and management.
    /// Provides operational visibility into cleanup runs, budget usage, and configuration.
    /// </summary>
    [ApiController]
    [Route("api/admin/media-cleanup")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class MediaCleanupController : ControllerBase
    {
        private readonly IMediaCleanupStatusService _statusService;
        private readonly ILogger<MediaCleanupController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaCleanupController"/> class.
        /// </summary>
        public MediaCleanupController(
            IMediaCleanupStatusService statusService,
            ILogger<MediaCleanupController> logger)
        {
            _statusService = statusService;
            _logger = logger;
        }

        /// <summary>
        /// Gets the current status of the media cleanup service.
        /// </summary>
        /// <returns>Status information including last run, budget usage, and retention policies.</returns>
        [HttpGet("status")]
        [ProducesResponseType(typeof(MediaCleanupStatusDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var status = await _statusService.GetStatusAsync();
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting media cleanup status");
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while getting cleanup status" });
            }
        }

        /// <summary>
        /// Gets whether the media cleanup service is currently enabled.
        /// </summary>
        /// <returns>The enabled state.</returns>
        [HttpGet("enabled")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetEnabled()
        {
            try
            {
                var isEnabled = await _statusService.IsEnabledAsync();
                return Ok(new { enabled = isEnabled });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting media cleanup enabled state");
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while getting enabled state" });
            }
        }

        /// <summary>
        /// Enables or disables the media cleanup service at runtime.
        /// This setting persists across restarts via GlobalSettings.
        /// </summary>
        /// <param name="request">The enabled state to set.</param>
        /// <returns>The new enabled state.</returns>
        [HttpPost("enabled")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SetEnabled([FromBody] UpdateMediaCleanupEnabledRequest request)
        {
            try
            {
                await _statusService.SetEnabledAsync(request.Enabled);

                _logger.LogInformation(
                    "Media cleanup service enabled state changed to {Enabled} by admin request",
                    request.Enabled);

                return Ok(new
                {
                    enabled = request.Enabled,
                    message = request.Enabled
                        ? "Media cleanup service has been enabled"
                        : "Media cleanup service has been disabled"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting media cleanup enabled state");
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while setting enabled state" });
            }
        }

        /// <summary>
        /// Gets the simple retention override setting.
        /// When active, all media uses this retention period regardless of account balance.
        /// </summary>
        /// <returns>The current simple retention override, or null if using policy-based retention.</returns>
        [HttpGet("simple-retention")]
        [ProducesResponseType(typeof(SimpleRetentionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSimpleRetention()
        {
            try
            {
                var days = await _statusService.GetSimpleRetentionOverrideAsync();
                return Ok(new SimpleRetentionResponse
                {
                    RetentionDays = days,
                    IsOverrideActive = days.HasValue
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting simple retention override");
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while getting simple retention override" });
            }
        }

        /// <summary>
        /// Sets or clears the simple retention override.
        /// When set, all media is deleted after the specified number of days regardless of account balance.
        /// Pass null for RetentionDays to clear the override and use policy-based retention.
        /// </summary>
        /// <param name="request">The retention days to set (1-365), or null to clear.</param>
        /// <returns>The new simple retention override state.</returns>
        [HttpPost("simple-retention")]
        [ProducesResponseType(typeof(SimpleRetentionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SetSimpleRetention([FromBody] UpdateSimpleRetentionRequest request)
        {
            try
            {
                await _statusService.SetSimpleRetentionOverrideAsync(request.RetentionDays);

                var message = request.RetentionDays.HasValue
                    ? $"Simple retention override set to {request.RetentionDays} days - all media will be deleted after this period"
                    : "Simple retention override cleared - using policy-based retention";

                _logger.LogInformation(
                    "Simple retention override changed to {Days} by admin request",
                    request.RetentionDays?.ToString() ?? "null (cleared)");

                return Ok(new SimpleRetentionResponse
                {
                    RetentionDays = request.RetentionDays,
                    IsOverrideActive = request.RetentionDays.HasValue,
                    Message = message
                });
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting simple retention override");
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while setting simple retention override" });
            }
        }
    }
}
