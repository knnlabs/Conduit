using System;
using System.Threading.Tasks;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for sending real-time image generation notifications
    /// </summary>
    public interface IImageGenerationNotificationService
    {
        /// <summary>
        /// Notify clients of image generation progress
        /// </summary>
        Task NotifyImageGenerationProgressAsync(string taskId, int progressPercentage, string status, int imagesCompleted, int totalImages, string? message = null);

        /// <summary>
        /// Notify clients that image generation has completed
        /// </summary>
        Task NotifyImageGenerationCompletedAsync(string taskId, string[] imageUrls, TimeSpan duration, decimal cost);

        /// <summary>
        /// Notify clients that image generation has failed
        /// </summary>
        Task NotifyImageGenerationFailedAsync(string taskId, string error, bool isRetryable);

        /// <summary>
        /// Notify clients that image generation was cancelled
        /// </summary>
        Task NotifyImageGenerationCancelledAsync(string taskId, string? reason);
    }
}