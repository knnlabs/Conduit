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
    public class MediaCleanupController : AdminControllerBase
    {
        private readonly IMediaCleanupStatusService _statusService;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaCleanupController"/> class.
        /// </summary>
        public MediaCleanupController(
            IMediaCleanupStatusService statusService,
            ILogger<MediaCleanupController> logger)
            : base(logger)
        {
            _statusService = statusService;
        }

        /// <summary>
        /// Gets the current status of the media cleanup service.
        /// </summary>
        /// <returns>Status information including last run, budget usage, and retention policies.</returns>
        [HttpGet("status")]
        [ProducesResponseType(typeof(MediaCleanupStatusDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> GetStatus()
        {
            return ExecuteAsync(
                () => _statusService.GetStatusAsync(),
                Ok,
                "GetStatus");
        }

        /// <summary>
        /// Gets whether the media cleanup service is currently enabled.
        /// </summary>
        /// <returns>The enabled state.</returns>
        [HttpGet("enabled")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> GetEnabled()
        {
            return ExecuteAsync(
                async () =>
                {
                    var isEnabled = await _statusService.IsEnabledAsync();
                    return new { enabled = isEnabled };
                },
                Ok,
                "GetEnabled");
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
        public Task<IActionResult> SetEnabled([FromBody] UpdateMediaCleanupEnabledRequest request)
        {
            return ExecuteAsync(
                async () =>
                {
                    await _statusService.SetEnabledAsync(request.Enabled);

                    LogAdminAudit("SetEnabled", "MediaCleanupService", detail: $"Enabled: {request.Enabled}");

                    return new
                    {
                        enabled = request.Enabled,
                        message = request.Enabled
                            ? "Media cleanup service has been enabled"
                            : "Media cleanup service has been disabled"
                    };
                },
                Ok,
                "SetEnabled");
        }

        /// <summary>
        /// Gets the simple retention override setting.
        /// When active, all media uses this retention period regardless of account balance.
        /// </summary>
        /// <returns>The current simple retention override, or null if using policy-based retention.</returns>
        [HttpGet("simple-retention")]
        [ProducesResponseType(typeof(SimpleRetentionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> GetSimpleRetention()
        {
            return ExecuteAsync(
                async () =>
                {
                    var days = await _statusService.GetSimpleRetentionOverrideAsync();
                    return new SimpleRetentionResponse
                    {
                        RetentionDays = days,
                        IsOverrideActive = days.HasValue
                    };
                },
                Ok,
                "GetSimpleRetention");
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
        public Task<IActionResult> SetSimpleRetention([FromBody] UpdateSimpleRetentionRequest request)
        {
            return ExecuteAsync(
                async () =>
                {
                    await _statusService.SetSimpleRetentionOverrideAsync(request.RetentionDays);

                    var message = request.RetentionDays.HasValue
                        ? $"Simple retention override set to {request.RetentionDays} days - all media will be deleted after this period"
                        : "Simple retention override cleared - using policy-based retention";

                    LogAdminAudit("SetSimpleRetention", "MediaCleanupService",
                        detail: $"RetentionDays: {request.RetentionDays?.ToString() ?? "cleared"}");

                    return new SimpleRetentionResponse
                    {
                        RetentionDays = request.RetentionDays,
                        IsOverrideActive = request.RetentionDays.HasValue,
                        Message = message
                    };
                },
                Ok,
                "SetSimpleRetention");
        }
    }
}
