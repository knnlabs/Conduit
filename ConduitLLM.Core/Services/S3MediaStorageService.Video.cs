using Amazon.S3;
using Amazon.S3.Model;

using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    public partial class S3MediaStorageService
    {
        /// <inheritdoc/>
        public async Task<MediaStorageResult> StoreVideoAsync(
            Stream content, 
            VideoMediaMetadata metadata,
            Action<long>? progressCallback = null)
        {
            try
            {
                // For large videos, we might want to use multipart upload
                if (content.CanSeek && content.Length > 100 * 1024 * 1024) // 100MB
                {
                    return await StoreVideoMultipartAsync(content, metadata, progressCallback);
                }

                // Convert Action<long> callback to IProgress<long> if needed
                IProgress<long>? progress = progressCallback != null 
                    ? new Progress<long>(progressCallback) 
                    : null;
                
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

                return await StoreAsync(content, baseMetadata, progress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store video");
                throw;
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
                // Note: Server-side encryption removed for MinIO compatibility
                
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
    }
}