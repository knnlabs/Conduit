using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service for retrieving and downloading generated content files.
    /// </summary>
    /// <remarks>
    /// This service uses a typed HttpClient configured with retry policies for resilience
    /// when fetching files from external URLs. The retry policy handles transient HTTP errors
    /// and rate limiting (HTTP 429) with exponential backoff.
    /// </remarks>
    public class FileRetrievalService : IFileRetrievalService
    {
        private readonly IMediaStorageService _storageService;
        private readonly HttpClient _httpClient;
        private readonly ILogger<FileRetrievalService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileRetrievalService"/> class.
        /// </summary>
        /// <param name="storageService">The media storage service for local storage operations.</param>
        /// <param name="httpClient">The typed HTTP client configured with retry policies.</param>
        /// <param name="logger">The logger instance.</param>
        public FileRetrievalService(
            IMediaStorageService storageService,
            HttpClient httpClient,
            ILogger<FileRetrievalService> logger)
        {
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<FileRetrievalResult?> RetrieveFileAsync(string fileIdentifier, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fileIdentifier))
            {
                _logger.LogWarning("File identifier is null or empty");
                return null;
            }

            try
            {
                // Check if it's a URL
                if (IsUrl(fileIdentifier))
                {
                    return await RetrieveFromUrlAsync(fileIdentifier, cancellationToken);
                }

                // Otherwise, treat as storage key
                return await RetrieveFromStorageAsync(fileIdentifier, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving file {FileIdentifier}", fileIdentifier);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DownloadFileAsync(string fileIdentifier, string destinationPath, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await RetrieveFileAsync(fileIdentifier, cancellationToken);
                if (result == null)
                {
                    _logger.LogWarning("Failed to retrieve file {FileIdentifier}", fileIdentifier);
                    return false;
                }

                using (result)
                {
                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Write to file
                    using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
                    await result.ContentStream.CopyToAsync(fileStream, cancellationToken);
                    
                    _logger.LogInformation("Successfully downloaded file to {DestinationPath}", destinationPath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file {FileIdentifier} to {DestinationPath}", 
                    fileIdentifier, destinationPath);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<string?> GetDownloadUrlAsync(string fileIdentifier, TimeSpan expiration, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fileIdentifier))
            {
                return null;
            }

            try
            {
                // If it's already a URL, return it
                if (IsUrl(fileIdentifier))
                {
                    return fileIdentifier;
                }

                // Generate URL from storage service
                return await _storageService.GenerateUrlAsync(fileIdentifier, expiration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating download URL for {FileIdentifier}", fileIdentifier);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<FileMetadata?> GetFileMetadataAsync(string fileIdentifier, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fileIdentifier))
            {
                return null;
            }

            try
            {
                // Check if it's a URL
                if (IsUrl(fileIdentifier))
                {
                    return await GetUrlMetadataAsync(fileIdentifier, cancellationToken);
                }

                // Get from storage
                var mediaInfo = await _storageService.GetInfoAsync(fileIdentifier);
                if (mediaInfo == null)
                {
                    return null;
                }

                return new FileMetadata
                {
                    FileName = mediaInfo.FileName,
                    ContentType = mediaInfo.ContentType,
                    SizeBytes = mediaInfo.SizeBytes,
                    CreatedAt = mediaInfo.CreatedAt,
                    ModifiedAt = mediaInfo.CreatedAt, // No separate modified date in MediaInfo
                    StorageProvider = "storage",
                    ETag = $"\"{mediaInfo.StorageKey}\"", // Use storage key as ETag
                    AdditionalMetadata = mediaInfo.CustomMetadata
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting metadata for {FileIdentifier}", fileIdentifier);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> FileExistsAsync(string fileIdentifier, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fileIdentifier))
            {
                return false;
            }

            try
            {
                // Check if it's a URL
                if (IsUrl(fileIdentifier))
                {
                    return await CheckUrlExistsAsync(fileIdentifier, cancellationToken);
                }

                // Check in storage
                return await _storageService.ExistsAsync(fileIdentifier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence of {FileIdentifier}", fileIdentifier);
                return false;
            }
        }

        private bool IsUrl(string identifier)
        {
            return identifier.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   identifier.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<FileRetrievalResult?> RetrieveFromStorageAsync(string storageKey, CancellationToken cancellationToken)
        {
            var mediaInfo = await _storageService.GetInfoAsync(storageKey);
            if (mediaInfo == null)
            {
                _logger.LogWarning("Media info not found for storage key {StorageKey}", storageKey);
                return null;
            }

            var stream = await _storageService.GetStreamAsync(storageKey);
            if (stream == null)
            {
                _logger.LogWarning("Stream not found for storage key {StorageKey}", storageKey);
                return null;
            }

            return new FileRetrievalResult
            {
                ContentStream = stream,
                Metadata = new FileMetadata
                {
                    FileName = mediaInfo.FileName,
                    ContentType = mediaInfo.ContentType,
                    SizeBytes = mediaInfo.SizeBytes,
                    CreatedAt = mediaInfo.CreatedAt,
                    ModifiedAt = mediaInfo.CreatedAt, // No separate modified date in MediaInfo
                    StorageProvider = "storage",
                    ETag = $"\"{mediaInfo.StorageKey}\"", // Use storage key as ETag
                    AdditionalMetadata = mediaInfo.CustomMetadata
                }
            };
        }

        private async Task<FileRetrievalResult?> RetrieveFromUrlAsync(string url, CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to retrieve URL {Url}: {StatusCode}", url, response.StatusCode);
                response.Dispose();
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var contentLength = response.Content.Headers.ContentLength ?? 0;
            var lastModified = response.Content.Headers.LastModified?.DateTime;
            var etag = response.Headers.ETag?.Tag;

            // Extract filename from Content-Disposition header if available
            string? fileName = null;
            if (response.Content.Headers.ContentDisposition?.FileName != null)
            {
                fileName = response.Content.Headers.ContentDisposition.FileName.Trim('"');
            }
            else
            {
                // Try to extract from URL
                fileName = Path.GetFileName(new Uri(url).LocalPath);
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            return new FileRetrievalResult
            {
                ContentStream = stream,
                Metadata = new FileMetadata
                {
                    FileName = fileName,
                    ContentType = contentType,
                    SizeBytes = contentLength,
                    ModifiedAt = lastModified,
                    StorageProvider = "url",
                    ETag = etag,
                    SupportsRangeRequests = response.Headers.AcceptRanges?.Contains("bytes") == true
                }
            };
        }

        private async Task<FileMetadata?> GetUrlMetadataAsync(string url, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var contentLength = response.Content.Headers.ContentLength ?? 0;
            var lastModified = response.Content.Headers.LastModified?.DateTime;
            var etag = response.Headers.ETag?.Tag;

            string? fileName = null;
            if (response.Content.Headers.ContentDisposition?.FileName != null)
            {
                fileName = response.Content.Headers.ContentDisposition.FileName.Trim('"');
            }
            else
            {
                fileName = Path.GetFileName(new Uri(url).LocalPath);
            }

            return new FileMetadata
            {
                FileName = fileName,
                ContentType = contentType,
                SizeBytes = contentLength,
                ModifiedAt = lastModified,
                StorageProvider = "url",
                ETag = etag,
                SupportsRangeRequests = response.Headers.AcceptRanges?.Contains("bytes") == true
            };
        }

        private async Task<bool> CheckUrlExistsAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "URL existence check failed for {Url}", url);
                return false;
            }
        }
    }
}