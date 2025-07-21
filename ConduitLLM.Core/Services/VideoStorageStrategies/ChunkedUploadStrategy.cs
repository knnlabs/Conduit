using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services.VideoStorageStrategies
{
    /// <summary>
    /// Strategy for uploading large video files in chunks using multipart upload.
    /// </summary>
    public class ChunkedUploadStrategy : IVideoStorageStrategy
    {
        private readonly ILogger<ChunkedUploadStrategy> _logger;
        private const long MinChunkedUploadSize = 50 * 1024 * 1024; // 50MB
        private const long ChunkSize = 10 * 1024 * 1024; // 10MB chunks

        public ChunkedUploadStrategy(ILogger<ChunkedUploadStrategy> logger)
        {
            _logger = logger;
        }

        public string Name => "ChunkedUpload";
        public int Priority => 50;

        public bool ShouldUse(long contentLength, VideoMediaMetadata metadata)
        {
            // Use chunked upload for large videos
            return contentLength > MinChunkedUploadSize;
        }

        public async Task<MediaStorageResult> StoreAsync(
            Stream content,
            VideoMediaMetadata metadata,
            IMediaStorageService storageService,
            Action<long>? progressCallback = null)
        {
            _logger.LogInformation("Using chunked upload strategy for video of size {Size} bytes", content.Length);
            
            // Initiate multipart upload
            var session = await storageService.InitiateMultipartUploadAsync(metadata);
            var parts = new List<PartUploadResult>();
            var buffer = new byte[ChunkSize];
            var partNumber = 1;
            var totalBytesUploaded = 0L;

            try
            {
                while (true)
                {
                    var bytesRead = await content.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        break;

                    // Upload part
                    using var partStream = new MemoryStream(buffer, 0, bytesRead);
                    var partResult = await storageService.UploadPartAsync(
                        session.SessionId, 
                        partNumber++, 
                        partStream);
                    
                    parts.Add(partResult);
                    
                    totalBytesUploaded += bytesRead;
                    progressCallback?.Invoke(totalBytesUploaded);
                    
                    _logger.LogDebug("Uploaded part {PartNumber} ({Bytes} bytes)", 
                        partResult.PartNumber, bytesRead);
                }

                // Complete multipart upload
                var result = await storageService.CompleteMultipartUploadAsync(session.SessionId, parts);
                
                _logger.LogInformation("Chunked upload completed successfully with {Parts} parts", parts.Count);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chunked upload failed, aborting session {SessionId}", session.SessionId);
                
                // Abort the multipart upload on failure
                try
                {
                    await storageService.AbortMultipartUploadAsync(session.SessionId);
                }
                catch (Exception abortEx)
                {
                    _logger.LogError(abortEx, "Failed to abort multipart upload session {SessionId}", session.SessionId);
                }
                
                throw;
            }
        }
    }
}