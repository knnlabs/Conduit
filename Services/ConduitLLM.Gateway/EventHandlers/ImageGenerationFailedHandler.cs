using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;

using ConduitLLM.Gateway.Interfaces;
namespace ConduitLLM.Gateway.EventHandlers
{
    /// <summary>
    /// Handles ImageGenerationFailed events to track failure metrics, analyze error patterns, and send notifications.
    /// </summary>
    public class ImageGenerationFailedHandler : IEventHandler<ImageGenerationFailed>
    {
        private readonly IAsyncTaskService _asyncTaskService;
        private readonly IMemoryCache _progressCache;
        private readonly IImageGenerationNotificationService _notificationService;
        private readonly ILogger<ImageGenerationFailedHandler> _logger;
        private const string FailureCountCacheKeyPrefix = "image_generation_failures_";

        public ImageGenerationFailedHandler(
            IAsyncTaskService asyncTaskService,
            IMemoryCache progressCache,
            IImageGenerationNotificationService notificationService,
            ILogger<ImageGenerationFailedHandler> logger)
        {
            _asyncTaskService = asyncTaskService;
            _progressCache = progressCache;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task HandleAsync(ImageGenerationFailed message, IEventContext context)
        {
            _logger.LogError("Image generation failed for task {TaskId}: {Error} (Provider: {Provider}, Retryable: {IsRetryable}, Attempt: {AttemptCount})",
                message.TaskId, message.Error, message.Provider, message.IsRetryable, message.AttemptCount);

            try
            {
                // Update task status to failed (if async task record exists)
                var taskStatus = await _asyncTaskService.GetTaskStatusAsync(message.TaskId, context.CancellationToken);
                if (taskStatus != null)
                {
                    var errorDetails = new
                    {
                        Error = message.Error,
                        ErrorCode = message.ErrorCode,
                        Provider = message.Provider,
                        IsRetryable = message.IsRetryable,
                        AttemptCount = message.AttemptCount
                    };

                    await _asyncTaskService.UpdateTaskStatusAsync(
                        message.TaskId,
                        TaskState.Failed,
                        progress: null,
                        errorDetails,
                        message.Error,
                        context.CancellationToken);
                }

                // Clear progress cache for failed task
                var progressCacheKey = CacheKeys.MediaProgress.ImageProgress(message.TaskId);
                _progressCache.Remove(progressCacheKey);

                // Track per-provider failure count
                MediaGenerationHandlerHelper.TrackFailureMetrics(
                    _progressCache, FailureCountCacheKeyPrefix, message.Provider, "image", _logger);

                // Analyze error patterns for actionable diagnostics
                MediaGenerationHandlerHelper.AnalyzeErrorPattern(
                    message.Error, message.TaskId, _logger,
                    ImageSpecificErrorPatterns);

                // Send failure notification to WebAdmin
                await _notificationService.NotifyImageGenerationFailedAsync(
                    message.TaskId,
                    message.Error,
                    message.IsRetryable);

                // Log permanent failure
                if (!message.IsRetryable)
                {
                    _logger.LogError("Image generation permanently failed for task {TaskId} after {AttemptCount} attempts. Error: {Error}",
                        message.TaskId, message.AttemptCount, message.Error);
                }

                // Flag critical failures (auth, account, credits) for immediate attention
                if (MediaGenerationHandlerHelper.IsCriticalFailure(message.Error))
                {
                    _logger.LogCritical("Critical image generation failure detected for provider {Provider}: {Error}",
                        message.Provider, message.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image generation failure for task {TaskId}", message.TaskId);
                throw; // Let the endpoint retry policy handle it
            }
        }

        /// <summary>
        /// Image-specific error patterns beyond the common set.
        /// </summary>
        private static readonly Dictionary<string, string> ImageSpecificErrorPatterns = new()
        {
            ["invalid size"] = "Requested image size is not supported"
        };
    }
}
