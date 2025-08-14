using System;
using System.Threading.Tasks;

namespace ConduitLLM.Http.Interfaces
{
    /// <summary>
    /// Unified interface for content generation notifications.
    /// Handles both image and video generation notifications through a single service.
    /// </summary>
    public interface IContentGenerationNotificationService
    {
        // Image Generation Events
        
        /// <summary>
        /// Notifies that an image generation task has started.
        /// </summary>
        Task NotifyImageGenerationStartedAsync(string taskId, string prompt, int numberOfImages, string size, string? style = null);

        /// <summary>
        /// Notifies progress update for an image generation task.
        /// </summary>
        Task NotifyImageGenerationProgressAsync(string taskId, int progressPercentage, string status, int imagesCompleted, int totalImages, string? message = null);

        /// <summary>
        /// Notifies that an image generation task has completed successfully.
        /// </summary>
        Task NotifyImageGenerationCompletedAsync(string taskId, string[] imageUrls, TimeSpan duration, decimal cost);

        /// <summary>
        /// Notifies that an image generation task has failed.
        /// </summary>
        Task NotifyImageGenerationFailedAsync(string taskId, string error, bool isRetryable);

        /// <summary>
        /// Notifies that an image generation task has been cancelled.
        /// </summary>
        Task NotifyImageGenerationCancelledAsync(string taskId, string? reason);

        // Video Generation Events
        
        /// <summary>
        /// Notifies that a video generation task has started.
        /// </summary>
        Task NotifyVideoGenerationStartedAsync(string taskId, string provider, DateTime startedAt, int? estimatedSeconds);

        /// <summary>
        /// Notifies progress update for a video generation task.
        /// </summary>
        Task NotifyVideoGenerationProgressAsync(string taskId, int progressPercentage, string status, string? message = null);

        /// <summary>
        /// Notifies that a video generation task has completed successfully.
        /// </summary>
        Task NotifyVideoGenerationCompletedAsync(string taskId, string videoUrl, TimeSpan duration, decimal cost);

        /// <summary>
        /// Notifies that a video generation task has failed.
        /// </summary>
        Task NotifyVideoGenerationFailedAsync(string taskId, string error, bool isRetryable);

        /// <summary>
        /// Notifies that a video generation task has been cancelled.
        /// </summary>
        Task NotifyVideoGenerationCancelledAsync(string taskId, string? reason);
    }
}