using System;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Handles media file retrieval and serving.
    /// </summary>
    [ApiController]
    [Route("v1/media")]
    [Authorize]
    public class MediaController : ControllerBase
    {
        private readonly IMediaStorageService _storageService;
        private readonly ILogger<MediaController> _logger;

        public MediaController(
            IMediaStorageService storageService,
            ILogger<MediaController> logger)
        {
            _storageService = storageService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves a media file by its storage key.
        /// </summary>
        /// <param name="storageKey">The unique storage key.</param>
        /// <returns>The media file.</returns>
        [HttpGet("{**storageKey}")]
        [AllowAnonymous] // Media URLs should work without auth
        public async Task<IActionResult> GetMedia(string storageKey)
        {
            try
            {
                // Validate storage key
                if (string.IsNullOrWhiteSpace(storageKey))
                {
                    return BadRequest("Invalid storage key");
                }

                // Get media info
                var mediaInfo = await _storageService.GetInfoAsync(storageKey);
                if (mediaInfo == null)
                {
                    return NotFound();
                }

                // Get media stream
                var stream = await _storageService.GetStreamAsync(storageKey);
                if (stream == null)
                {
                    return NotFound();
                }

                // Set cache headers for performance
                Response.Headers["Cache-Control"] = "public, max-age=3600"; // 1 hour
                Response.Headers["ETag"] = $"\"{storageKey}\"";

                // Return file with proper content type
                return File(stream, mediaInfo.ContentType, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving media with key {StorageKey}", storageKey);
                return StatusCode(500, "An error occurred while retrieving the media");
            }
        }

        /// <summary>
        /// Gets metadata information about a media file.
        /// </summary>
        /// <param name="storageKey">The unique storage key.</param>
        /// <returns>Media metadata.</returns>
        [HttpGet("info/{**storageKey}")]
        public async Task<IActionResult> GetMediaInfo(string storageKey)
        {
            try
            {
                var mediaInfo = await _storageService.GetInfoAsync(storageKey);
                if (mediaInfo == null)
                {
                    return NotFound();
                }

                return Ok(mediaInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving media info for key {StorageKey}", storageKey);
                return StatusCode(500, "An error occurred while retrieving media information");
            }
        }

        /// <summary>
        /// Checks if a media file exists.
        /// </summary>
        /// <param name="storageKey">The unique storage key.</param>
        /// <returns>True if the media exists.</returns>
        [HttpHead("{**storageKey}")]
        [AllowAnonymous]
        public async Task<IActionResult> CheckMediaExists(string storageKey)
        {
            try
            {
                var exists = await _storageService.ExistsAsync(storageKey);
                if (!exists)
                {
                    return NotFound();
                }

                var mediaInfo = await _storageService.GetInfoAsync(storageKey);
                if (mediaInfo != null)
                {
                    Response.Headers["Content-Type"] = mediaInfo.ContentType;
                    Response.Headers["Content-Length"] = mediaInfo.SizeBytes.ToString();
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking media existence for key {StorageKey}", storageKey);
                return StatusCode(500);
            }
        }
    }
}