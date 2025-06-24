using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Http.Hubs;

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
                await _hubContext.Clients.All.SendAsync("VideoGenerationStarted", new
                {
                    requestId,
                    provider,
                    startedAt,
                    estimatedSeconds
                });
                
                _logger.LogDebug("Sent VideoGenerationStarted notification for request {RequestId}", requestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VideoGenerationStarted notification for request {RequestId}", requestId);
            }
        }

        public async Task NotifyVideoGenerationProgressAsync(string requestId, int progressPercentage, string status, string? message = null)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("VideoGenerationProgress", new
                {
                    requestId,
                    progressPercentage,
                    status,
                    message,
                    timestamp = DateTime.UtcNow
                });
                
                _logger.LogDebug("Sent VideoGenerationProgress notification for request {RequestId}: {Progress}%", 
                    requestId, progressPercentage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VideoGenerationProgress notification for request {RequestId}", requestId);
            }
        }

        public async Task NotifyVideoGenerationCompletedAsync(string requestId, string videoUrl, TimeSpan duration, decimal cost)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("VideoGenerationCompleted", new
                {
                    requestId,
                    videoUrl,
                    durationSeconds = duration.TotalSeconds,
                    cost,
                    completedAt = DateTime.UtcNow
                });
                
                _logger.LogDebug("Sent VideoGenerationCompleted notification for request {RequestId}", requestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VideoGenerationCompleted notification for request {RequestId}", requestId);
            }
        }

        public async Task NotifyVideoGenerationFailedAsync(string requestId, string error, bool isRetryable)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("VideoGenerationFailed", new
                {
                    requestId,
                    error,
                    isRetryable,
                    failedAt = DateTime.UtcNow
                });
                
                _logger.LogDebug("Sent VideoGenerationFailed notification for request {RequestId}", requestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VideoGenerationFailed notification for request {RequestId}", requestId);
            }
        }

        public async Task NotifyVideoGenerationCancelledAsync(string requestId, string? reason)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("VideoGenerationCancelled", new
                {
                    requestId,
                    reason,
                    cancelledAt = DateTime.UtcNow
                });
                
                _logger.LogDebug("Sent VideoGenerationCancelled notification for request {RequestId}", requestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VideoGenerationCancelled notification for request {RequestId}", requestId);
            }
        }
    }
}