using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Defines strategies for storing video content based on size and requirements.
    /// </summary>
    public interface IVideoStorageStrategy
    {
        /// <summary>
        /// Determines if this strategy should be used for the given video.
        /// </summary>
        /// <param name="contentLength">The size of the video in bytes.</param>
        /// <param name="metadata">Video metadata.</param>
        /// <returns>True if this strategy should be used.</returns>
        bool ShouldUse(long contentLength, VideoMediaMetadata metadata);

        /// <summary>
        /// Stores the video using this strategy.
        /// </summary>
        /// <param name="content">The video content stream.</param>
        /// <param name="metadata">Video metadata.</param>
        /// <param name="storageService">The storage service to use.</param>
        /// <param name="progressCallback">Optional progress callback.</param>
        /// <returns>Storage result.</returns>
        Task<MediaStorageResult> StoreAsync(
            Stream content,
            VideoMediaMetadata metadata,
            IMediaStorageService storageService,
            Action<long>? progressCallback = null);

        /// <summary>
        /// Gets the priority of this strategy (higher priority strategies are evaluated first).
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Gets the name of this strategy.
        /// </summary>
        string Name { get; }
    }
}