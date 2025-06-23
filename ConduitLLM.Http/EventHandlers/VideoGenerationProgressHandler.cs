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
    /// Handles VideoGenerationProgress events to track generation progress and enable real-time updates.
    /// </summary>
    public class VideoGenerationProgressHandler : IConsumer<VideoGenerationProgress>
    {
        private readonly IAsyncTaskService _asyncTaskService;
        private readonly IMemoryCache _progressCache;
        private readonly ILogger<VideoGenerationProgressHandler> _logger;
        private const string ProgressCacheKeyPrefix = "video_generation_progress_";

        public VideoGenerationProgressHandler(
            IAsyncTaskService asyncTaskService,
            IMemoryCache progressCache,
            ILogger<VideoGenerationProgressHandler> logger)
        {
            _asyncTaskService = asyncTaskService;
            _progressCache = progressCache;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<VideoGenerationProgress> context)
        {
            var message = context.Message;
            
            _logger.LogDebug("Video generation progress for request {RequestId}: {Progress}% - {Status}", 
                message.RequestId, message.ProgressPercentage, message.Status);

            try
            {
                // Update progress cache for real-time queries
                var cacheKey = $"{ProgressCacheKeyPrefix}{message.RequestId}";
                var progressData = new
                {
                    RequestId = message.RequestId,
                    Status = message.Status,
                    ProgressPercentage = message.ProgressPercentage,
                    Message = message.Message,
                    FramesCompleted = message.FramesCompleted,
                    TotalFrames = message.TotalFrames,
                    LastUpdated = DateTime.UtcNow
                };
                
                // Cache progress for 1 hour (long-running tasks)
                _progressCache.Set(cacheKey, progressData, TimeSpan.FromHours(1));
                
                // Update task status with progress info
                var taskStatus = await _asyncTaskService.GetTaskStatusAsync(message.RequestId, context.CancellationToken);
                if (taskStatus != null)
                {
                    // Update progress percentage and message
                    taskStatus.Progress = message.ProgressPercentage;
                    taskStatus.ProgressMessage = message.Message ?? message.Status;
                    
                    // Update metadata with detailed progress info
                    if (taskStatus.Result is IDictionary<string, object> resultDict)
                    {
                        resultDict["progress"] = progressData;
                    }
                    else if (taskStatus.Result == null)
                    {
                        taskStatus.Result = new Dictionary<string, object> { ["progress"] = progressData };
                    }
                    
                    await _asyncTaskService.UpdateTaskStatusAsync(
                        message.RequestId, 
                        TaskState.Processing, 
                        progress: message.ProgressPercentage,
                        result: taskStatus.Result,
                        error: null,
                        cancellationToken: context.CancellationToken);
                }
                
                // Log significant progress milestones
                LogProgressMilestone(message);
                
                // Future: Send real-time updates to WebUI via SignalR
                // await _hubContext.Clients.Group($"task_{message.RequestId}").SendAsync("VideoGenerationProgress", progressData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling video generation progress for request {RequestId}", message.RequestId);
                throw; // Let MassTransit handle retry
            }
        }

        private void LogProgressMilestone(VideoGenerationProgress progress)
        {
            // Log significant milestones for monitoring
            if (progress.ProgressPercentage == 0 && progress.Status.Contains("start", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Video generation started for request {RequestId}", progress.RequestId);
            }
            else if (progress.ProgressPercentage == 25)
            {
                _logger.LogInformation("Video generation 25% complete for request {RequestId}", progress.RequestId);
            }
            else if (progress.ProgressPercentage == 50)
            {
                _logger.LogInformation("Video generation 50% complete for request {RequestId}", progress.RequestId);
            }
            else if (progress.ProgressPercentage == 75)
            {
                _logger.LogInformation("Video generation 75% complete for request {RequestId}", progress.RequestId);
            }
            else if (progress.Status.Contains("encoding", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Video encoding phase for request {RequestId}", progress.RequestId);
            }
            else if (progress.Status.Contains("upload", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Video upload phase for request {RequestId}", progress.RequestId);
            }
            
            // Log frame progress if available
            if (progress.FramesCompleted.HasValue && progress.TotalFrames.HasValue)
            {
                _logger.LogDebug("Video generation frames: {FramesCompleted}/{TotalFrames} for request {RequestId}",
                    progress.FramesCompleted, progress.TotalFrames, progress.RequestId);
            }
        }
    }
}