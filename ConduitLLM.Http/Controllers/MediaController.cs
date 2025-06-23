using System;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

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

                // Check if this is a video and if range is requested
                if (mediaInfo.MediaType == MediaType.Video && Request.Headers.ContainsKey(HeaderNames.Range))
                {
                    return await HandleVideoRangeRequest(storageKey, mediaInfo);
                }

                // Get media stream for non-video or non-range requests
                var stream = await _storageService.GetStreamAsync(storageKey);
                if (stream == null)
                {
                    return NotFound();
                }

                // Set cache headers for performance
                Response.Headers["Cache-Control"] = "public, max-age=3600"; // 1 hour
                Response.Headers["ETag"] = $"\"{storageKey}\"";

                // Add CORS headers for video playback
                if (mediaInfo.MediaType == MediaType.Video)
                {
                    Response.Headers["Accept-Ranges"] = "bytes";
                    Response.Headers["Access-Control-Allow-Origin"] = "*";
                    Response.Headers["Access-Control-Allow-Methods"] = "GET, HEAD, OPTIONS";
                    Response.Headers["Access-Control-Allow-Headers"] = "Range";
                }

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

        /// <summary>
        /// Handles HTTP range requests for video streaming.
        /// </summary>
        private async Task<IActionResult> HandleVideoRangeRequest(string storageKey, MediaInfo mediaInfo)
        {
            try
            {
                var rangeHeader = Request.Headers[HeaderNames.Range].FirstOrDefault();
                if (string.IsNullOrEmpty(rangeHeader))
                {
                    return BadRequest("Invalid range header");
                }

                // Parse range header (e.g., "bytes=0-1023")
                var range = ParseRangeHeader(rangeHeader, mediaInfo.SizeBytes);
                if (range == null)
                {
                    return StatusCode(416, "Requested Range Not Satisfiable"); // 416 Range Not Satisfiable
                }

                // Get video stream with range
                var rangedStream = await _storageService.GetVideoStreamAsync(
                    storageKey, 
                    range.Value.Start, 
                    range.Value.End);

                if (rangedStream == null)
                {
                    return NotFound();
                }

                // Set response headers for partial content
                Response.StatusCode = 206; // Partial Content
                Response.Headers["Accept-Ranges"] = "bytes";
                Response.Headers["Content-Range"] = $"bytes {rangedStream.RangeStart}-{rangedStream.RangeEnd}/{rangedStream.TotalSize}";
                Response.Headers["Content-Length"] = rangedStream.ContentLength.ToString();
                Response.Headers["Cache-Control"] = "public, max-age=3600";
                Response.Headers["ETag"] = $"\"{storageKey}\"";
                
                // CORS headers for video playback
                Response.Headers["Access-Control-Allow-Origin"] = "*";
                Response.Headers["Access-Control-Allow-Methods"] = "GET, HEAD, OPTIONS";
                Response.Headers["Access-Control-Allow-Headers"] = "Range";

                return File(rangedStream.Stream, rangedStream.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling video range request for key {StorageKey}", storageKey);
                return StatusCode(500, "An error occurred while streaming the video");
            }
        }

        /// <summary>
        /// Parses HTTP range header.
        /// </summary>
        private (long Start, long End)? ParseRangeHeader(string rangeHeader, long totalSize)
        {
            try
            {
                // Remove "bytes=" prefix
                if (!rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var rangeValue = rangeHeader.Substring(6);
                var parts = rangeValue.Split('-');

                if (parts.Length != 2)
                {
                    return null;
                }

                long start = 0;
                long end = totalSize - 1;

                // Parse start
                if (!string.IsNullOrEmpty(parts[0]))
                {
                    if (!long.TryParse(parts[0], out start))
                    {
                        return null;
                    }
                }

                // Parse end
                if (!string.IsNullOrEmpty(parts[1]))
                {
                    if (!long.TryParse(parts[1], out end))
                    {
                        return null;
                    }
                }
                else if (!string.IsNullOrEmpty(parts[0]))
                {
                    // If no end specified, use a reasonable chunk size (1MB)
                    end = Math.Min(start + 1024 * 1024 - 1, totalSize - 1);
                }

                // Validate range
                if (start < 0 || start >= totalSize || end < start || end >= totalSize)
                {
                    return null;
                }

                return (start, end);
            }
            catch
            {
                return null;
            }
        }
    }
}