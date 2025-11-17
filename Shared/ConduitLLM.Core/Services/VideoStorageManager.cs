using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Manages video storage by selecting and applying the appropriate storage strategy.
    /// </summary>
    public class VideoStorageManager
    {
        private readonly IEnumerable<IVideoStorageStrategy> _strategies;
        private readonly IMediaStorageService _storageService;
        private readonly ILogger<VideoStorageManager> _logger;

        public VideoStorageManager(
            IEnumerable<IVideoStorageStrategy> strategies,
            IMediaStorageService storageService,
            ILogger<VideoStorageManager> logger)
        {
            _strategies = strategies?.OrderByDescending(s => s.Priority) ?? 
                throw new ArgumentNullException(nameof(strategies));
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Stores a video using the most appropriate strategy.
        /// </summary>
        /// <param name="content">The video content stream.</param>
        /// <param name="metadata">Video metadata.</param>
        /// <param name="progressCallback">Optional progress callback.</param>
        /// <returns>Storage result.</returns>
        public async Task<MediaStorageResult> StoreVideoAsync(
            Stream content,
            VideoMediaMetadata metadata,
            Action<long>? progressCallback = null)
        {
            ArgumentNullException.ThrowIfNull(content);
            ArgumentNullException.ThrowIfNull(metadata);

            var contentLength = content.Length;
            
            // Find the appropriate strategy
            var strategy = _strategies.FirstOrDefault(s => s.ShouldUse(contentLength, metadata));
            
            if (strategy == null)
            {
                _logger.LogWarning("No suitable storage strategy found for video of size {Size}, using default", 
                    contentLength);
                
                // Fall back to direct storage
                return await _storageService.StoreVideoAsync(content, metadata, progressCallback);
            }

            _logger.LogInformation("Selected {Strategy} strategy for video of size {Size} bytes", 
                strategy.Name, contentLength);

            try
            {
                return await strategy.StoreAsync(content, metadata, _storageService, progressCallback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Storage strategy {Strategy} failed", strategy.Name);
                
                // Try next best strategy
                var fallbackStrategy = _strategies
                    .Where(s => s.Priority < strategy.Priority && s.ShouldUse(contentLength, metadata))
                    .FirstOrDefault();
                
                if (fallbackStrategy != null)
                {
                    _logger.LogInformation("Falling back to {Strategy} strategy", fallbackStrategy.Name);
                    return await fallbackStrategy.StoreAsync(content, metadata, _storageService, progressCallback);
                }
                
                throw;
            }
        }

        /// <summary>
        /// Gets storage recommendations for a given video size.
        /// </summary>
        /// <param name="estimatedSize">Estimated size in bytes.</param>
        /// <returns>Recommended strategy name.</returns>
        public string GetRecommendedStrategy(long estimatedSize)
        {
            var metadata = new VideoMediaMetadata();
            var strategy = _strategies.FirstOrDefault(s => s.ShouldUse(estimatedSize, metadata));
            return strategy?.Name ?? "Default";
        }

        /// <summary>
        /// Gets all available storage strategies.
        /// </summary>
        /// <returns>List of strategy names and their priorities.</returns>
        public IEnumerable<(string Name, int Priority)> GetAvailableStrategies()
        {
            return _strategies.Select(s => (s.Name, s.Priority)).OrderByDescending(s => s.Priority);
        }
    }
}