using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Interfaces;
using Microsoft.AspNetCore.Authorization;
using ConduitLLM.Configuration.DTOs;
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
        public Task<IActionResult> GetOverallStats([FromQuery] int? virtualKeyGroupId = null)
        {
            return ExecuteAsync(
                () => _mediaService.GetOverallStorageStatsAsync(virtualKeyGroupId),
                Ok,
                "GetOverallStats",
                new { VirtualKeyGroupId = virtualKeyGroupId });
        }

        /// <summary>
        /// Gets storage statistics for a specific virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key.</param>
        /// <returns>Storage statistics for the virtual key.</returns>
        [HttpGet("stats/virtual-key/{virtualKeyId}")]
        public Task<IActionResult> GetStatsByVirtualKey(int virtualKeyId)
        {
            return ExecuteAsync(
                () => _mediaService.GetStorageStatsByVirtualKeyAsync(virtualKeyId),
                Ok,
                "GetStatsByVirtualKey",
                new { VirtualKeyId = virtualKeyId });
        }

        /// <summary>
        /// Gets storage statistics grouped by provider.
        /// </summary>
        /// <returns>Dictionary of provider names to storage size.</returns>
        [HttpGet("stats/by-provider")]
        public Task<IActionResult> GetStatsByProvider()
        {
            return ExecuteAsync(
                () => _mediaService.GetStorageStatsByProviderAsync(),
                Ok,
                "GetStatsByProvider");
        }

        /// <summary>
        /// Gets storage statistics grouped by media type.
        /// </summary>
        /// <returns>Dictionary of media types to storage size.</returns>
        [HttpGet("stats/by-type")]
        public Task<IActionResult> GetStatsByMediaType()
        {
            return ExecuteAsync(
                () => _mediaService.GetStorageStatsByMediaTypeAsync(),
                Ok,
                "GetStatsByMediaType");
        }

        /// <summary>
        /// Gets media records for a specific virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key.</param>
        /// <returns>List of media records.</returns>
        [HttpGet("virtual-key/{virtualKeyId}")]
        public Task<IActionResult> GetMediaByVirtualKey(int virtualKeyId)
        {
            return ExecuteAsync(
                () => _mediaService.GetMediaByVirtualKeyAsync(virtualKeyId),
                Ok,
                "GetMediaByVirtualKey",
                new { VirtualKeyId = virtualKeyId });
        }

        /// <summary>
        /// Searches for media records by storage key pattern.
        /// </summary>
        /// <param name="pattern">The pattern to search for in storage keys.</param>
        /// <returns>List of matching media records.</returns>
        [HttpGet("search")]
        public Task<IActionResult> SearchMedia([FromQuery] string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return Task.FromResult<IActionResult>(BadRequest(new ErrorResponseDto("Search pattern is required")));
            }

            return ExecuteAsync(
                () => _mediaService.SearchMediaByStorageKeyAsync(pattern),
                Ok,
                "SearchMedia",
                new { Pattern = pattern });
        }

        /// <summary>
        /// Deletes a specific media record and its associated file.
        /// </summary>
        /// <param name="mediaId">The ID of the media record to delete.</param>
        /// <returns>Success status.</returns>
        [HttpDelete("{mediaId}")]
        public Task<IActionResult> DeleteMedia(Guid mediaId)
        {
            return ExecuteAsync(
                async () =>
                {
                    if (!await _mediaService.DeleteMediaAsync(mediaId))
                        throw new KeyNotFoundException();
                    LogAdminAudit("Deleted", "Media", mediaId);
                },
                Ok(new { message = "Media deleted successfully" }),
                "DeleteMedia",
                new { MediaId = mediaId });
        }

        /// <summary>
        /// Manually triggers cleanup of expired media files.
        /// </summary>
        /// <returns>Number of files cleaned up.</returns>
        [HttpPost("cleanup/expired")]
        public Task<IActionResult> CleanupExpiredMedia()
        {
            return ExecuteAsync(
                async () =>
                {
                    var count = await _mediaService.CleanupExpiredMediaAsync();
                    LogAdminAudit("CleanedUpExpired", "Media", detail: $"DeletedCount: {count}");
                    return (object)new
                    {
                        message = $"Cleaned up {count} expired media files",
                        deletedCount = count
                    };
                },
                Ok,
                "CleanupExpiredMedia");
        }

        /// <summary>
        /// Manually triggers cleanup of orphaned media files.
        /// </summary>
        /// <returns>Number of files cleaned up.</returns>
        [HttpPost("cleanup/orphaned")]
        public Task<IActionResult> CleanupOrphanedMedia()
        {
            return ExecuteAsync(
                async () =>
                {
                    var count = await _mediaService.CleanupOrphanedMediaAsync();
                    LogAdminAudit("CleanedUpOrphaned", "Media", detail: $"DeletedCount: {count}");
                    return (object)new
                    {
                        message = $"Cleaned up {count} orphaned media files",
                        deletedCount = count
                    };
                },
                Ok,
                "CleanupOrphanedMedia");
        }

        /// <summary>
        /// Manually triggers pruning of old media files.
        /// </summary>
        /// <param name="request">The pruning request with days to keep.</param>
        /// <returns>Number of files pruned.</returns>
        [HttpPost("cleanup/prune")]
        public Task<IActionResult> PruneOldMedia([FromBody] PruneMediaRequest request)
        {
            if (request?.DaysToKeep == null || request.DaysToKeep <= 0)
            {
                return Task.FromResult<IActionResult>(BadRequest(new ErrorResponseDto("DaysToKeep must be a positive number")));
            }

            return ExecuteAsync(
                async () =>
                {
                    var count = await _mediaService.PruneOldMediaAsync(request.DaysToKeep.Value);
                    LogAdminAudit("Pruned", "Media", detail: $"DaysToKeep: {request.DaysToKeep}, DeletedCount: {count}");
                    return (object)new
                    {
                        message = $"Pruned {count} media files older than {request.DaysToKeep} days",
                        deletedCount = count
                    };
                },
                Ok,
                "PruneOldMedia",
                new { DaysToKeep = request?.DaysToKeep });
        }

        // ─── Cleanup Service Configuration ──────────────────────────────
        // Routes use absolute paths to maintain backward compatibility with api/admin/media-cleanup

        /// <summary>
        /// Gets the current status of the media cleanup service.
        /// </summary>
        [HttpGet("/api/admin/media-cleanup/status")]
        [ProducesResponseType(typeof(MediaCleanupStatusDto), StatusCodes.Status200OK)]
        public Task<IActionResult> GetCleanupStatus()
        {
            return ExecuteAsync(
                () => _cleanupStatusService.GetStatusAsync(),
                Ok,
                "GetCleanupStatus");
        }

        /// <summary>
        /// Gets whether the media cleanup service is currently enabled.
        /// </summary>
        [HttpGet("/api/admin/media-cleanup/enabled")]
        public Task<IActionResult> GetCleanupEnabled()
        {
            return ExecuteAsync(
                async () =>
                {
                    var isEnabled = await _cleanupStatusService.IsEnabledAsync();
                    return new { enabled = isEnabled };
                },
                Ok,
                "GetCleanupEnabled");
        }

        /// <summary>
        /// Enables or disables the media cleanup service at runtime.
        /// This setting persists across restarts via GlobalSettings.
        /// </summary>
        [HttpPost("/api/admin/media-cleanup/enabled")]
        public Task<IActionResult> SetCleanupEnabled([FromBody] UpdateMediaCleanupEnabledRequest request)
        {
            return ExecuteAsync(
                async () =>
                {
                    await _cleanupStatusService.SetEnabledAsync(request.Enabled);
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
                "SetCleanupEnabled");
        }

        /// <summary>
        /// Gets the simple retention override setting.
        /// </summary>
        [HttpGet("/api/admin/media-cleanup/simple-retention")]
        [ProducesResponseType(typeof(SimpleRetentionResponse), StatusCodes.Status200OK)]
        public Task<IActionResult> GetSimpleRetention()
        {
            return ExecuteAsync(
                async () =>
                {
                    var days = await _cleanupStatusService.GetSimpleRetentionOverrideAsync();
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
        /// Pass null for RetentionDays to clear the override and use policy-based retention.
        /// </summary>
        [HttpPost("/api/admin/media-cleanup/simple-retention")]
        [ProducesResponseType(typeof(SimpleRetentionResponse), StatusCodes.Status200OK)]
        public Task<IActionResult> SetSimpleRetention([FromBody] UpdateSimpleRetentionRequest request)
        {
            return ExecuteAsync(
                async () =>
                {
                    await _cleanupStatusService.SetSimpleRetentionOverrideAsync(request.RetentionDays);
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
