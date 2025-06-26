using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
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
    public class S3MediaStorageService : IMediaStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly S3StorageOptions _options;
        private readonly ILogger<S3MediaStorageService> _logger;
        private readonly string _bucketName;
        private readonly ConcurrentDictionary<string, InitiateMultipartUploadResponse> _multipartUploads = new();

        public S3MediaStorageService(
            IOptions<S3StorageOptions> options,
            ILogger<S3MediaStorageService> logger)
        {
            _options = options.Value;
            _logger = logger;
            
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
            _logger.LogInformation("S3MediaStorageService initialized with bucket: {BucketName}, ServiceUrl: {ServiceUrl}, Region: {Region}", 
                _bucketName, _options.ServiceUrl ?? "default", _options.Region);

            var config = new AmazonS3Config
            {
                ForcePathStyle = _options.ForcePathStyle,
                UseHttp = false
            };

            if (!string.IsNullOrEmpty(_options.ServiceUrl))
            {
                config.ServiceURL = _options.ServiceUrl;
            }
            else if (!string.IsNullOrEmpty(_options.Region))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(_options.Region);
            }

            _s3Client = new AmazonS3Client(_options.AccessKey, _options.SecretKey, config);

            // Initialize bucket if needed
            Task.Run(async () => await EnsureBucketExistsAsync());
        }

        /// <inheritdoc/>
        public async Task<MediaStorageResult> StoreAsync(Stream content, MediaMetadata metadata)
        {
            try
            {
                // Generate storage key based on content hash
                var contentHash = await ComputeHashAsync(content);
                var extension = GetExtensionFromContentType(metadata.ContentType);
                var storageKey = GenerateStorageKey(contentHash, metadata.MediaType, extension);

                // Reset stream position after hashing
                content.Position = 0;

                // Upload to S3
                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = storageKey,
                    InputStream = content,
                    ContentType = metadata.ContentType,
                    ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
                };

                // Add metadata
                putRequest.Metadata.Add("content-type", metadata.ContentType);
                putRequest.Metadata.Add("media-type", metadata.MediaType.ToString());
                putRequest.Metadata.Add("original-filename", metadata.FileName ?? "");
                putRequest.Metadata.Add("created-by", metadata.CreatedBy ?? "");

                // Add custom metadata
                foreach (var (key, value) in metadata.CustomMetadata ?? new Dictionary<string, string>())
                {
                    putRequest.Metadata.Add($"custom-{key}", value);
                }

                if (metadata.ExpiresAt.HasValue)
                {
                    putRequest.Metadata.Add("expires-at", metadata.ExpiresAt.Value.ToString("O"));
                }

                var response = await _s3Client.PutObjectAsync(putRequest);

                _logger.LogInformation("Stored media with key {StorageKey} to S3", storageKey);

                // Generate public URL
                var url = await GenerateUrlAsync(storageKey, _options.DefaultUrlExpiration);

                return new MediaStorageResult
                {
                    StorageKey = storageKey,
                    Url = url,
                    SizeBytes = content.Length,
                    ContentHash = contentHash,
                    CreatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store media to S3");
                throw;
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
                if (response.Metadata.Keys.Contains("media-type"))
                {
                    Enum.TryParse<MediaType>(response.Metadata["media-type"], out mediaType);
                }

                var customMetadata = new Dictionary<string, string>();
                foreach (var key in response.Metadata.Keys.Where(k => k.StartsWith("custom-")))
                {
                    customMetadata[key.Substring(7)] = response.Metadata[key];
                }

                DateTime? expiresAt = null;
                if (response.Metadata.Keys.Contains("expires-at"))
                {
                    if (DateTime.TryParse(response.Metadata["expires-at"], out var expires))
                    {
                        expiresAt = expires;
                    }
                }

                return new MediaInfo
                {
                    StorageKey = storageKey,
                    ContentType = response.Headers.ContentType,
                    SizeBytes = response.ContentLength,
                    FileName = response.Metadata.Keys.Contains("original-filename") 
                        ? response.Metadata["original-filename"] 
                        : "",
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
            try
            {
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = storageKey
                };

                await _s3Client.DeleteObjectAsync(deleteRequest);
                _logger.LogInformation("Deleted media with key {StorageKey}", storageKey);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete media {StorageKey}", storageKey);
                return false;
            }
        }

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
                    Protocol = Protocol.HTTPS
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
                return;

            try
            {
                await _s3Client.HeadBucketAsync(new HeadBucketRequest { BucketName = _bucketName });
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                try
                {
                    _logger.LogInformation("Creating S3 bucket {BucketName}", _bucketName);
                    await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = _bucketName });
                }
                catch (Exception createEx)
                {
                    _logger.LogError(createEx, "Failed to create bucket {BucketName}", _bucketName);
                }
            }
        }

        private static async Task<string> ComputeHashAsync(Stream stream)
        {
            using var sha256 = SHA256.Create();
            var hash = await sha256.ComputeHashAsync(stream);
            return Convert.ToBase64String(hash).Replace("/", "-").Replace("+", "_").TrimEnd('=');
        }

        private static string GenerateStorageKey(string contentHash, MediaType mediaType, string extension)
        {
            var typeFolder = mediaType.ToString().ToLower();
            var dateFolder = DateTime.UtcNow.ToString("yyyy/MM/dd");
            return $"{typeFolder}/{dateFolder}/{contentHash}{extension}";
        }

        private static string GetExtensionFromContentType(string contentType)
        {
            return contentType?.ToLower() switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "video/mp4" => ".mp4",
                "video/webm" => ".webm",
                "audio/mpeg" => ".mp3",
                "audio/wav" => ".wav",
                "audio/webm" => ".weba",
                _ => ""
            };
        }

        /// <inheritdoc/>
        public async Task<MediaStorageResult> StoreVideoAsync(
            Stream content, 
            VideoMediaMetadata metadata,
            Action<long>? progressCallback = null)
        {
            try
            {
                // For large videos, we might want to use multipart upload
                if (content.Length > 100 * 1024 * 1024) // 100MB
                {
                    return await StoreVideoMultipartAsync(content, metadata, progressCallback);
                }

                // For smaller videos, use regular upload with progress tracking
                var progressStream = new ProgressStream(content, progressCallback);
                
                // Set metadata with video-specific information
                var baseMetadata = new MediaMetadata
                {
                    ContentType = metadata.ContentType,
                    FileName = metadata.FileName,
                    MediaType = MediaType.Video,
                    CustomMetadata = metadata.CustomMetadata,
                    CreatedBy = metadata.CreatedBy,
                    ExpiresAt = metadata.ExpiresAt
                };

                // Add video-specific metadata
                baseMetadata.CustomMetadata["duration"] = metadata.Duration.ToString();
                baseMetadata.CustomMetadata["resolution"] = metadata.Resolution;
                baseMetadata.CustomMetadata["width"] = metadata.Width.ToString();
                baseMetadata.CustomMetadata["height"] = metadata.Height.ToString();
                baseMetadata.CustomMetadata["framerate"] = metadata.FrameRate.ToString();
                
                if (!string.IsNullOrEmpty(metadata.Codec))
                    baseMetadata.CustomMetadata["codec"] = metadata.Codec;
                
                if (metadata.Bitrate.HasValue)
                    baseMetadata.CustomMetadata["bitrate"] = metadata.Bitrate.Value.ToString();
                
                if (!string.IsNullOrEmpty(metadata.GeneratedByModel))
                    baseMetadata.CustomMetadata["generated-by-model"] = metadata.GeneratedByModel;
                
                if (!string.IsNullOrEmpty(metadata.GenerationPrompt))
                    baseMetadata.CustomMetadata["generation-prompt"] = metadata.GenerationPrompt;

                return await StoreAsync(progressStream, baseMetadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store video");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<MultipartUploadSession> InitiateMultipartUploadAsync(VideoMediaMetadata metadata)
        {
            try
            {
                // Generate storage key
                var extension = GetExtensionFromContentType(metadata.ContentType);
                var storageKey = GenerateStorageKey(Guid.NewGuid().ToString(), MediaType.Video, extension);
                
                var initiateRequest = new InitiateMultipartUploadRequest
                {
                    BucketName = _bucketName,
                    Key = storageKey,
                    ContentType = metadata.ContentType,
                    ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
                };

                // Add metadata
                initiateRequest.Metadata.Add("content-type", metadata.ContentType);
                initiateRequest.Metadata.Add("media-type", MediaType.Video.ToString());
                initiateRequest.Metadata.Add("duration", metadata.Duration.ToString());
                initiateRequest.Metadata.Add("resolution", metadata.Resolution);
                
                if (!string.IsNullOrEmpty(metadata.GeneratedByModel))
                    initiateRequest.Metadata.Add("generated-by-model", metadata.GeneratedByModel);

                var response = await _s3Client.InitiateMultipartUploadAsync(initiateRequest);
                
                var session = new MultipartUploadSession
                {
                    SessionId = Guid.NewGuid().ToString(),
                    StorageKey = storageKey,
                    S3UploadId = response.UploadId,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    MinimumPartSize = 5 * 1024 * 1024, // 5MB minimum part size for S3
                    MaxParts = 10000 // S3 limit
                };

                _multipartUploads[session.SessionId] = response;
                
                _logger.LogInformation("Initiated multipart upload session {SessionId} for key {StorageKey}", 
                    session.SessionId, storageKey);
                
                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate multipart upload");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<PartUploadResult> UploadPartAsync(string sessionId, int partNumber, Stream content)
        {
            try
            {
                if (!_multipartUploads.TryGetValue(sessionId, out var uploadInfo))
                {
                    throw new InvalidOperationException($"Upload session {sessionId} not found");
                }

                var uploadRequest = new UploadPartRequest
                {
                    BucketName = _bucketName,
                    Key = uploadInfo.Key,
                    UploadId = uploadInfo.UploadId,
                    PartNumber = partNumber,
                    InputStream = content
                };

                var response = await _s3Client.UploadPartAsync(uploadRequest);
                
                _logger.LogDebug("Uploaded part {PartNumber} for session {SessionId}", partNumber, sessionId);
                
                return new PartUploadResult
                {
                    PartNumber = partNumber,
                    ETag = response.ETag,
                    SizeBytes = content.Length
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload part {PartNumber} for session {SessionId}", 
                    partNumber, sessionId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<MediaStorageResult> CompleteMultipartUploadAsync(string sessionId, List<PartUploadResult> parts)
        {
            try
            {
                if (!_multipartUploads.TryRemove(sessionId, out var uploadInfo))
                {
                    throw new InvalidOperationException($"Upload session {sessionId} not found");
                }

                var completeRequest = new CompleteMultipartUploadRequest
                {
                    BucketName = _bucketName,
                    Key = uploadInfo.Key,
                    UploadId = uploadInfo.UploadId,
                    PartETags = parts.Select(p => new PartETag(p.PartNumber, p.ETag)).ToList()
                };

                var response = await _s3Client.CompleteMultipartUploadAsync(completeRequest);
                
                _logger.LogInformation("Completed multipart upload for key {StorageKey}", uploadInfo.Key);
                
                // Generate URL
                var url = await GenerateUrlAsync(uploadInfo.Key, _options.DefaultUrlExpiration);
                
                return new MediaStorageResult
                {
                    StorageKey = uploadInfo.Key,
                    Url = url,
                    SizeBytes = parts.Sum(p => p.SizeBytes),
                    ContentHash = response.ETag,
                    CreatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete multipart upload for session {SessionId}", sessionId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task AbortMultipartUploadAsync(string sessionId)
        {
            try
            {
                if (!_multipartUploads.TryRemove(sessionId, out var uploadInfo))
                {
                    // Already removed or doesn't exist
                    return;
                }

                var abortRequest = new AbortMultipartUploadRequest
                {
                    BucketName = _bucketName,
                    Key = uploadInfo.Key,
                    UploadId = uploadInfo.UploadId
                };

                await _s3Client.AbortMultipartUploadAsync(abortRequest);
                
                _logger.LogInformation("Aborted multipart upload session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to abort multipart upload for session {SessionId}", sessionId);
            }
        }

        /// <inheritdoc/>
        public async Task<RangedStream?> GetVideoStreamAsync(string storageKey, long? rangeStart = null, long? rangeEnd = null)
        {
            try
            {
                // First get metadata to determine file size
                var metadataRequest = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = storageKey
                };

                var metadata = await _s3Client.GetObjectMetadataAsync(metadataRequest);
                var totalSize = metadata.ContentLength;

                // Setup range request
                var getRequest = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = storageKey
                };

                // Calculate actual range
                var start = rangeStart ?? 0;
                var end = rangeEnd ?? totalSize - 1;
                
                // Ensure range is valid
                start = Math.Max(0, Math.Min(start, totalSize - 1));
                end = Math.Max(start, Math.Min(end, totalSize - 1));

                if (start > 0 || end < totalSize - 1)
                {
                    getRequest.ByteRange = new ByteRange(start, end);
                }

                var response = await _s3Client.GetObjectAsync(getRequest);

                return new RangedStream
                {
                    Stream = response.ResponseStream,
                    RangeStart = start,
                    RangeEnd = end,
                    TotalSize = totalSize,
                    ContentType = response.Headers.ContentType
                };
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Video with key {StorageKey} not found", storageKey);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get video stream for {StorageKey}", storageKey);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<PresignedUploadUrl> GeneratePresignedUploadUrlAsync(VideoMediaMetadata metadata, TimeSpan expiration)
        {
            try
            {
                // Generate storage key
                var extension = GetExtensionFromContentType(metadata.ContentType);
                var storageKey = GenerateStorageKey(Guid.NewGuid().ToString(), MediaType.Video, extension);

                var presignRequest = new GetPreSignedUrlRequest
                {
                    BucketName = _bucketName,
                    Key = storageKey,
                    Verb = HttpVerb.PUT,
                    Expires = DateTime.UtcNow.Add(expiration),
                    Protocol = Protocol.HTTPS,
                    ContentType = metadata.ContentType
                };

                // Add headers that must be included in the upload
                presignRequest.Headers["x-amz-server-side-encryption"] = ServerSideEncryptionMethod.AES256.Value;
                
                // Add metadata as headers
                presignRequest.Headers["x-amz-meta-media-type"] = MediaType.Video.ToString();
                presignRequest.Headers["x-amz-meta-duration"] = metadata.Duration.ToString();
                presignRequest.Headers["x-amz-meta-resolution"] = metadata.Resolution;
                
                if (!string.IsNullOrEmpty(metadata.GeneratedByModel))
                    presignRequest.Headers["x-amz-meta-generated-by-model"] = metadata.GeneratedByModel;

                var url = await _s3Client.GetPreSignedURLAsync(presignRequest);

                return new PresignedUploadUrl
                {
                    Url = url,
                    HttpMethod = "PUT",
                    RequiredHeaders = new Dictionary<string, string>
                    {
                        ["Content-Type"] = metadata.ContentType,
                        ["x-amz-server-side-encryption"] = ServerSideEncryptionMethod.AES256.Value
                    },
                    ExpiresAt = DateTime.UtcNow.Add(expiration),
                    StorageKey = storageKey,
                    MaxFileSizeBytes = 5L * 1024 * 1024 * 1024 // 5GB max
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate presigned upload URL");
                throw;
            }
        }

        private async Task<MediaStorageResult> StoreVideoMultipartAsync(
            Stream content, 
            VideoMediaMetadata metadata,
            Action<long>? progressCallback)
        {
            var session = await InitiateMultipartUploadAsync(metadata);
            var parts = new List<PartUploadResult>();
            var buffer = new byte[session.MinimumPartSize];
            var partNumber = 1;
            var totalBytesUploaded = 0L;

            try
            {
                while (true)
                {
                    var bytesRead = await content.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        break;

                    using var partStream = new MemoryStream(buffer, 0, bytesRead);
                    var partResult = await UploadPartAsync(session.SessionId, partNumber++, partStream);
                    parts.Add(partResult);
                    
                    totalBytesUploaded += bytesRead;
                    progressCallback?.Invoke(totalBytesUploaded);
                }

                return await CompleteMultipartUploadAsync(session.SessionId, parts);
            }
            catch
            {
                await AbortMultipartUploadAsync(session.SessionId);
                throw;
            }
        }

        /// <summary>
        /// Stream wrapper that reports progress during read operations.
        /// </summary>
        private class ProgressStream : Stream
        {
            private readonly Stream _innerStream;
            private readonly Action<long>? _progressCallback;
            private long _totalBytesRead;

            public ProgressStream(Stream innerStream, Action<long>? progressCallback)
            {
                _innerStream = innerStream;
                _progressCallback = progressCallback;
                _totalBytesRead = 0;
            }

            public override bool CanRead => _innerStream.CanRead;
            public override bool CanSeek => _innerStream.CanSeek;
            public override bool CanWrite => _innerStream.CanWrite;
            public override long Length => _innerStream.Length;
            public override long Position 
            { 
                get => _innerStream.Position;
                set => _innerStream.Position = value;
            }

            public override void Flush() => _innerStream.Flush();
            
            public override int Read(byte[] buffer, int offset, int count)
            {
                var bytesRead = _innerStream.Read(buffer, offset, count);
                if (bytesRead > 0)
                {
                    _totalBytesRead += bytesRead;
                    _progressCallback?.Invoke(_totalBytesRead);
                }
                return bytesRead;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var bytesRead = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
                if (bytesRead > 0)
                {
                    _totalBytesRead += bytesRead;
                    _progressCallback?.Invoke(_totalBytesRead);
                }
                return bytesRead;
            }

            public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
            public override void SetLength(long value) => _innerStream.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);
            
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _innerStream?.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}