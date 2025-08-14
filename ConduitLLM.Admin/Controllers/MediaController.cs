using System;
using System.Threading.Tasks;
using ConduitLLM.Admin.Interfaces;
using Microsoft.AspNetCore.Authorization;
using ConduitLLM.Configuration.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Administrative controller for media lifecycle management.
    /// </summary>
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class MediaController : ControllerBase
    {
        private readonly IAdminMediaService _mediaService;
        private readonly ILogger<MediaController> _logger;

        /// <summary>
        /// Initializes a new instance of the MediaController class.
        /// </summary>
        /// <param name="mediaService">The admin media service.</param>
        /// <param name="logger">The logger instance.</param>
        public MediaController(
            IAdminMediaService mediaService,
            ILogger<MediaController> logger)
        {
            _mediaService = mediaService ?? throw new ArgumentNullException(nameof(mediaService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets overall storage statistics across all virtual keys.
        /// </summary>
        /// <returns>Overall storage statistics.</returns>
        [HttpGet("stats")]
        public async Task<IActionResult> GetOverallStats()
        {
            try
            {
                var stats = await _mediaService.GetOverallStorageStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting overall storage statistics");
                return StatusCode(500, new ErrorResponseDto("Failed to get storage statistics"));
            }
        }

        /// <summary>
        /// Gets storage statistics for a specific virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key.</param>
        /// <returns>Storage statistics for the virtual key.</returns>
        [HttpGet("stats/virtual-key/{virtualKeyId}")]
        public async Task<IActionResult> GetStatsByVirtualKey(int virtualKeyId)
        {
            try
            {
                var stats = await _mediaService.GetStorageStatsByVirtualKeyAsync(virtualKeyId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage statistics for virtual key {VirtualKeyId}", virtualKeyId);
                return StatusCode(500, new ErrorResponseDto("Failed to get storage statistics"));
            }
        }

        /// <summary>
        /// Gets storage statistics grouped by provider.
        /// </summary>
        /// <returns>Dictionary of provider names to storage size.</returns>
        [HttpGet("stats/by-provider")]
        public async Task<IActionResult> GetStatsByProvider()
        {
            try
            {
                var stats = await _mediaService.GetStorageStatsByProviderAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage statistics by provider");
                return StatusCode(500, new ErrorResponseDto("Failed to get storage statistics"));
            }
        }

        /// <summary>
        /// Gets storage statistics grouped by media type.
        /// </summary>
        /// <returns>Dictionary of media types to storage size.</returns>
        [HttpGet("stats/by-type")]
        public async Task<IActionResult> GetStatsByMediaType()
        {
            try
            {
                var stats = await _mediaService.GetStorageStatsByMediaTypeAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage statistics by media type");
                return StatusCode(500, new ErrorResponseDto("Failed to get storage statistics"));
            }
        }

        /// <summary>
        /// Gets media records for a specific virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key.</param>
        /// <returns>List of media records.</returns>
        [HttpGet("virtual-key/{virtualKeyId}")]
        public async Task<IActionResult> GetMediaByVirtualKey(int virtualKeyId)
        {
            try
            {
                var media = await _mediaService.GetMediaByVirtualKeyAsync(virtualKeyId);
                return Ok(media);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting media for virtual key {VirtualKeyId}", virtualKeyId);
                return StatusCode(500, new ErrorResponseDto("Failed to get media records"));
            }
        }

        /// <summary>
        /// Searches for media records by storage key pattern.
        /// </summary>
        /// <param name="pattern">The pattern to search for in storage keys.</param>
        /// <returns>List of matching media records.</returns>
        [HttpGet("search")]
        public async Task<IActionResult> SearchMedia([FromQuery] string pattern)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    return BadRequest(new ErrorResponseDto("Search pattern is required"));
                }

                var media = await _mediaService.SearchMediaByStorageKeyAsync(pattern);
                return Ok(media);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching media with pattern {Pattern}", pattern);
                return StatusCode(500, new ErrorResponseDto("Failed to search media"));
            }
        }

        /// <summary>
        /// Deletes a specific media record and its associated file.
        /// </summary>
        /// <param name="mediaId">The ID of the media record to delete.</param>
        /// <returns>Success status.</returns>
        [HttpDelete("{mediaId}")]
        public async Task<IActionResult> DeleteMedia(Guid mediaId)
        {
            try
            {
                var result = await _mediaService.DeleteMediaAsync(mediaId);
                if (!result)
                {
                    return NotFound(new ErrorResponseDto("Media record not found"));
                }

                return Ok(new { message = "Media deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting media {MediaId}", mediaId);
                return StatusCode(500, new ErrorResponseDto("Failed to delete media"));
            }
        }

        /// <summary>
        /// Manually triggers cleanup of expired media files.
        /// </summary>
        /// <returns>Number of files cleaned up.</returns>
        [HttpPost("cleanup/expired")]
        public async Task<IActionResult> CleanupExpiredMedia()
        {
            try
            {
                var count = await _mediaService.CleanupExpiredMediaAsync();
                return Ok(new { 
                    message = $"Cleaned up {count} expired media files",
                    deletedCount = count 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during expired media cleanup");
                return StatusCode(500, new ErrorResponseDto("Failed to cleanup expired media"));
            }
        }

        /// <summary>
        /// Manually triggers cleanup of orphaned media files.
        /// </summary>
        /// <returns>Number of files cleaned up.</returns>
        [HttpPost("cleanup/orphaned")]
        public async Task<IActionResult> CleanupOrphanedMedia()
        {
            try
            {
                var count = await _mediaService.CleanupOrphanedMediaAsync();
                return Ok(new { 
                    message = $"Cleaned up {count} orphaned media files",
                    deletedCount = count 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during orphaned media cleanup");
                return StatusCode(500, new ErrorResponseDto("Failed to cleanup orphaned media"));
            }
        }

        /// <summary>
        /// Manually triggers pruning of old media files.
        /// </summary>
        /// <param name="request">The pruning request with days to keep.</param>
        /// <returns>Number of files pruned.</returns>
        [HttpPost("cleanup/prune")]
        public async Task<IActionResult> PruneOldMedia([FromBody] PruneMediaRequest request)
        {
            try
            {
                if (request?.DaysToKeep == null || request.DaysToKeep <= 0)
                {
                    return BadRequest(new ErrorResponseDto("DaysToKeep must be a positive number"));
                }

                var count = await _mediaService.PruneOldMediaAsync(request.DaysToKeep.Value);
                return Ok(new { 
                    message = $"Pruned {count} media files older than {request.DaysToKeep} days",
                    deletedCount = count 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during old media pruning");
                return StatusCode(500, new ErrorResponseDto("Failed to prune old media"));
            }
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
