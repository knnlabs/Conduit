using ConduitLLM.Configuration.Constants;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Gateway.Interfaces;

using MassTransit;

using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.Gateway.EventHandlers
{
    /// <summary>
    /// Handles VideoGenerationFailed events to update task status, track failure metrics, and analyze error patterns.
    /// </summary>
    public class VideoGenerationFailedHandler : IConsumer<VideoGenerationFailed>
    {
        private readonly IAsyncTaskService _asyncTaskService;
        private readonly IMemoryCache _progressCache;
        private readonly IVideoGenerationNotificationService _notificationService;
        private readonly ILogger<VideoGenerationFailedHandler> _logger;
        private const string FailureCountCacheKeyPrefix = "video_generation_failures_";

        public VideoGenerationFailedHandler(
            IAsyncTaskService asyncTaskService,
            IMemoryCache progressCache,
            IVideoGenerationNotificationService notificationService,
            ILogger<VideoGenerationFailedHandler> logger)
        {
            _asyncTaskService = asyncTaskService;
            _progressCache = progressCache;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<VideoGenerationFailed> context)
        {
            var message = context.Message;
            var provider = message.Provider ?? "unknown";

            _logger.LogError("Video generation failed for request {RequestId}: {Error} (Provider: {Provider}, Retryable: {IsRetryable}, Retry: {RetryCount}/{MaxRetries})",
                message.RequestId, message.Error, provider, message.IsRetryable, message.RetryCount, message.MaxRetries);

            try
            {
                // Check if task exists (only async tasks will have task records)
                var taskStatus = await _asyncTaskService.GetTaskStatusAsync(message.RequestId, context.CancellationToken);
                if (taskStatus != null)
                {
                    var errorDetails = new
                    {
                        Error = message.Error,
                        ErrorCode = message.ErrorCode,
                        Provider = message.Provider,
                        IsRetryable = message.IsRetryable,
                        FailedAt = message.FailedAt
                    };

                    await _asyncTaskService.UpdateTaskStatusAsync(
                        message.RequestId,
                        TaskState.Failed,
                        progress: null,
                        errorDetails,
                        message.Error,
                        context.CancellationToken);
                }
                else
                {
                    _logger.LogInformation("Sync video generation failed (no task record) for request {RequestId}", message.RequestId);
                }

                // Clear progress cache for this task
                var progressCacheKey = CacheKeys.MediaProgress.VideoProgress(message.RequestId);
                _progressCache.Remove(progressCacheKey);

                // Track per-provider failure count and log structured metrics
                TrackFailureMetrics(message);

                // Analyze error patterns for actionable diagnostics
                AnalyzeErrorPattern(message);

                // Send failure notification via notification service
                await _notificationService.NotifyVideoGenerationFailedAsync(
                    message.RequestId,
                    message.Error,
                    message.IsRetryable,
                    errorCode: message.ErrorCode,
                    retryCount: message.RetryCount,
                    maxRetries: message.MaxRetries,
                    nextRetryAt: message.NextRetryAt,
                    failedAt: message.FailedAt);

                // Log retry state or permanent failure
                if (message.IsRetryable)
                {
                    _logger.LogInformation("Video generation failure is retryable for request {RequestId} (Retry {RetryCount}/{MaxRetries})",
                        message.RequestId, message.RetryCount, message.MaxRetries);

                    if (message.NextRetryAt.HasValue)
                    {
                        _logger.LogInformation("Video generation will be retried at {NextRetryAt} for request {RequestId}",
                            message.NextRetryAt.Value, message.RequestId);
                    }
                }
                else
                {
                    _logger.LogError("Video generation failed permanently for request {RequestId}: {Error}",
                        message.RequestId, message.Error);
                }

                // Flag critical failures (auth, account, credits) for immediate attention
                if (IsCriticalFailure(message))
                {
                    _logger.LogCritical("Critical video generation failure detected for provider {Provider}: {Error}",
                        provider, message.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling video generation failure for request {RequestId}", message.RequestId);
                throw; // Let MassTransit handle retry
            }
        }

        private void TrackFailureMetrics(VideoGenerationFailed message)
        {
            var provider = message.Provider ?? "unknown";

            // Track failure count by provider in a 1-hour sliding window
            var failureCacheKey = $"{FailureCountCacheKeyPrefix}{provider}";
            var failureCount = 0;

            if (_progressCache.TryGetValue<int>(failureCacheKey, out var existingCount))
            {
                failureCount = existingCount;
            }

            failureCount++;
            _progressCache.Set(failureCacheKey, failureCount, TimeSpan.FromHours(1));

            _logger.LogWarning("Provider {Provider} video failure count in last hour: {FailureCount}",
                provider, failureCount);

            // Log structured metrics for monitoring/alerting pipelines
            _logger.LogInformation("Video generation failure metrics: {@Metrics}", new
            {
                RequestId = message.RequestId,
                Provider = provider,
                ErrorCode = message.ErrorCode ?? "unknown",
                IsRetryable = message.IsRetryable,
                FailedAt = message.FailedAt,
                ErrorType = DetermineErrorType(message.Error, message.ErrorCode),
                ProviderFailureCount = failureCount
            });
        }

        private void AnalyzeErrorPattern(VideoGenerationFailed message)
        {
            var errorPatterns = new Dictionary<string, string>
            {
                ["rate limit"] = "Provider rate limit exceeded - consider implementing backoff",
                ["quota"] = "Provider quota exhausted - check account limits",
                ["timeout"] = "Request timeout - provider may be experiencing high load",
                ["invalid api key"] = "Authentication failure - check provider credentials",
                ["insufficient credits"] = "Provider account has insufficient credits",
                ["content policy"] = "Content violates provider's usage policy",
                ["model not found"] = "Requested model is not available",
                ["duration"] = "Requested video duration may exceed provider limits",
                ["resolution"] = "Requested video resolution may not be supported",
                ["codec"] = "Unsupported video codec or output format",
                ["format"] = "Unsupported video format requested"
            };

            var lowerError = message.Error.ToLowerInvariant();
            foreach (var (pattern, analysis) in errorPatterns)
            {
                if (lowerError.Contains(pattern))
                {
                    _logger.LogWarning("Error pattern detected for request {RequestId}: {Analysis}",
                        message.RequestId, analysis);
                    break;
                }
            }
        }

        private bool IsCriticalFailure(VideoGenerationFailed message)
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

        private static string DetermineErrorType(string error, string? errorCode)
        {
            if (string.IsNullOrEmpty(error))
                return "unknown";

            var lowerError = error.ToLowerInvariant();

            if (lowerError.Contains("rate limit") || lowerError.Contains("quota"))
                return "rate_limit";
            if (lowerError.Contains("auth") || lowerError.Contains("unauthorized"))
                return "authentication";
            if (lowerError.Contains("timeout"))
                return "timeout";
            if (lowerError.Contains("invalid") || lowerError.Contains("bad request"))
                return "validation";
            if (lowerError.Contains("not found"))
                return "not_found";
            if (lowerError.Contains("server error") || lowerError.Contains("internal"))
                return "server_error";

            return "other";
        }
    }
}