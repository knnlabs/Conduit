using System;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles VideoGenerationFailed events to update task status and track failures.
    /// </summary>
    public class VideoGenerationFailedHandler : IConsumer<VideoGenerationFailed>
    {
        private readonly IAsyncTaskService _asyncTaskService;
        private readonly IMemoryCache _progressCache;
        private readonly ILogger<VideoGenerationFailedHandler> _logger;
        private const string ProgressCacheKeyPrefix = "video_generation_progress_";

        public VideoGenerationFailedHandler(
            IAsyncTaskService asyncTaskService,
            IMemoryCache progressCache,
            ILogger<VideoGenerationFailedHandler> logger)
        {
            _asyncTaskService = asyncTaskService;
            _progressCache = progressCache;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<VideoGenerationFailed> context)
        {
            var message = context.Message;
            
            _logger.LogWarning("Video generation failed for request {RequestId}: {Error}", 
                message.RequestId, message.Error);

            try
            {
                // Check if task exists (only async tasks will have task records)
                var taskStatus = await _asyncTaskService.GetTaskStatusAsync(message.RequestId, context.CancellationToken);
                if (taskStatus != null)
                {
                    // This is an async task, update its status
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
                    // This is a sync task failure, just log it
                    _logger.LogInformation("Sync video generation failed (no task record) for request {RequestId}", message.RequestId);
                }
                
                // Clear progress cache for this task
                var progressCacheKey = $"{ProgressCacheKeyPrefix}{message.RequestId}";
                _progressCache.Remove(progressCacheKey);
                
                // Log failure metrics for monitoring
                LogFailureMetrics(message);
                
                // Determine if automatic retry should be attempted
                if (message.IsRetryable)
                {
                    _logger.LogInformation("Video generation failure is retryable for request {RequestId}", message.RequestId);
                    // Future: Could publish a VideoGenerationRetryRequested event here
                }
                else
                {
                    _logger.LogError("Video generation failed permanently for request {RequestId}: {Error}", 
                        message.RequestId, message.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling video generation failure for request {RequestId}", message.RequestId);
                throw; // Let MassTransit handle retry
            }
        }

        private void LogFailureMetrics(VideoGenerationFailed failure)
        {
            var metrics = new
            {
                RequestId = failure.RequestId,
                Provider = failure.Provider ?? "unknown",
                ErrorCode = failure.ErrorCode ?? "unknown",
                IsRetryable = failure.IsRetryable,
                FailedAt = failure.FailedAt,
                ErrorType = DetermineErrorType(failure.Error, failure.ErrorCode)
            };
            
            _logger.LogInformation("Video generation failure metrics: {Metrics}", metrics);
        }

        private string DetermineErrorType(string error, string? errorCode)
        {
            // Categorize errors for better monitoring and alerting
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