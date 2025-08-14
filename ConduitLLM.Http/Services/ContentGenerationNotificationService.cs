using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Http.Interfaces;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Unified implementation of content generation notification service using SignalR.
    /// Handles both image and video generation notifications through ContentGenerationHub.
    /// </summary>
    public class ContentGenerationNotificationService : IContentGenerationNotificationService
    {
        private readonly IHubContext<ContentGenerationHub> _hubContext;
        private readonly ILogger<ContentGenerationNotificationService> _logger;

        public ContentGenerationNotificationService(
            IHubContext<ContentGenerationHub> hubContext,
            ILogger<ContentGenerationNotificationService> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Image Generation Events

        public async Task NotifyImageGenerationStartedAsync(string taskId, string prompt, int numberOfImages, string size, string? style = null)
        {
            try
            {
                var notification = new
                {
                    taskId,
                    prompt,
                    numberOfImages,
                    size,
                    style,
                    startedAt = DateTime.UtcNow
                };

                // Send to image-specific group
                await _hubContext.Clients.Group($"image-{taskId}").SendAsync("ImageGenerationStarted", notification);
                
                // Also send to unified content group
                await _hubContext.Clients.Group($"content-{taskId}").SendAsync("ContentGenerationStarted", new
                {
                    taskId,
                    contentType = "image",
                    details = notification
                });
                
                _logger.LogInformation(
                    "[SignalR:ImageGenerationStarted] Sent notification - TaskId: {TaskId}, Prompt: {Prompt}, NumberOfImages: {NumberOfImages}, Size: {Size}, Style: {Style}",
                    taskId, prompt.Length > 50 ? prompt.Substring(0, 50) + "..." : prompt, numberOfImages, size, style ?? "default");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send ImageGenerationStarted notification for task {TaskId}", taskId);
            }
        }

        public async Task NotifyImageGenerationProgressAsync(string taskId, int progressPercentage, string status, int imagesCompleted, int totalImages, string? message = null)
        {
            try
            {
                var notification = new
                {
                    taskId,
                    progressPercentage,
                    status,
                    imagesCompleted,
                    totalImages,
                    message,
                    timestamp = DateTime.UtcNow
                };

                // Send to image-specific group
                await _hubContext.Clients.Group($"image-{taskId}").SendAsync("ImageGenerationProgress", notification);
                
                // Also send to unified content group
                await _hubContext.Clients.Group($"content-{taskId}").SendAsync("ContentGenerationProgress", new
                {
                    taskId,
                    contentType = "image",
                    progressPercentage,
                    details = notification
                });
                
                _logger.LogDebug("Sent ImageGenerationProgress notification for task {TaskId}: {Progress}% ({ImagesCompleted}/{TotalImages})", 
                    taskId, progressPercentage, imagesCompleted, totalImages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send ImageGenerationProgress notification for task {TaskId}", taskId);
            }
        }

        public async Task NotifyImageGenerationCompletedAsync(string taskId, string[] imageUrls, TimeSpan duration, decimal cost)
        {
            try
            {
                var notification = new
                {
                    taskId,
                    imageUrls,
                    durationSeconds = duration.TotalSeconds,
                    cost,
                    completedAt = DateTime.UtcNow
                };

                // Send to image-specific group
                await _hubContext.Clients.Group($"image-{taskId}").SendAsync("ImageGenerationCompleted", notification);
                
                // Also send to unified content group
                await _hubContext.Clients.Group($"content-{taskId}").SendAsync("ContentGenerationCompleted", new
                {
                    taskId,
                    contentType = "image",
                    details = notification
                });
                
                _logger.LogDebug("Sent ImageGenerationCompleted notification for task {TaskId} with {ImageCount} images", 
                    taskId, imageUrls.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send ImageGenerationCompleted notification for task {TaskId}", taskId);
            }
        }

        public async Task NotifyImageGenerationFailedAsync(string taskId, string error, bool isRetryable)
        {
            try
            {
                var notification = new
                {
                    taskId,
                    error,
                    isRetryable,
                    failedAt = DateTime.UtcNow
                };

                // Send to image-specific group
                await _hubContext.Clients.Group($"image-{taskId}").SendAsync("ImageGenerationFailed", notification);
                
                // Also send to unified content group
                await _hubContext.Clients.Group($"content-{taskId}").SendAsync("ContentGenerationFailed", new
                {
                    taskId,
                    contentType = "image",
                    error,
                    isRetryable,
                    details = notification
                });
                
                _logger.LogDebug("Sent ImageGenerationFailed notification for task {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send ImageGenerationFailed notification for task {TaskId}", taskId);
            }
        }

        public async Task NotifyImageGenerationCancelledAsync(string taskId, string? reason)
        {
            try
            {
                var notification = new
                {
                    taskId,
                    reason,
                    cancelledAt = DateTime.UtcNow
                };

                // Send to image-specific group
                await _hubContext.Clients.Group($"image-{taskId}").SendAsync("ImageGenerationCancelled", notification);
                
                // Also send to unified content group
                await _hubContext.Clients.Group($"content-{taskId}").SendAsync("ContentGenerationCancelled", new
                {
                    taskId,
                    contentType = "image",
                    reason,
                    details = notification
                });
                
                _logger.LogDebug("Sent ImageGenerationCancelled notification for task {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send ImageGenerationCancelled notification for task {TaskId}", taskId);
            }
        }

        // Video Generation Events

        public async Task NotifyVideoGenerationStartedAsync(string taskId, string provider, DateTime startedAt, int? estimatedSeconds)
        {
            try
            {
                var notification = new
                {
                    taskId,
                    provider,
                    startedAt,
                    estimatedSeconds
                };

                // Send to video-specific group
                await _hubContext.Clients.Group($"video-{taskId}").SendAsync("VideoGenerationStarted", notification);
                
                // Also send to unified content group
                await _hubContext.Clients.Group($"content-{taskId}").SendAsync("ContentGenerationStarted", new
                {
                    taskId,
                    contentType = "video",
                    details = notification
                });
                
                _logger.LogDebug("Sent VideoGenerationStarted notification for task {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VideoGenerationStarted notification for task {TaskId}", taskId);
            }
        }

        public async Task NotifyVideoGenerationProgressAsync(string taskId, int progressPercentage, string status, string? message = null)
        {
            try
            {
                var notification = new
                {
                    taskId,
                    progressPercentage,
                    status,
                    message,
                    timestamp = DateTime.UtcNow
                };

                // Send to video-specific group
                await _hubContext.Clients.Group($"video-{taskId}").SendAsync("VideoGenerationProgress", notification);
                
                // Also send to unified content group
                await _hubContext.Clients.Group($"content-{taskId}").SendAsync("ContentGenerationProgress", new
                {
                    taskId,
                    contentType = "video",
                    progressPercentage,
                    details = notification
                });
                
                _logger.LogDebug("Sent VideoGenerationProgress notification for task {TaskId}: {Progress}%", 
                    taskId, progressPercentage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VideoGenerationProgress notification for task {TaskId}", taskId);
            }
        }

        public async Task NotifyVideoGenerationCompletedAsync(string taskId, string videoUrl, TimeSpan duration, decimal cost)
        {
            try
            {
                var notification = new
                {
                    taskId,
                    videoUrl,
                    durationSeconds = duration.TotalSeconds,
                    cost,
                    completedAt = DateTime.UtcNow
                };

                // Send to video-specific group
                await _hubContext.Clients.Group($"video-{taskId}").SendAsync("VideoGenerationCompleted", notification);
                
                // Also send to unified content group
                await _hubContext.Clients.Group($"content-{taskId}").SendAsync("ContentGenerationCompleted", new
                {
                    taskId,
                    contentType = "video",
                    details = notification
                });
                
                _logger.LogDebug("Sent VideoGenerationCompleted notification for task {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VideoGenerationCompleted notification for task {TaskId}", taskId);
            }
        }

        public async Task NotifyVideoGenerationFailedAsync(string taskId, string error, bool isRetryable)
        {
            try
            {
                var notification = new
                {
                    taskId,
                    error,
                    isRetryable,
                    failedAt = DateTime.UtcNow
                };

                // Send to video-specific group
                await _hubContext.Clients.Group($"video-{taskId}").SendAsync("VideoGenerationFailed", notification);
                
                // Also send to unified content group
                await _hubContext.Clients.Group($"content-{taskId}").SendAsync("ContentGenerationFailed", new
                {
                    taskId,
                    contentType = "video",
                    error,
                    isRetryable,
                    details = notification
                });
                
                _logger.LogDebug("Sent VideoGenerationFailed notification for task {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VideoGenerationFailed notification for task {TaskId}", taskId);
            }
        }

        public async Task NotifyVideoGenerationCancelledAsync(string taskId, string? reason)
        {
            try
            {
                var notification = new
                {
                    taskId,
                    reason,
                    cancelledAt = DateTime.UtcNow
                };

                // Send to video-specific group
                await _hubContext.Clients.Group($"video-{taskId}").SendAsync("VideoGenerationCancelled", notification);
                
                // Also send to unified content group
                await _hubContext.Clients.Group($"content-{taskId}").SendAsync("ContentGenerationCancelled", new
                {
                    taskId,
                    contentType = "video",
                    reason,
                    details = notification
                });
                
                _logger.LogDebug("Sent VideoGenerationCancelled notification for task {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VideoGenerationCancelled notification for task {TaskId}", taskId);
            }
        }
    }
}