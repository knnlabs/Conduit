using System.Collections.Concurrent;
using System.Security.Cryptography;

using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

using ConduitLLM.Configuration.Utilities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Provides media storage using S3-compatible services (AWS S3, Cloudflare R2, MinIO, etc.).
    /// </summary>
    public partial class S3MediaStorageService : IMediaStorageService, IDisposable
    {
        private static readonly TimeSpan MultipartSessionLifetime = TimeSpan.FromHours(24);
        private static readonly TimeSpan MultipartCleanupInterval = TimeSpan.FromHours(1);
        private readonly IAmazonS3 _s3Client;
        private readonly S3StorageOptions _options;
        private readonly ILogger<S3MediaStorageService> _logger;
        private readonly IMediaQuotaGuard? _quotaGuard;
        private readonly string _bucketName;
        private readonly TransferUtility _transferUtility;
        private readonly ConcurrentDictionary<string, MultipartUploadState> _multipartUploads = new();
        private readonly TimeProvider _timeProvider;
        private readonly Timer _multipartCleanupTimer;
        private readonly SemaphoreSlim _multipartCleanupLock = new(1, 1);
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _bucketInitialized;
        private bool _disposed;

        public S3MediaStorageService(
            IOptions<S3StorageOptions> options,
            ILogger<S3MediaStorageService> logger,
            TimeProvider? timeProvider = null,
            IMediaQuotaGuard? quotaGuard = null,
            IAmazonS3? s3Client = null)
        {
            _options = options.Value;
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _quotaGuard = quotaGuard;
            
            // Validate required configuration
            if (string.IsNullOrEmpty(_options.AccessKey))
            {
                throw new InvalidOperationException("S3 AccessKey is required. Set CONDUITLLM__STORAGE__S3__ACCESSKEY environment variable.");
            }
            
            if (string.IsNullOrEmpty(_options.SecretKey))
            {
                throw new InvalidOperationException("S3 SecretKey is required. Set CONDUITLLM__STORAGE__S3__SECRETKEY environment variable.");
            }
            
            if (string.IsNullOrEmpty(_options.BucketName))
            {
                throw new InvalidOperationException("S3 BucketName is required. Set CONDUITLLM__STORAGE__S3__BUCKETNAME environment variable.");
            }
            
            _bucketName = _options.BucketName;
            
            // Log R2 detection and configuration
            _logger.LogInformation("S3 Storage Options - ServiceUrl: {ServiceUrl}, IsR2: {IsR2}", _options.ServiceUrl, _options.IsR2);
            if (_options.IsR2)
            {
                _logger.LogInformation("Cloudflare R2 detected. Using optimized settings: MultipartChunkSize={ChunkSize}MB, MultipartThreshold={Threshold}MB", 
                    _options.MultipartChunkSizeBytes / (1024 * 1024), 
                    _options.MultipartThresholdBytes / (1024 * 1024));
            }
            
            _logger.LogInformation("S3MediaStorageService initialized with bucket: {BucketName}, ServiceUrl: {ServiceUrl}, Region: {Region}", 
                _bucketName, _options.ServiceUrl ?? "default", _options.Region);

            var config = new AmazonS3Config
            {
                ForcePathStyle = _options.ForcePathStyle,
                UseHttp = false,
                // Disable automatic checksum calculation for R2 compatibility
                // AWS SDK v4 adds CRC32 checksums by default which R2 doesn't fully support
                RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED
            };

            if (!string.IsNullOrEmpty(_options.ServiceUrl))
            {
                config.ServiceURL = _options.ServiceUrl;
                // Don't set RegionEndpoint when using ServiceURL for R2
            }
            else if (!string.IsNullOrEmpty(_options.Region) && _options.Region != "auto")
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(_options.Region);
            }

            // For R2, we'll rely on using PutObject instead of TransferUtility
            // to avoid streaming signature issues

            _s3Client = s3Client ??
                new AmazonS3Client(_options.AccessKey, _options.SecretKey, config);
            _transferUtility = new TransferUtility(_s3Client);
            _multipartCleanupTimer = new Timer(
                _ => _ = CleanupExpiredMultipartUploadsAsync(),
                null,
                MultipartCleanupInterval,
                MultipartCleanupInterval);

            // Bucket initialization is now deferred to first use to avoid blocking startup
            _logger.LogInformation("S3MediaStorageService initialized (bucket check deferred to first use)");
        }

        /// <summary>
        /// Stops multipart cleanup and releases tracked session state.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _multipartCleanupTimer.Dispose();
            _multipartUploads.Clear();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Ensures the bucket is initialized before use. Thread-safe and only runs once.
        /// </summary>
        private async Task EnsureBucketInitializedAsync()
        {
            if (_bucketInitialized)
            {
                return;
            }

            await _initLock.WaitAsync();
            try
            {
                if (_bucketInitialized)
                {
                    return;
                }

                await EnsureBucketExistsAsync();
                _bucketInitialized = true;
                _logger.LogInformation("S3 bucket initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize S3 bucket");
                throw;
            }
            finally
            {
                _initLock.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<MediaStorageResult> StoreAsync(Stream content, MediaMetadata metadata, IProgress<long>? progress = null)
        {
            // Ensure bucket exists on first use
            await EnsureBucketInitializedAsync();

            _logger.LogInformation("StoreAsync called - IsR2: {IsR2}", _options.IsR2);

            // Track if we created a memory stream that needs disposal
            MemoryStream? memoryStream = null;

            try
            {
                // For streaming, we can't compute hash beforehand, so generate a unique key
                var temporaryKey = Guid.NewGuid().ToString();
                var extension = GetExtensionFromContentType(metadata.ContentType);
                var storageKey = GenerateStorageKey(temporaryKey, metadata.MediaType, extension);

                // If the stream doesn't support seeking, we need to buffer it
                Stream uploadStream = content;
                if (!content.CanSeek)
                {
                    _logger.LogDebug("Stream does not support seeking, buffering to memory");
                    memoryStream = new MemoryStream();
                    await content.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                    uploadStream = memoryStream;
                }

                if (_quotaGuard != null)
                {
                    await _quotaGuard.EnsureCanStoreAsync(
                        metadata.CreatedBy,
                        Math.Max(0, uploadStream.Length - uploadStream.Position));
                }

                // Wrap stream with progress reporting if needed
                if (progress != null)
                {
                    uploadStream = new ProgressReportingStream(uploadStream, progress);
                }

                // Check if we need to use multipart upload based on stream length
                // For R2, we can't use TransferUtility due to streaming signature issues
                // So we'll always use PutObject for R2, regardless of file size
                bool useTransferUtility = !_options.IsR2 && uploadStream.Length > _options.MultipartThresholdBytes;
                
                _logger.LogInformation("S3 Upload Decision: IsR2={IsR2}, StreamLength={Length}, Threshold={Threshold}, UseTransferUtility={UseTransfer}", 
                    _options.IsR2, uploadStream.Length, _options.MultipartThresholdBytes, useTransferUtility);

                // Store content length before upload (stream may be disposed after)
                long contentLength = uploadStream.Length;
                string? etag = null;

                if (useTransferUtility)
                {
                    // Use TransferUtility for large files
                    var uploadRequest = new TransferUtilityUploadRequest
                    {
                        BucketName = _bucketName,
                        Key = storageKey,
                        InputStream = uploadStream,
                        ContentType = metadata.ContentType,
                        PartSize = _options.MultipartChunkSizeBytes,
                        CannedACL = S3CannedACL.Private
                    };
                    
                    // In AWS SDK v3, DisablePayloadSigning might not be available on TransferUtilityUploadRequest
                    // We'll need to use PutObject for R2 instead

                    // Add metadata
                    uploadRequest.Metadata.Add("content-type", metadata.ContentType);
                    uploadRequest.Metadata.Add("media-type", metadata.MediaType.ToString());
                    
                    // Only add optional metadata if not null/empty (R2 doesn't like empty metadata values)
                    if (!string.IsNullOrEmpty(metadata.FileName))
                    {
                        uploadRequest.Metadata.Add("original-filename", metadata.FileName);
                    }
                    
                    if (!string.IsNullOrEmpty(metadata.CreatedBy))
                    {
                        uploadRequest.Metadata.Add("created-by", metadata.CreatedBy);
                    }

                    // Add custom metadata (skip empty values)
                    foreach (var (key, value) in metadata.CustomMetadata ?? new Dictionary<string, string>())
                    {
                        if (!string.IsNullOrEmpty(value))
                        {
                            uploadRequest.Metadata.Add($"custom-{key}", value);
                        }
                    }

                    if (metadata.ExpiresAt.HasValue)
                    {
                        uploadRequest.Metadata.Add("expires-at", metadata.ExpiresAt.Value.ToString("O"));
                    }

                    // Subscribe to upload progress events
                    uploadRequest.UploadProgressEvent += (sender, args) =>
                    {
                        // TransferUtility already reports progress
                        _logger.LogDebug("Upload progress: {TransferredBytes}/{TotalBytes}", 
                            args.TransferredBytes, args.TotalBytes);
                    };

                    await _transferUtility.UploadAsync(uploadRequest);
                }
                else
                {
                    // Use regular PutObject for smaller files
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = storageKey,
                        InputStream = uploadStream,
                        ContentType = metadata.ContentType
                    };
                    
                    // For R2 compatibility, disable payload signing
                    if (_options.IsR2)
                    {
                        putRequest.DisablePayloadSigning = true;
                        putRequest.DisableDefaultChecksumValidation = true;
                        _logger.LogInformation("R2 detected - DisablePayloadSigning and DisableDefaultChecksumValidation set to true for PutObjectRequest");
                    }

                    // Add metadata
                    putRequest.Metadata.Add("content-type", metadata.ContentType);
                    putRequest.Metadata.Add("media-type", metadata.MediaType.ToString());
                    
                    // Only add optional metadata if not null/empty (R2 doesn't like empty metadata values)
                    if (!string.IsNullOrEmpty(metadata.FileName))
                    {
                        putRequest.Metadata.Add("original-filename", metadata.FileName);
                    }
                    
                    if (!string.IsNullOrEmpty(metadata.CreatedBy))
                    {
                        putRequest.Metadata.Add("created-by", metadata.CreatedBy);
                    }

                    // Add custom metadata (skip empty values)
                    foreach (var (key, value) in metadata.CustomMetadata ?? new Dictionary<string, string>())
                    {
                        if (!string.IsNullOrEmpty(value))
                        {
                            putRequest.Metadata.Add($"custom-{key}", value);
                        }
                    }

                    if (metadata.ExpiresAt.HasValue)
                    {
                        putRequest.Metadata.Add("expires-at", metadata.ExpiresAt.Value.ToString("O"));
                    }

                    var response = await _s3Client.PutObjectAsync(putRequest);
                    etag = response.ETag;
                }

                _logger.LogInformation("Stored media with key {StorageKey} to S3", storageKey);

                // Generate public URL
                var url = await GenerateUrlAsync(storageKey, _options.DefaultUrlExpiration);

                return new MediaStorageResult
                {
                    StorageKey = storageKey,
                    Url = url,
                    SizeBytes = contentLength,
                    ContentHash = etag ?? temporaryKey,
                    CreatedAt = DateTime.UtcNow
                };
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(ex, "AWS S3 error while storing media: {ErrorCode}", ex.ErrorCode);
                
                if (ex.StatusCode == System.Net.HttpStatusCode.RequestEntityTooLarge)
                {
                    throw new InvalidOperationException("File size exceeds S3 limits", ex);
                }
                else if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                         ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new UnauthorizedAccessException("Insufficient permissions to upload to S3", ex);
                }
                else if (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable || 
                         ex.ErrorCode == "SlowDown")
                {
                    throw new InvalidOperationException("S3 service is throttling requests. Please retry later.", ex);
                }
                
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store media to S3");
                throw;
            }
            finally
            {
                // Dispose of the memory stream if we created one
                memoryStream?.Dispose();
            }
        }

        /// <summary>
        /// Stores base64 encoded media content with streaming support.
        /// </summary>
        public async Task<MediaStorageResult> StoreBase64Async(string base64Content, MediaMetadata metadata, IProgress<long>? progress = null)
        {
            try
            {
                using var base64Stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(base64Content));
                using var decodedStream = new CryptoStream(base64Stream, new FromBase64Transform(), CryptoStreamMode.Read);
                
                return await StoreAsync(decodedStream, metadata, progress);
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Invalid base64 format in content");
                throw new InvalidOperationException("The provided content is not valid base64 encoded data", ex);
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "Failed to decode base64 content");
                throw new InvalidOperationException("Failed to decode base64 content", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<Stream?> GetStreamAsync(string storageKey)
        {
            try
            {
                var getRequest = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = storageKey
                };

                var response = await _s3Client.GetObjectAsync(getRequest);
                return response.ResponseStream;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Media with key {StorageKey} not found", storageKey);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve media {StorageKey} from S3", storageKey);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<MediaInfo?> GetInfoAsync(string storageKey)
        {
            try
            {
                var metadataRequest = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = storageKey
                };

                var response = await _s3Client.GetObjectMetadataAsync(metadataRequest);

                var mediaType = MediaType.Other;
                
                // AWS SDK prefixes custom metadata with "x-amz-meta-" when returned
                if (response.Metadata.Keys.Contains("x-amz-meta-media-type"))
                {
                    Enum.TryParse<MediaType>(response.Metadata["x-amz-meta-media-type"], true, out mediaType);
                }
                else if (response.Metadata.Keys.Contains("media-type"))
                {
                    // Fallback for direct key access (might be used in some scenarios)
                    Enum.TryParse<MediaType>(response.Metadata["media-type"], true, out mediaType);
                }

                var customMetadata = new Dictionary<string, string>();
                // AWS SDK prefixes custom metadata with "x-amz-meta-"
                foreach (var key in response.Metadata.Keys.Where(k => k.StartsWith("x-amz-meta-custom-")))
                {
                    // Remove "x-amz-meta-custom-" prefix to get the original key
                    customMetadata[key.Substring(18)] = response.Metadata[key];
                }
                // Fallback for direct custom- keys (might be used in some scenarios)
                foreach (var key in response.Metadata.Keys.Where(k => k.StartsWith("custom-")))
                {
                    customMetadata[key.Substring(7)] = response.Metadata[key];
                }

                DateTime? expiresAt = null;
                if (response.Metadata.Keys.Contains("x-amz-meta-expires-at"))
                {
                    if (DateTime.TryParse(response.Metadata["x-amz-meta-expires-at"], out var expires))
                    {
                        expiresAt = expires;
                    }
                }
                else if (response.Metadata.Keys.Contains("expires-at"))
                {
                    if (DateTime.TryParse(response.Metadata["expires-at"], out var expires))
                    {
                        expiresAt = expires;
                    }
                }

                string fileName = "";
                if (response.Metadata.Keys.Contains("x-amz-meta-original-filename"))
                {
                    fileName = response.Metadata["x-amz-meta-original-filename"];
                }
                else if (response.Metadata.Keys.Contains("original-filename"))
                {
                    fileName = response.Metadata["original-filename"];
                }

                return new MediaInfo
                {
                    StorageKey = storageKey,
                    ContentType = response.Headers.ContentType,
                    SizeBytes = response.ContentLength,
                    FileName = fileName,
                    MediaType = mediaType,
                    CreatedAt = response.LastModified ?? DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    CustomMetadata = customMetadata
                };
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get media info for {StorageKey}", storageKey);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string storageKey)
        {
            if (string.IsNullOrWhiteSpace(storageKey))
                return false;

            var result = await DeleteManyAsync([storageKey]);
            return result.Items.SingleOrDefault()?.Deleted == true;
        }

        /// <inheritdoc/>
        public async Task<MediaBulkDeleteResult> DeleteManyAsync(
            IEnumerable<string> storageKeys,
            CancellationToken cancellationToken = default)
        {
            var keys = storageKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var results = new List<MediaDeleteItemResult>(keys.Count);

            foreach (var chunk in keys.Chunk(1000))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var request = new DeleteObjectsRequest
                    {
                        BucketName = _bucketName,
                        Objects = chunk
                            .Select(key => new KeyVersion { Key = key })
                            .ToList(),
                        Quiet = false
                    };
                    var response = await _s3Client.DeleteObjectsAsync(
                        request,
                        cancellationToken);
                    var errors = (response.DeleteErrors ?? [])
                        .ToDictionary(error => error.Key, StringComparer.Ordinal);
                    var confirmed = (response.DeletedObjects ?? [])
                        .Select(item => item.Key)
                        .ToHashSet(StringComparer.Ordinal);

                    foreach (var key in chunk)
                    {
                        if (errors.TryGetValue(key, out var error))
                        {
                            results.Add(new MediaDeleteItemResult
                            {
                                StorageKey = key,
                                IsRetryable = IsRetryableDeleteError(error.Code),
                                ErrorCode = error.Code,
                                ErrorMessage = error.Message
                            });
                        }
                        else
                        {
                            results.Add(new MediaDeleteItemResult
                            {
                                StorageKey = key,
                                Deleted = confirmed.Contains(key),
                                ErrorCode = confirmed.Contains(key) ? null : "unconfirmed",
                                ErrorMessage = confirmed.Contains(key)
                                    ? null
                                    : "Storage did not confirm deletion"
                            });
                        }
                    }
                }
                catch (AmazonS3Exception ex) when (
                    ex.StatusCode is System.Net.HttpStatusCode.TooManyRequests or
                        System.Net.HttpStatusCode.ServiceUnavailable ||
                    IsRetryableDeleteError(ex.ErrorCode))
                {
                    _logger.LogWarning(
                        ex,
                        "Storage throttled bulk deletion of {Count} media objects",
                        chunk.Length);
                    var pendingKeys = keys
                        .Skip(results.Count)
                        .ToList();
                    results.AddRange(pendingKeys.Select(key => new MediaDeleteItemResult
                    {
                        StorageKey = key,
                        IsRetryable = true,
                        ErrorCode = ex.ErrorCode ?? $"http_{(int)ex.StatusCode}",
                        ErrorMessage = ex.Message
                    }));
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to bulk delete {Count} media objects",
                        chunk.Length);
                    results.AddRange(chunk.Select(key => new MediaDeleteItemResult
                    {
                        StorageKey = key,
                        ErrorCode = ex.GetType().Name,
                        ErrorMessage = ex.Message
                    }));
                }
            }

            var deletedCount = results.Count(item => item.Deleted);
            _logger.LogInformation(
                "Bulk media delete confirmed {DeletedCount} of {RequestedCount} objects",
                deletedCount,
                keys.Count);
            return new MediaBulkDeleteResult { Items = results };
        }

        private static bool IsRetryableDeleteError(string? errorCode) =>
            errorCode is not null &&
            (errorCode.Equals("SlowDown", StringComparison.OrdinalIgnoreCase) ||
             errorCode.Equals("Throttling", StringComparison.OrdinalIgnoreCase) ||
             errorCode.Equals("ThrottlingException", StringComparison.OrdinalIgnoreCase) ||
             errorCode.Equals("ServiceUnavailable", StringComparison.OrdinalIgnoreCase) ||
             errorCode.Equals("InternalError", StringComparison.OrdinalIgnoreCase));

        /// <inheritdoc/>
        public async Task<string> GenerateUrlAsync(string storageKey, TimeSpan? expiration = null)
        {
            try
            {
                // If we have a public base URL configured, use it for public access
                if (!string.IsNullOrEmpty(_options.PublicBaseUrl))
                {
                    return $"{_options.PublicBaseUrl.TrimEnd('/')}/{storageKey}";
                }

                // Otherwise, generate a presigned URL
                var urlRequest = new GetPreSignedUrlRequest
                {
                    BucketName = _bucketName,
                    Key = storageKey,
                    Verb = HttpVerb.GET,
                    Expires = DateTime.UtcNow.Add(expiration ?? _options.DefaultUrlExpiration),
                    Protocol = Protocol.HTTP
                };

                return await _s3Client.GetPreSignedURLAsync(urlRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate URL for {StorageKey}", storageKey);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ExistsAsync(string storageKey)
        {
            try
            {
                var metadataRequest = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = storageKey
                };

                await _s3Client.GetObjectMetadataAsync(metadataRequest);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        private async Task EnsureBucketExistsAsync()
        {
            if (!_options.AutoCreateBucket)
            {
                _logger.LogInformation("AutoCreateBucket is disabled. Skipping bucket creation check.");
                // Even if we don't auto-create, we should still configure CORS if the bucket exists
                await ConfigureBucketCorsAsync();
                return;
            }

            try
            {
                _logger.LogInformation("Checking if bucket {BucketName} exists at {ServiceUrl}, IsR2: {IsR2}", 
                    _bucketName, _options.ServiceUrl ?? "default endpoint", _options.IsR2);
                
                // For R2, skip bucket existence check as it may cause signature issues
                if (_options.IsR2)
                {
                    _logger.LogInformation("Skipping bucket existence check for R2 compatibility");
                    return;
                }
                
                await _s3Client.HeadBucketAsync(new HeadBucketRequest { BucketName = _bucketName });
                _logger.LogInformation("Bucket {BucketName} exists", _bucketName);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                try
                {
                    _logger.LogInformation("Bucket {BucketName} not found. Creating new bucket...", _bucketName);
                    await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = _bucketName });
                    _logger.LogInformation("Successfully created bucket {BucketName}", _bucketName);
                }
                catch (Exception createEx)
                {
                    _logger.LogError(createEx, "Failed to create bucket {BucketName}. Error: {ErrorMessage}", _bucketName, createEx.Message);
                    throw; // Re-throw to fail fast during startup
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking bucket {BucketName} existence. Error: {ErrorMessage}", _bucketName, ex.Message);
                throw; // Re-throw to fail fast during startup
            }

            // Configure CORS after ensuring bucket exists
            await ConfigureBucketCorsAsync();
        }

        private static async Task<string> ComputeHashAsync(Stream stream)
        {
            return await Sha256Hash.LegacyStorageBase64UrlAsync(stream);
        }

        private static string GenerateStorageKey(string contentHash, MediaType mediaType, string extension)
        {
            var typeFolder = mediaType.ToString().ToLower();
            var dateFolder = DateTime.UtcNow.ToString("yyyy/MM/dd");
            return $"{typeFolder}/{dateFolder}/{contentHash}{extension}";
        }

        private static string GetExtensionFromContentType(string contentType)
        {
            return Utilities.MediaContentTypes.GetExtension(contentType) ?? "";
        }
    }
}
