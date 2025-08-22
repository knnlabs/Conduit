using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;

using ConduitLLM.Http.Interfaces;
namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles ImageGenerationProgress events to track generation progress and enable real-time updates.
    /// </summary>
    public class ImageGenerationProgressHandler : IConsumer<ImageGenerationProgress>
    {
        private readonly IMemoryCache _progressCache;
        private readonly IAsyncTaskService _taskService;
        private readonly IImageGenerationNotificationService _notificationService;
        private readonly ILogger<ImageGenerationProgressHandler> _logger;
        private const string ProgressCacheKeyPrefix = "image_generation_progress_";

        public ImageGenerationProgressHandler(
            IMemoryCache progressCache,
            IAsyncTaskService taskService,
            IImageGenerationNotificationService notificationService,
            ILogger<ImageGenerationProgressHandler> logger)
        {
            _progressCache = progressCache;
            _taskService = taskService;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<ImageGenerationProgress> context)
        {
            var message = context.Message;
            
            _logger.LogInformation("Processing image generation progress for task {TaskId}: {Status} ({ImagesCompleted}/{TotalImages})", 
                message.TaskId, message.Status, message.ImagesCompleted, message.TotalImages);

            try
            {
                // Update progress cache for real-time queries
                var cacheKey = $"{ProgressCacheKeyPrefix}{message.TaskId}";
                var progressData = new
                {
                    TaskId = message.TaskId,
                    Status = message.Status,
                    ImagesCompleted = message.ImagesCompleted,
                    TotalImages = message.TotalImages,
                    ProgressPercentage = message.ProgressPercentage,
                    Message = message.Message,
                    LastUpdated = DateTime.UtcNow
                };
                
                // Cache progress for 1 hour (long-running tasks)
                _progressCache.Set(cacheKey, progressData, TimeSpan.FromHours(1));
                
                // Update task metadata with progress info
                var taskStatus = await _taskService.GetTaskStatusAsync(message.TaskId);
                if (taskStatus != null && taskStatus.Result is IDictionary<string, object> resultDict)
                {
                    resultDict["progress"] = progressData;
                    await _taskService.UpdateTaskStatusAsync(message.TaskId, taskStatus.State, progress: null, result: resultDict);
                }
                
                // Track generation metrics
                if (message.Status == "processing" && message.ImagesCompleted == 0)
                {
                    _logger.LogInformation("Image generation started for task {TaskId} - generating {TotalImages} images",
                        message.TaskId, message.TotalImages);
                }
                else if (message.Status == "storing")
                {
                    _logger.LogDebug("Storing image {ImagesCompleted} of {TotalImages} for task {TaskId}",
                        message.ImagesCompleted + 1, message.TotalImages, message.TaskId);
                }
                
                // Send real-time updates to WebUI
                await _notificationService.NotifyImageGenerationProgressAsync(
                    message.TaskId,
                    message.ProgressPercentage,
                    message.Status,
                    message.ImagesCompleted,
                    message.TotalImages,
                    message.Message);
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image generation progress for task {TaskId}", message.TaskId);
                throw; // Let MassTransit handle retry
            }

            await Task.CompletedTask;
        }
    }
}