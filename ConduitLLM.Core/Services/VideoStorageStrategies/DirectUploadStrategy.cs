using System;
using System.IO;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services.VideoStorageStrategies
{
    /// <summary>
    /// Strategy for directly uploading small video files.
    /// </summary>
    public class DirectUploadStrategy : IVideoStorageStrategy
    {
        private readonly ILogger<DirectUploadStrategy> _logger;
        private const long MaxDirectUploadSize = 50 * 1024 * 1024; // 50MB

        public DirectUploadStrategy(ILogger<DirectUploadStrategy> logger)
        {
            _logger = logger;
        }

        public string Name => "DirectUpload";
        public int Priority => 100;

        public bool ShouldUse(long contentLength, VideoMediaMetadata metadata)
        {
            // Use direct upload for small videos
            return contentLength <= MaxDirectUploadSize;
        }

        public async Task<MediaStorageResult> StoreAsync(
            Stream content,
            VideoMediaMetadata metadata,
            IMediaStorageService storageService,
            Action<long>? progressCallback = null)
        {
            _logger.LogInformation("Using direct upload strategy for video of size {Size} bytes", content.Length);
            
            try
            {
                // Use the video-specific storage method with progress tracking
                return await storageService.StoreVideoAsync(content, metadata, progressCallback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Direct upload failed for video");
                throw;
            }
        }
    }
}