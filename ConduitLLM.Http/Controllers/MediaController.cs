using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Handles media file upload, retrieval and serving.
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
        /// Uploads a media file and returns the storage URL.
        /// </summary>
        /// <param name="file">The file to upload.</param>
        /// <param name="mediaType">Optional media type (image/video/audio).</param>
        /// <returns>The storage result with URL.</returns>
        [HttpPost("upload")]
        [Authorize]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(524288000)] // 500MB limit
        public async Task<IActionResult> UploadMedia(
            [FromForm] IFormFile file,
            [FromForm] string? mediaType = null)
        {
            try
            {
                // Validate file
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { error = "No file provided or file is empty" });
                }

                // Validate file extension
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var allowedImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg" };
                var allowedVideoExtensions = new[] { ".mp4", ".webm", ".mov", ".avi", ".mkv", ".flv", ".wmv", ".m4v" };
                var allowedAudioExtensions = new[] { ".mp3", ".wav", ".ogg", ".m4a", ".flac", ".aac" };

                // Determine media type from extension if not provided
                MediaType determinedMediaType;
                if (!string.IsNullOrEmpty(mediaType))
                {
                    if (!Enum.TryParse<MediaType>(mediaType, true, out determinedMediaType))
                    {
                        return BadRequest(new { error = "Invalid media type. Must be Image, Video, or Audio" });
                    }
                }
                else if (allowedImageExtensions.Contains(extension))
                {
                    determinedMediaType = MediaType.Image;
                }
                else if (allowedVideoExtensions.Contains(extension))
                {
                    determinedMediaType = MediaType.Video;
                }
                else if (allowedAudioExtensions.Contains(extension))
                {
                    determinedMediaType = MediaType.Audio;
                }
                else
                {
                    return BadRequest(new { error = $"Unsupported file extension: {extension}" });
                }

                // Validate file size based on type
                var maxSizeBytes = determinedMediaType switch
                {
                    MediaType.Image => 104857600L, // 100MB for images
                    MediaType.Video => 524288000L, // 500MB for videos
                    MediaType.Audio => 209715200L, // 200MB for audio
                    _ => 104857600L // Default 100MB
                };

                if (file.Length > maxSizeBytes)
                {
                    var maxSizeMB = maxSizeBytes / (1024 * 1024);
                    return BadRequest(new { error = $"File size exceeds maximum allowed size of {maxSizeMB}MB for {determinedMediaType}" });
                }

                // Create metadata
                var metadata = new MediaMetadata
                {
                    MediaType = determinedMediaType,
                    ContentType = file.ContentType ?? GetContentTypeFromExtension(extension),
                    FileName = file.FileName
                };

                // Upload file using storage service
                using var stream = file.OpenReadStream();
                var result = await _storageService.StoreAsync(stream, metadata);

                _logger.LogInformation("Media uploaded successfully. Type: {MediaType}, Size: {Size} bytes, Key: {StorageKey}",
                    determinedMediaType, file.Length, result.StorageKey);

                // Return result with full URL
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                return Ok(new
                {
                    success = true,
                    storageKey = result.StorageKey,
                    url = result.Url,
                    directUrl = $"{baseUrl}/v1/media/{result.StorageKey}",
                    contentType = metadata.ContentType,
                    mediaType = determinedMediaType.ToString(),
                    fileName = file.FileName,
                    sizeBytes = file.Length
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading media file");
                return StatusCode(500, new { error = "An error occurred while uploading the media file" });
            }
        }

        /// <summary>
        /// Gets content type from file extension.
        /// </summary>
        private string GetContentTypeFromExtension(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".svg" => "image/svg+xml",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".mov" => "video/quicktime",
                ".avi" => "video/x-msvideo",
                ".mkv" => "video/x-matroska",
                ".flv" => "video/x-flv",
                ".wmv" => "video/x-ms-wmv",
                ".m4v" => "video/x-m4v",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".ogg" => "audio/ogg",
                ".m4a" => "audio/mp4",
                ".flac" => "audio/flac",
                ".aac" => "audio/aac",
                _ => "application/octet-stream"
            };
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
