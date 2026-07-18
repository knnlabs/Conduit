using Microsoft.AspNetCore.SignalR;
using ConduitLLM.Gateway.Hubs;
using ConduitLLM.Gateway.Interfaces;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Services;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Implementation of image generation notification service using SignalR.
    /// Inherits from SignalRNotificationServiceBase for common functionality.
    /// </summary>
    public class ImageGenerationNotificationService
        : SignalRNotificationServiceBase<ImageGenerationHub>,
          IImageGenerationNotificationService
    {
        public ImageGenerationNotificationService(
            IHubContext<ImageGenerationHub> hubContext,
            ILogger<ImageGenerationNotificationService> logger)
            : base(hubContext, logger)
        {
        }

        public async Task NotifyImageGenerationStartedAsync(string taskId, string prompt, int numberOfImages, string size, string? style = null)
        {
            var groupName = SignalRConstants.Groups.ImageTask(taskId);

            await SendToGroupAsync(groupName, SignalRConstants.ClientMethods.ImageGenerationStarted, new
            {
                taskId,
                prompt,
                numberOfImages,
                size,
                style,
                startedAt = DateTime.UtcNow
            });

            Logger.LogInformation(
                "[SignalR:ImageGenerationStarted] Sent notification - TaskId: {TaskId}, NumberOfImages: {NumberOfImages}, Size: {Size}, Group: {Group}",
                taskId, numberOfImages, size, groupName);
        }

        public async Task NotifyImageGenerationProgressAsync(string taskId, int progressPercentage, string status, int imagesCompleted, int totalImages, string? message = null)
        {
            var groupName = SignalRConstants.Groups.ImageTask(taskId);

            await SendToGroupAsync(groupName, SignalRConstants.ClientMethods.ImageGenerationProgress, new
            {
                taskId,
                progressPercentage,
                status,
                imagesCompleted,
                totalImages,
                message,
                timestamp = DateTime.UtcNow
            });

            Logger.LogDebug("Sent ImageGenerationProgress notification for task {TaskId}: {Progress}% ({ImagesCompleted}/{TotalImages})",
                taskId, progressPercentage, imagesCompleted, totalImages);
        }

        public async Task NotifyImageGenerationCompletedAsync(string taskId, string[] imageUrls, TimeSpan duration, decimal cost)
        {
            var groupName = SignalRConstants.Groups.ImageTask(taskId);

            await SendToGroupAsync(groupName, SignalRConstants.ClientMethods.ImageGenerationCompleted, new
            {
                taskId,
                imageUrls,
                durationSeconds = duration.TotalSeconds,
                cost,
                completedAt = DateTime.UtcNow
            });

            Logger.LogInformation("Sent ImageGenerationCompleted notification for task {TaskId} with {ImageCount} images to group {GroupName}",
                taskId, imageUrls.Length, groupName);
        }

        public async Task NotifyImageGenerationFailedAsync(string taskId, string error, bool isRetryable)
        {
            var groupName = SignalRConstants.Groups.ImageTask(taskId);

            await SendToGroupAsync(groupName, SignalRConstants.ClientMethods.ImageGenerationFailed, new
            {
                taskId,
                error,
                isRetryable,
                failedAt = DateTime.UtcNow
            });

            Logger.LogDebug("Sent ImageGenerationFailed notification for task {TaskId}", taskId);
        }

        public async Task NotifyImageGenerationCancelledAsync(string taskId, string? reason)
        {
            var groupName = SignalRConstants.Groups.ImageTask(taskId);

            await SendToGroupAsync(groupName, SignalRConstants.ClientMethods.ImageGenerationCancelled, new
            {
                taskId,
                reason,
                cancelledAt = DateTime.UtcNow
            });

            Logger.LogDebug("Sent ImageGenerationCancelled notification for task {TaskId}", taskId);
        }
    }
}
