using ConduitLLM.Configuration.Constants;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;

using ConduitLLM.Gateway.Interfaces;
namespace ConduitLLM.Gateway.EventHandlers
{
    /// <summary>
    /// Handles ImageGenerationFailed events to track failure metrics, analyze error patterns, and send notifications.
    /// </summary>
    public class ImageGenerationFailedHandler : IConsumer<ImageGenerationFailed>
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

        public async Task Consume(ConsumeContext<ImageGenerationFailed> context)
        {
            var message = context.Message;

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
                TrackFailureMetrics(message);

                // Analyze error patterns for actionable diagnostics
                AnalyzeErrorPattern(message);

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
                if (IsCriticalFailure(message))
                {
                    _logger.LogCritical("Critical image generation failure detected for provider {Provider}: {Error}",
                        message.Provider, message.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image generation failure for task {TaskId}", message.TaskId);
                throw; // Let MassTransit handle retry
            }
        }

        private void TrackFailureMetrics(ImageGenerationFailed message)
        {
            // Track failure count by provider in a 1-hour sliding window
            var failureCacheKey = $"{FailureCountCacheKeyPrefix}{message.Provider}";
            var failureCount = 0;

            if (_progressCache.TryGetValue<int>(failureCacheKey, out var existingCount))
            {
                failureCount = existingCount;
            }

            failureCount++;
            _progressCache.Set(failureCacheKey, failureCount, TimeSpan.FromHours(1));

            _logger.LogWarning("Provider {Provider} image failure count in last hour: {FailureCount}",
                message.Provider, failureCount);
        }

        private void AnalyzeErrorPattern(ImageGenerationFailed message)
        {
            var errorPatterns = new Dictionary<string, string>
            {
                ["rate limit"] = "Provider rate limit exceeded - consider implementing backoff",
                ["timeout"] = "Request timeout - provider may be experiencing high load",
                ["invalid api key"] = "Authentication failure - check provider credentials",
                ["insufficient credits"] = "Provider account has insufficient credits",
                ["content policy"] = "Content violates provider's usage policy",
                ["model not found"] = "Requested model is not available",
                ["invalid size"] = "Requested image size is not supported"
            };

            var lowerError = message.Error.ToLowerInvariant();
            foreach (var (pattern, analysis) in errorPatterns)
            {
                if (lowerError.Contains(pattern))
                {
                    _logger.LogWarning("Error pattern detected for task {TaskId}: {Analysis}",
                        message.TaskId, analysis);
                    break;
                }
            }
        }

        private static bool IsCriticalFailure(ImageGenerationFailed message)
        {
            var criticalErrorPatterns = new[]
            {
                "invalid api key",
                "authentication failed",
                "unauthorized",
                "forbidden",
                "account suspended",
                "insufficient credits"
            };

            var lowerError = message.Error.ToLowerInvariant();
            return criticalErrorPatterns.Any(pattern => lowerError.Contains(pattern));
        }
    }
}
