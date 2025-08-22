using Microsoft.AspNetCore.SignalR;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Core.Constants;
namespace ConduitLLM.Http.Interfaces
{
    /// <summary>
    /// Implementation of image generation notification service using SignalR
    /// </summary>
    public class ImageGenerationNotificationService : IImageGenerationNotificationService
    {
        private readonly IHubContext<ImageGenerationHub> _hubContext;
        private readonly ILogger<ImageGenerationNotificationService> _logger;

        public ImageGenerationNotificationService(
            IHubContext<ImageGenerationHub> hubContext,
            ILogger<ImageGenerationNotificationService> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task NotifyImageGenerationStartedAsync(string taskId, string prompt, int numberOfImages, string size, string? style = null)
        {
            try
            {
                await _hubContext.Clients.Group(SignalRConstants.Groups.ImageTask(taskId)).SendAsync(SignalRConstants.ClientMethods.ImageGenerationStarted, new
                {
                    taskId,
                    prompt,
                    numberOfImages,
                    size,
                    style,
                    startedAt = DateTime.UtcNow
                });
                
                _logger.LogInformation(
                    "[SignalR:ImageGenerationStarted] Sent notification - TaskId: {TaskId}, Prompt: {Prompt}, NumberOfImages: {NumberOfImages}, Size: {Size}, Style: {Style}, Group: {Group}",
                    taskId, prompt.Length > 50 ? prompt.Substring(0, 50) + "..." : prompt, numberOfImages, size, style ?? "default", SignalRConstants.Groups.ImageTask(taskId));
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
                await _hubContext.Clients.Group(SignalRConstants.Groups.ImageTask(taskId)).SendAsync(SignalRConstants.ClientMethods.ImageGenerationProgress, new
                {
                    taskId,
                    progressPercentage,
                    status,
                    imagesCompleted,
                    totalImages,
                    message,
                    timestamp = DateTime.UtcNow
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
                var groupName = SignalRConstants.Groups.ImageTask(taskId);
                _logger.LogInformation("NotifyImageGenerationCompletedAsync called for task {TaskId}, sending to group {GroupName}", taskId, groupName);
                
                await _hubContext.Clients.Group(groupName).SendAsync(SignalRConstants.ClientMethods.ImageGenerationCompleted, new
                {
                    taskId,
                    imageUrls,
                    durationSeconds = duration.TotalSeconds,
                    cost,
                    completedAt = DateTime.UtcNow
                });
                
                _logger.LogInformation("Successfully sent ImageGenerationCompleted notification for task {TaskId} with {ImageCount} images to group {GroupName}", 
                    taskId, imageUrls.Length, groupName);
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
                await _hubContext.Clients.Group(SignalRConstants.Groups.ImageTask(taskId)).SendAsync(SignalRConstants.ClientMethods.ImageGenerationFailed, new
                {
                    taskId,
                    error,
                    isRetryable,
                    failedAt = DateTime.UtcNow
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
                await _hubContext.Clients.Group(SignalRConstants.Groups.ImageTask(taskId)).SendAsync(SignalRConstants.ClientMethods.ImageGenerationCancelled, new
                {
                    taskId,
                    reason,
                    cancelledAt = DateTime.UtcNow
                });
                
                _logger.LogDebug("Sent ImageGenerationCancelled notification for task {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send ImageGenerationCancelled notification for task {TaskId}", taskId);
            }
        }
    }
}