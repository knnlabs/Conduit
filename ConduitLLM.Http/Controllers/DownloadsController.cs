using System;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Controller for handling file downloads with enhanced features.
    /// </summary>
    [ApiController]
    [Route("v1/downloads")]
    [Authorize]
    public class DownloadsController : ControllerBase
    {
        private readonly IFileRetrievalService _fileRetrievalService;
        private readonly ILogger<DownloadsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DownloadsController"/> class.
        /// </summary>
        public DownloadsController(
            IFileRetrievalService fileRetrievalService,
            ILogger<DownloadsController> logger)
        {
            _fileRetrievalService = fileRetrievalService ?? throw new ArgumentNullException(nameof(fileRetrievalService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Downloads a file by its identifier with support for range requests.
        /// </summary>
        /// <param name="fileId">The file identifier (storage key or URL).</param>
        /// <param name="inline">Whether to display inline (true) or force download (false).</param>
        /// <returns>The file content.</returns>
        [HttpGet("{**fileId}")]
        public async Task<IActionResult> DownloadFile(string fileId, [FromQuery] bool inline = false)
        {
            try
            {
                var result = await _fileRetrievalService.RetrieveFileAsync(fileId);
                if (result == null)
                {
                    return NotFound(new { error = new { message = "File not found", type = "not_found" } });
                }

                using (result)
                {
                    // Set appropriate headers
                    if (!inline && !string.IsNullOrEmpty(result.Metadata.FileName))
                    {
                        Response.Headers["Content-Disposition"] = $"attachment; filename=\"{result.Metadata.FileName}\"";
                    }

                    // Set cache headers
                    if (!string.IsNullOrEmpty(result.Metadata.ETag))
                    {
                        Response.Headers["ETag"] = result.Metadata.ETag;
                        Response.Headers["Cache-Control"] = "private, max-age=3600";
                    }

                    // Return file with range processing support
                    return File(
                        result.ContentStream, 
                        result.Metadata.ContentType,
                        result.Metadata.FileName,
                        enableRangeProcessing: result.Metadata.SupportsRangeRequests);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file {FileId}", fileId);
                return StatusCode(500, new { error = new { message = "An error occurred while downloading the file", type = "server_error" } });
            }
        }

        /// <summary>
        /// Gets metadata information about a file.
        /// </summary>
        /// <param name="fileId">The file identifier.</param>
        /// <returns>File metadata.</returns>
        [HttpGet("metadata/{**fileId}")]
        public async Task<IActionResult> GetFileMetadata(string fileId)
        {
            try
            {
                var metadata = await _fileRetrievalService.GetFileMetadataAsync(fileId);
                if (metadata == null)
                {
                    return NotFound(new { error = new { message = "File not found", type = "not_found" } });
                }

                return Ok(new
                {
                    file_name = metadata.FileName,
                    content_type = metadata.ContentType,
                    size_bytes = metadata.SizeBytes,
                    created_at = metadata.CreatedAt,
                    modified_at = metadata.ModifiedAt,
                    storage_provider = metadata.StorageProvider,
                    etag = metadata.ETag,
                    supports_range_requests = metadata.SupportsRangeRequests,
                    additional_metadata = metadata.AdditionalMetadata
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting metadata for file {FileId}", fileId);
                return StatusCode(500, new { error = new { message = "An error occurred while retrieving file metadata", type = "server_error" } });
            }
        }

        /// <summary>
        /// Generates a temporary download URL for a file.
        /// </summary>
        /// <param name="request">The URL generation request.</param>
        /// <returns>A temporary download URL.</returns>
        [HttpPost("generate-url")]
        public async Task<IActionResult> GenerateDownloadUrl([FromBody] GenerateUrlRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.FileId))
                {
                    return BadRequest(new { error = new { message = "File ID is required", type = "invalid_request_error" } });
                }

                var expirationMinutes = request.ExpirationMinutes ?? 60; // Default 1 hour
                if (expirationMinutes < 1 || expirationMinutes > 10080) // Max 1 week
                {
                    return BadRequest(new { error = new { message = "Expiration must be between 1 minute and 1 week", type = "invalid_request_error" } });
                }

                var expiration = TimeSpan.FromMinutes(expirationMinutes);
                var url = await _fileRetrievalService.GetDownloadUrlAsync(request.FileId, expiration);

                if (string.IsNullOrEmpty(url))
                {
                    return NotFound(new { error = new { message = "File not found or URL generation failed", type = "not_found" } });
                }

                return Ok(new
                {
                    url = url,
                    expires_at = DateTime.UtcNow.Add(expiration),
                    expiration_minutes = expirationMinutes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating download URL for file {FileId}", request.FileId);
                return StatusCode(500, new { error = new { message = "An error occurred while generating download URL", type = "server_error" } });
            }
        }

        /// <summary>
        /// Checks if a file exists.
        /// </summary>
        /// <param name="fileId">The file identifier.</param>
        /// <returns>200 OK if exists, 404 if not.</returns>
        [HttpHead("{**fileId}")]
        public async Task<IActionResult> CheckFileExists(string fileId)
        {
            try
            {
                var exists = await _fileRetrievalService.FileExistsAsync(fileId);
                if (!exists)
                {
                    return NotFound();
                }

                var metadata = await _fileRetrievalService.GetFileMetadataAsync(fileId);
                if (metadata != null)
                {
                    Response.Headers["Content-Type"] = metadata.ContentType;
                    Response.Headers["Content-Length"] = metadata.SizeBytes.ToString();
                    if (!string.IsNullOrEmpty(metadata.ETag))
                    {
                        Response.Headers["ETag"] = metadata.ETag;
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence of file {FileId}", fileId);
                return StatusCode(500);
            }
        }
    }

    /// <summary>
    /// Request to generate a temporary download URL.
    /// </summary>
    public class GenerateUrlRequest
    {
        /// <summary>
        /// The file identifier.
        /// </summary>
        public required string FileId { get; set; }

        /// <summary>
        /// How many minutes the URL should be valid (1-10080).
        /// </summary>
        public int? ExpirationMinutes { get; set; }
    }
}