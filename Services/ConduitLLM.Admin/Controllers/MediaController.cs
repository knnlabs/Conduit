using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Admin.Interfaces;
using Microsoft.AspNetCore.Authorization;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Administrative controller for media lifecycle management including
    /// statistics, search, cleanup operations, and cleanup service configuration.
    /// </summary>
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ServiceFilter(typeof(OperationLoggingFilter))]
    public class MediaController : AdminControllerBase
    {
        private readonly IAdminMediaService _mediaService;
        private readonly IMediaCleanupStatusService _cleanupStatusService;

        /// <summary>
        /// Initializes a new instance of the MediaController class.
        /// </summary>
        /// <param name="mediaService">The admin media service.</param>
        /// <param name="cleanupStatusService">The media cleanup status service.</param>
        /// <param name="logger">The logger instance.</param>
        public MediaController(
            IAdminMediaService mediaService,
            IMediaCleanupStatusService cleanupStatusService,
            ILogger<MediaController> logger)
            : base(logger)
        {
            _mediaService = mediaService ?? throw new ArgumentNullException(nameof(mediaService));
            _cleanupStatusService = cleanupStatusService ?? throw new ArgumentNullException(nameof(cleanupStatusService));
        }

        /// <summary>
        /// Gets overall storage statistics across all virtual keys.
        /// </summary>
        /// <param name="virtualKeyGroupId">Optional filter by virtual key group ID</param>
        /// <returns>Overall storage statistics.</returns>
        [HttpGet("stats")]
        [ProducesResponseType(typeof(OverallMediaStorageStats), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOverallStats([FromQuery] int? virtualKeyGroupId = null)
        {
            var stats = await _mediaService.GetOverallStorageStatsAsync(virtualKeyGroupId);
            return Ok(stats);
        }

        /// <summary>
        /// Gets storage statistics for a specific virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key.</param>
        /// <returns>Storage statistics for the virtual key.</returns>
        [HttpGet("stats/virtual-key/{virtualKeyId}")]
        [ProducesResponseType(typeof(MediaStorageStats), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStatsByVirtualKey(int virtualKeyId)
        {
            var stats = await _mediaService.GetStorageStatsByVirtualKeyAsync(virtualKeyId);
            return Ok(stats);
        }

        /// <summary>
        /// Gets storage statistics grouped by provider.
        /// </summary>
        /// <returns>Dictionary of provider names to storage size.</returns>
        [HttpGet("stats/by-provider")]
        [ProducesResponseType(typeof(Dictionary<string, long>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStatsByProvider()
        {
            var stats = await _mediaService.GetStorageStatsByProviderAsync();
            return Ok(stats);
        }

        /// <summary>
        /// Gets storage statistics grouped by media type.
        /// </summary>
        /// <returns>Dictionary of media types to storage size.</returns>
        [HttpGet("stats/by-type")]
        [ProducesResponseType(typeof(Dictionary<string, long>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStatsByMediaType()
        {
            var stats = await _mediaService.GetStorageStatsByMediaTypeAsync();
            return Ok(stats);
        }

        /// <summary>
        /// Gets media records for a specific virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key.</param>
        /// <returns>List of media records.</returns>
        [HttpGet("virtual-key/{virtualKeyId}")]
        [ProducesResponseType(typeof(List<MediaRecord>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMediaByVirtualKey(int virtualKeyId)
        {
            var media = await _mediaService.GetMediaByVirtualKeyAsync(virtualKeyId);
            return Ok(media);
        }

        /// <summary>
        /// Searches for media records by storage key pattern.
        /// </summary>
        /// <param name="pattern">The pattern to search for in storage keys.</param>
        /// <returns>List of matching media records.</returns>
        [HttpGet("search")]
        [ProducesResponseType(typeof(List<MediaRecord>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SearchMedia([FromQuery] string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return BadRequest(new ErrorResponseDto("Search pattern is required"));
            }

            var media = await _mediaService.SearchMediaByStorageKeyAsync(pattern);
            return Ok(media);
        }

        /// <summary>
        /// Deletes a specific media record and its associated file.
        /// </summary>
        /// <param name="mediaId">The ID of the media record to delete.</param>
        /// <returns>Success status.</returns>
        [HttpDelete("{mediaId}")]
        [ProducesResponseType(typeof(MediaDeletionResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteMedia(Guid mediaId)
        {
            if (!await _mediaService.DeleteMediaAsync(mediaId))
                throw new KeyNotFoundException();
            LogAdminAudit("Deleted", "Media", mediaId);
            return Ok(new MediaDeletionResponseDto { Message = "Media deleted successfully" });
        }

        /// <summary>
        /// Manually triggers cleanup of expired media files.
        /// </summary>
        /// <returns>Number of files cleaned up.</returns>
        [HttpPost("cleanup/expired")]
        [ProducesResponseType(typeof(MediaCleanupResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> CleanupExpiredMedia()
        {
            var count = await _mediaService.CleanupExpiredMediaAsync();
            LogAdminAudit("CleanedUpExpired", "Media", detail: $"DeletedCount: {count}");
            return Ok(new MediaCleanupResponseDto
            {
                Message = $"Cleaned up {count} expired media files",
                DeletedCount = count
            });
        }

        /// <summary>
        /// Manually triggers cleanup of orphaned media files.
        /// </summary>
        /// <returns>Number of files cleaned up.</returns>
        [HttpPost("cleanup/orphaned")]
        [ProducesResponseType(typeof(MediaCleanupResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> CleanupOrphanedMedia()
        {
            var count = await _mediaService.CleanupOrphanedMediaAsync();
            LogAdminAudit("CleanedUpOrphaned", "Media", detail: $"DeletedCount: {count}");
            return Ok(new MediaCleanupResponseDto
            {
                Message = $"Cleaned up {count} orphaned media files",
                DeletedCount = count
            });
        }

        /// <summary>
        /// Manually triggers pruning of old media files.
        /// </summary>
        /// <param name="request">The pruning request with days to keep.</param>
        /// <returns>Number of files pruned.</returns>
        [HttpPost("cleanup/prune")]
        [ProducesResponseType(typeof(MediaCleanupResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> PruneOldMedia([FromBody] PruneMediaRequest request)
        {
            if (request?.DaysToKeep == null || request.DaysToKeep <= 0)
            {
                return BadRequest(new ErrorResponseDto("DaysToKeep must be a positive number"));
            }

            var count = await _mediaService.PruneOldMediaAsync(request.DaysToKeep.Value);
            LogAdminAudit("Pruned", "Media", detail: $"DaysToKeep: {request.DaysToKeep}, DeletedCount: {count}");
            return Ok(new MediaCleanupResponseDto
            {
                Message = $"Pruned {count} media files older than {request.DaysToKeep} days",
                DeletedCount = count
            });
        }

        // ─── Cleanup Service Configuration ──────────────────────────────
        // Routes use absolute paths to maintain backward compatibility with api/admin/media-cleanup

        /// <summary>
        /// Gets the current status of the media cleanup service.
        /// </summary>
        [HttpGet("/api/admin/media-cleanup/status")]
        [ProducesResponseType(typeof(MediaCleanupStatusDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCleanupStatus()
        {
            var status = await _cleanupStatusService.GetStatusAsync();
            return Ok(status);
        }

        /// <summary>
        /// Gets whether the media cleanup service is currently enabled.
        /// </summary>
        [HttpGet("/api/admin/media-cleanup/enabled")]
        [ProducesResponseType(typeof(MediaCleanupEnabledDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCleanupEnabled()
        {
            var isEnabled = await _cleanupStatusService.IsEnabledAsync();
            return Ok(new MediaCleanupEnabledDto { Enabled = isEnabled });
        }

        /// <summary>
        /// Enables or disables the media cleanup service at runtime.
        /// This setting persists across restarts via GlobalSettings.
        /// </summary>
        [HttpPost("/api/admin/media-cleanup/enabled")]
        [ProducesResponseType(typeof(MediaCleanupEnabledChangedDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> SetCleanupEnabled([FromBody] UpdateMediaCleanupEnabledRequest request)
        {
            await _cleanupStatusService.SetEnabledAsync(request.Enabled);
            LogAdminAudit("SetEnabled", "MediaCleanupService", detail: $"Enabled: {request.Enabled}");
            return Ok(new MediaCleanupEnabledChangedDto
            {
                Enabled = request.Enabled,
                Message = request.Enabled
                    ? "Media cleanup service has been enabled"
                    : "Media cleanup service has been disabled"
            });
        }

        /// <summary>
        /// Gets the simple retention override setting.
        /// </summary>
        [HttpGet("/api/admin/media-cleanup/simple-retention")]
        [ProducesResponseType(typeof(SimpleRetentionResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSimpleRetention()
        {
            var days = await _cleanupStatusService.GetSimpleRetentionOverrideAsync();
            return Ok(new SimpleRetentionResponse
            {
                RetentionDays = days,
                IsOverrideActive = days.HasValue
            });
        }

        /// <summary>
        /// Sets or clears the simple retention override.
        /// Pass null for RetentionDays to clear the override and use policy-based retention.
        /// </summary>
        [HttpPost("/api/admin/media-cleanup/simple-retention")]
        [ProducesResponseType(typeof(SimpleRetentionResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> SetSimpleRetention([FromBody] UpdateSimpleRetentionRequest request)
        {
            await _cleanupStatusService.SetSimpleRetentionOverrideAsync(request.RetentionDays);
            var message = request.RetentionDays.HasValue
                ? $"Simple retention override set to {request.RetentionDays} days - all media will be deleted after this period"
                : "Simple retention override cleared - using policy-based retention";
            LogAdminAudit("SetSimpleRetention", "MediaCleanupService",
                detail: $"RetentionDays: {request.RetentionDays?.ToString() ?? "cleared"}");
            return Ok(new SimpleRetentionResponse
            {
                RetentionDays = request.RetentionDays,
                IsOverrideActive = request.RetentionDays.HasValue,
                Message = message
            });
        }
    }

    /// <summary>
    /// Request model for pruning old media.
    /// </summary>
    public class PruneMediaRequest
    {
        /// <summary>
        /// Gets or sets the number of days to keep media files.
        /// </summary>
        public int? DaysToKeep { get; set; }
    }
}
