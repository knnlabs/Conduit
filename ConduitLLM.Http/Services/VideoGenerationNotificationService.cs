using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Core.Constants;

using ConduitLLM.Http.Interfaces;
namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Implementation of video generation notification service using SignalR
    /// </summary>
    public class VideoGenerationNotificationService : IVideoGenerationNotificationService
    {
        private readonly IHubContext<VideoGenerationHub> _hubContext;
        private readonly ILogger<VideoGenerationNotificationService> _logger;

        public VideoGenerationNotificationService(
            IHubContext<VideoGenerationHub> hubContext,
            ILogger<VideoGenerationNotificationService> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task NotifyVideoGenerationStartedAsync(string requestId, string provider, DateTime startedAt, int? estimatedSeconds)
        {
            try
            {
                // Use taskId for consistency and send to specific group for security
                var taskId = requestId; // requestId is actually taskId in the video generation flow
                await _hubContext.Clients.Group(SignalRConstants.Groups.VideoTask(taskId)).SendAsync(SignalRConstants.ClientMethods.VideoGenerationStarted, new
                {
                    taskId, // Changed from requestId to taskId for consistency
                    provider,
                    startedAt,
                    estimatedSeconds
                });
                
                _logger.LogDebug("Sent VideoGenerationStarted notification for task {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VideoGenerationStarted notification for task {TaskId}", requestId);
            }
        }

        public async Task NotifyVideoGenerationProgressAsync(string requestId, int progressPercentage, string status, string? message = null)
        {
            try
            {
                // Use taskId for consistency and send to specific group for security
                var taskId = requestId; // requestId is actually taskId in the video generation flow
                await _hubContext.Clients.Group(SignalRConstants.Groups.VideoTask(taskId)).SendAsync(SignalRConstants.ClientMethods.VideoGenerationProgress, new
                {
                    taskId, // Changed from requestId to taskId for consistency
                    progressPercentage,
                    status,
                    message,
                    timestamp = DateTime.UtcNow
                });
                
                _logger.LogDebug("Sent VideoGenerationProgress notification for task {TaskId}: {Progress}%", 
                    taskId, progressPercentage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VideoGenerationProgress notification for task {TaskId}", requestId);
            }
        }

        public async Task NotifyVideoGenerationCompletedAsync(string requestId, string videoUrl, TimeSpan duration, decimal cost)
        {
            try
            {
                // Use taskId for consistency and send to specific group for security
                var taskId = requestId; // requestId is actually taskId in the video generation flow
                await _hubContext.Clients.Group(SignalRConstants.Groups.VideoTask(taskId)).SendAsync(SignalRConstants.ClientMethods.VideoGenerationCompleted, new
                {
                    taskId, // Changed from requestId to taskId for consistency
                    videoUrl,
                    durationSeconds = duration.TotalSeconds,
                    cost,
                    completedAt = DateTime.UtcNow
                });
                
                _logger.LogDebug("Sent VideoGenerationCompleted notification for task {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VideoGenerationCompleted notification for task {TaskId}", requestId);
            }
        }

        public async Task NotifyVideoGenerationFailedAsync(string requestId, string error, bool isRetryable)
        {
            try
            {
                // Use taskId for consistency and send to specific group for security
                var taskId = requestId; // requestId is actually taskId in the video generation flow
                await _hubContext.Clients.Group(SignalRConstants.Groups.VideoTask(taskId)).SendAsync(SignalRConstants.ClientMethods.VideoGenerationFailed, new
                {
                    taskId, // Changed from requestId to taskId for consistency
                    error,
                    isRetryable,
                    failedAt = DateTime.UtcNow
                });
                
                _logger.LogDebug("Sent VideoGenerationFailed notification for task {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VideoGenerationFailed notification for task {TaskId}", requestId);
            }
        }

        public async Task NotifyVideoGenerationCancelledAsync(string requestId, string? reason)
        {
            try
            {
                // Use taskId for consistency and send to specific group for security
                var taskId = requestId; // requestId is actually taskId in the video generation flow
                await _hubContext.Clients.Group($"video-{taskId}").SendAsync("VideoGenerationCancelled", new
                {
                    taskId, // Changed from requestId to taskId for consistency
                    reason,
                    cancelledAt = DateTime.UtcNow
                });
                
                _logger.LogDebug("Sent VideoGenerationCancelled notification for task {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VideoGenerationCancelled notification for task {TaskId}", requestId);
            }
        }
    }
}