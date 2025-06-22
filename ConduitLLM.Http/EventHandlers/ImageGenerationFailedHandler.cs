using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles ImageGenerationFailed events to log failures, implement retry logic, and clean up resources.
    /// </summary>
    public class ImageGenerationFailedHandler : IConsumer<ImageGenerationFailed>
    {
        private readonly IMemoryCache _progressCache;
        private readonly IMediaStorageService _storageService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<ImageGenerationFailedHandler> _logger;
        private const string ProgressCacheKeyPrefix = "image_generation_progress_";
        private const string FailureCountCacheKeyPrefix = "image_generation_failures_";
        private const int MaxRetryAttempts = 3;

        public ImageGenerationFailedHandler(
            IMemoryCache progressCache,
            IMediaStorageService storageService,
            IPublishEndpoint publishEndpoint,
            ILogger<ImageGenerationFailedHandler> logger)
        {
            _progressCache = progressCache;
            _storageService = storageService;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<ImageGenerationFailed> context)
        {
            var message = context.Message;
            
            _logger.LogError("Image generation failed for task {TaskId}: {Error} (Provider: {Provider}, Retryable: {IsRetryable}, Attempt: {AttemptCount})", 
                message.TaskId, message.Error, message.Provider, message.IsRetryable, message.AttemptCount);

            try
            {
                // Clear progress cache for failed task
                var progressCacheKey = $"{ProgressCacheKeyPrefix}{message.TaskId}";
                _progressCache.Remove(progressCacheKey);
                
                // Track failure metrics
                await TrackFailureMetrics(message);
                
                // Implement retry logic for retryable errors
                if (message.IsRetryable && message.AttemptCount < MaxRetryAttempts)
                {
                    _logger.LogInformation("Scheduling retry for task {TaskId} (attempt {NextAttempt} of {MaxAttempts})",
                        message.TaskId, message.AttemptCount + 1, MaxRetryAttempts);
                    
                    // Future: Re-queue the image generation request with increased attempt count
                    // await _publishEndpoint.Publish(new ImageGenerationRequested
                    // {
                    //     TaskId = message.TaskId,
                    //     VirtualKeyId = message.VirtualKeyId,
                    //     // ... copy original request details ...
                    //     AttemptCount = message.AttemptCount + 1
                    // }, context => context.Delay = TimeSpan.FromSeconds(Math.Pow(2, message.AttemptCount)));
                    
                    // For now, just log the retry intention
                    _logger.LogWarning("Retry mechanism not yet implemented - task {TaskId} will not be retried automatically", 
                        message.TaskId);
                }
                else
                {
                    // Final failure - clean up any partial resources
                    await CleanupPartialResources(message);
                    
                    // Log final failure details
                    _logger.LogError("Image generation permanently failed for task {TaskId} after {AttemptCount} attempts. Error: {Error}",
                        message.TaskId, message.AttemptCount, message.Error);
                }
                
                // Analyze error patterns for common issues
                AnalyzeErrorPattern(message);
                
                // Future: Send failure notification to WebUI
                // await _hubContext.Clients.Group($"task_{message.TaskId}").SendAsync("ImageGenerationFailed", new
                // {
                //     TaskId = message.TaskId,
                //     Error = message.Error,
                //     ErrorCode = message.ErrorCode,
                //     IsRetryable = message.IsRetryable
                // });
                
                // Future: Send alert for critical failures
                if (IsCriticalFailure(message))
                {
                    _logger.LogCritical("Critical image generation failure detected for provider {Provider}: {Error}",
                        message.Provider, message.Error);
                    // await _alertService.SendCriticalFailureAlert(message);
                }
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image generation failure for task {TaskId}", message.TaskId);
                throw; // Let MassTransit handle retry
            }

            await Task.CompletedTask;
        }

        private async Task TrackFailureMetrics(ImageGenerationFailed message)
        {
            // Track failure count by provider
            var failureCacheKey = $"{FailureCountCacheKeyPrefix}{message.Provider}";
            var failureCount = 0;
            
            if (_progressCache.TryGetValue<int>(failureCacheKey, out var existingCount))
            {
                failureCount = existingCount;
            }
            
            failureCount++;
            
            // Cache failure count for 1 hour sliding window
            _progressCache.Set(failureCacheKey, failureCount, TimeSpan.FromHours(1));
            
            // Log metrics
            _logger.LogWarning("Provider {Provider} failure count in last hour: {FailureCount}", 
                message.Provider, failureCount);
            
            await Task.CompletedTask;
        }

        private async Task CleanupPartialResources(ImageGenerationFailed message)
        {
            try
            {
                // Clean up any partial uploads or temporary files
                _logger.LogInformation("Cleaning up partial resources for failed task {TaskId}", message.TaskId);
                
                // Future: Implement actual cleanup logic
                // - Check for partial uploads in storage
                // - Remove temporary files
                // - Clean up any reserved resources
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up resources for failed task {TaskId}", message.TaskId);
                // Don't throw - cleanup failures shouldn't prevent event processing
            }
        }

        private void AnalyzeErrorPattern(ImageGenerationFailed message)
        {
            // Analyze common error patterns
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

        private bool IsCriticalFailure(ImageGenerationFailed message)
        {
            // Determine if this is a critical failure requiring immediate attention
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