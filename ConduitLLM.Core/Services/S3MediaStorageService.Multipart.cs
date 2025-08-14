using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3.Model;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    public partial class S3MediaStorageService
    {
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
                    ContentType = metadata.ContentType
                    // Removed ServerSideEncryptionMethod for R2 compatibility
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
                    MinimumPartSize = _options.MultipartChunkSizeBytes,
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
    }
}