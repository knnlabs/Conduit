using System;
using System.IO;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services.VideoStorageStrategies
{
    /// <summary>
    /// Strategy for generating presigned URLs for direct client uploads.
    /// This is useful for very large files or when you want to offload upload bandwidth.
    /// </summary>
    public class PresignedUrlStrategy : IVideoStorageStrategy
    {
        private readonly ILogger<PresignedUrlStrategy> _logger;
        private const long MinPresignedUploadSize = 100 * 1024 * 1024; // 100MB

        public PresignedUrlStrategy(ILogger<PresignedUrlStrategy> logger)
        {
            _logger = logger;
        }

        public string Name => "PresignedUrl";
        public int Priority => 10; // Lower priority, used for very large files

        public bool ShouldUse(long contentLength, VideoMediaMetadata metadata)
        {
            // Use presigned URL for very large videos or when explicitly requested
            return contentLength > MinPresignedUploadSize || 
                   metadata.CustomMetadata.ContainsKey("use_presigned_url");
        }

        public async Task<MediaStorageResult> StoreAsync(
            Stream content,
            VideoMediaMetadata metadata,
            IMediaStorageService storageService,
            Action<long>? progressCallback = null)
        {
            _logger.LogInformation("Generating presigned URL for video upload");
            
            // Generate presigned URL
            var presignedUrl = await storageService.GeneratePresignedUploadUrlAsync(
                metadata, 
                TimeSpan.FromHours(1)); // 1 hour expiration
            
            // In a real implementation, you would return the presigned URL to the client
            // and they would upload directly. For this implementation, we'll simulate
            // by returning a placeholder result.
            
            _logger.LogInformation("Generated presigned URL: {Url}", presignedUrl.Url);
            
            // Return a result indicating the presigned URL was generated
            // The actual upload would happen client-side
            return new MediaStorageResult
            {
                StorageKey = presignedUrl.StorageKey,
                Url = presignedUrl.Url,
                SizeBytes = content.Length,
                ContentHash = "pending", // Will be calculated after client upload
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}