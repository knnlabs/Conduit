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
            var groupName = SignalRConstants.Groups.VideoTask(requestId);

            await SendToGroupAsync(groupName, SignalRConstants.ClientMethods.VideoGenerationStarted, new
            {
                taskId = requestId,
                provider,
                startedAt,
                estimatedSeconds
            });

            Logger.LogDebug("Sent VideoGenerationStarted notification for task {TaskId}", requestId);
        }

        public async Task NotifyVideoGenerationProgressAsync(string requestId, int progressPercentage, string status, string? message = null, int? framesCompleted = null, int? totalFrames = null)
        {
            var groupName = SignalRConstants.Groups.VideoTask(requestId);

            await SendToGroupAsync(groupName, SignalRConstants.ClientMethods.VideoGenerationProgress, new
            {
                taskId = requestId,
                progressPercentage,
                status,
                message,
                framesCompleted,
                totalFrames,
                timestamp = DateTime.UtcNow
            });

            Logger.LogDebug("Sent VideoGenerationProgress notification for task {TaskId}: {Progress}%",
                requestId, progressPercentage);
        }

        public async Task NotifyVideoGenerationCompletedAsync(string requestId, string videoUrl, TimeSpan duration, decimal cost, string? previewUrl = null, string? resolution = null, long? fileSize = null, string? provider = null, string? model = null, DateTime? completedAt = null, double? generationDurationSeconds = null)
        {
            var groupName = SignalRConstants.Groups.VideoTask(requestId);

            await SendToGroupAsync(groupName, SignalRConstants.ClientMethods.VideoGenerationCompleted, new
            {
                taskId = requestId,
                status = "completed",
                videoUrl,
                previewUrl,
                duration = duration.TotalSeconds,
                resolution,
                fileSize,
                cost,
                provider,
                model,
                completedAt = completedAt ?? DateTime.UtcNow,
                generationDuration = generationDurationSeconds
            });

            Logger.LogDebug("Sent VideoGenerationCompleted notification for task {TaskId}", requestId);
        }

        public async Task NotifyVideoGenerationFailedAsync(string requestId, string error, bool isRetryable, string? errorCode = null, int? retryCount = null, int? maxRetries = null, DateTime? nextRetryAt = null, DateTime? failedAt = null)
        {
            var groupName = SignalRConstants.Groups.VideoTask(requestId);

            await SendToGroupAsync(groupName, SignalRConstants.ClientMethods.VideoGenerationFailed, new
            {
                taskId = requestId,
                status = "failed",
                error,
                errorCode,
                isRetryable,
                retryCount,
                maxRetries,
                nextRetryAt,
                failedAt = failedAt ?? DateTime.UtcNow
            });

            Logger.LogDebug("Sent VideoGenerationFailed notification for task {TaskId}", requestId);
        }

        public async Task NotifyVideoGenerationCancelledAsync(string requestId, string? reason)
        {
            var groupName = SignalRConstants.Groups.VideoTask(requestId);

            await SendToGroupAsync(groupName, SignalRConstants.ClientMethods.VideoGenerationCancelled, new
            {
                taskId = requestId,
                reason,
                cancelledAt = DateTime.UtcNow
            });

            Logger.LogDebug("Sent VideoGenerationCancelled notification for task {TaskId}", requestId);
        }
    }
}
