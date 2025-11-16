namespace ConduitLLM.Http.Interfaces
{
    /// <summary>
    /// Service for sending real-time video generation notifications
    /// </summary>
    public interface IVideoGenerationNotificationService
    {
        /// <summary>
        /// Notify clients that video generation has started
        /// </summary>
        Task NotifyVideoGenerationStartedAsync(string requestId, string provider, DateTime startedAt, int? estimatedSeconds);

        /// <summary>
        /// Notify clients of video generation progress
        /// </summary>
        Task NotifyVideoGenerationProgressAsync(string requestId, int progressPercentage, string status, string? message = null);

        /// <summary>
        /// Notify clients that video generation has completed
        /// </summary>
        Task NotifyVideoGenerationCompletedAsync(string requestId, string videoUrl, TimeSpan duration, decimal cost);

        /// <summary>
        /// Notify clients that video generation has failed
        /// </summary>
        Task NotifyVideoGenerationFailedAsync(string requestId, string error, bool isRetryable);

        /// <summary>
        /// Notify clients that video generation was cancelled
        /// </summary>
        Task NotifyVideoGenerationCancelledAsync(string requestId, string? reason);
    }
}