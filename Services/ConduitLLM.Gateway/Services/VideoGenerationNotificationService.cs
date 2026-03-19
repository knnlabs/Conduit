using Microsoft.AspNetCore.SignalR;
using ConduitLLM.Gateway.Hubs;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Interfaces;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Implementation of video generation notification service using SignalR.
    /// Inherits from SignalRNotificationServiceBase for common functionality.
    /// </summary>
    public class VideoGenerationNotificationService
        : SignalRNotificationServiceBase<VideoGenerationHub>,
          IVideoGenerationNotificationService
    {
        public VideoGenerationNotificationService(
            IHubContext<VideoGenerationHub> hubContext,
            ILogger<VideoGenerationNotificationService> logger)
            : base(hubContext, logger)
        {
        }

        public async Task NotifyVideoGenerationStartedAsync(string requestId, string provider, DateTime startedAt, int? estimatedSeconds)
        {
            var taskId = requestId;
            var groupName = SignalRConstants.Groups.VideoTask(taskId);

            await SendToGroupAsync(groupName, SignalRConstants.ClientMethods.VideoGenerationStarted, new
            {
                taskId,
                provider,
                startedAt,
                estimatedSeconds
            });

            Logger.LogDebug("Sent VideoGenerationStarted notification for task {TaskId}", taskId);
        }

        public async Task NotifyVideoGenerationProgressAsync(string requestId, int progressPercentage, string status, string? message = null)
        {
            var taskId = requestId;
            var groupName = SignalRConstants.Groups.VideoTask(taskId);

            await SendToGroupAsync(groupName, SignalRConstants.ClientMethods.VideoGenerationProgress, new
            {
                taskId,
                progressPercentage,
                status,
                message,
                timestamp = DateTime.UtcNow
            });

            Logger.LogDebug("Sent VideoGenerationProgress notification for task {TaskId}: {Progress}%",
                taskId, progressPercentage);
        }

        public async Task NotifyVideoGenerationCompletedAsync(string requestId, string videoUrl, TimeSpan duration, decimal cost)
        {
            var taskId = requestId;
            var groupName = SignalRConstants.Groups.VideoTask(taskId);

            await SendToGroupAsync(groupName, SignalRConstants.ClientMethods.VideoGenerationCompleted, new
            {
                taskId,
                videoUrl,
                durationSeconds = duration.TotalSeconds,
                cost,
                completedAt = DateTime.UtcNow
            });

            Logger.LogDebug("Sent VideoGenerationCompleted notification for task {TaskId}", taskId);
        }

        public async Task NotifyVideoGenerationFailedAsync(string requestId, string error, bool isRetryable)
        {
            var taskId = requestId;
            var groupName = SignalRConstants.Groups.VideoTask(taskId);

            await SendToGroupAsync(groupName, SignalRConstants.ClientMethods.VideoGenerationFailed, new
            {
                taskId,
                error,
                isRetryable,
                failedAt = DateTime.UtcNow
            });

            Logger.LogDebug("Sent VideoGenerationFailed notification for task {TaskId}", taskId);
        }

        public async Task NotifyVideoGenerationCancelledAsync(string requestId, string? reason)
        {
            var taskId = requestId;
            var groupName = SignalRConstants.Groups.VideoTask(taskId);

            await SendToGroupAsync(groupName, SignalRConstants.ClientMethods.VideoGenerationCancelled, new
            {
                taskId,
                reason,
                cancelledAt = DateTime.UtcNow
            });

            Logger.LogDebug("Sent VideoGenerationCancelled notification for task {TaskId}", taskId);
        }
    }
}