using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Interfaces;

using MassTransit;

using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.Gateway.EventHandlers
{
    /// <summary>
    /// Handles VideoGenerationCompleted events to update task status and track completion metrics.
    /// Also updates the RequestLog with the actual cost after video generation completes.
    /// </summary>
    public class VideoGenerationCompletedHandler : IConsumer<VideoGenerationCompleted>
    {
        private readonly IAsyncTaskService _asyncTaskService;
        private readonly IRequestLogRepository _requestLogRepository;
        private readonly IMemoryCache _progressCache;
        private readonly IVideoGenerationNotificationService _notificationService;
        private readonly ILogger<VideoGenerationCompletedHandler> _logger;
        private const string CompletedTasksCacheKey = "completed_video_tasks";

        public VideoGenerationCompletedHandler(
            IAsyncTaskService asyncTaskService,
            IRequestLogRepository requestLogRepository,
            IMemoryCache progressCache,
            IVideoGenerationNotificationService notificationService,
            ILogger<VideoGenerationCompletedHandler> logger)
        {
            _asyncTaskService = asyncTaskService;
            _requestLogRepository = requestLogRepository;
            _progressCache = progressCache;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<VideoGenerationCompleted> context)
        {
            var message = context.Message;

            _logger.LogInformation("Processing video generation completion for request {RequestId}: Video generated in {Duration}s (cost: ${Cost})",
                message.RequestId, message.GenerationDuration.TotalSeconds, message.Cost);

            try
            {
                // Parse resolution to width/height if available
                int width = 0, height = 0;
                if (!string.IsNullOrEmpty(message.Resolution))
                {
                    var parts = message.Resolution.Split('x', 'X');
                    if (parts.Length == 2)
                    {
                        int.TryParse(parts[0], out width);
                        int.TryParse(parts[1], out height);
                    }
                }

                // Update task status to completed with VideoGenerationResponse format
                // This matches what the SDK expects: { created, data: [{ url, metadata }], model }
                var result = new VideoGenerationResponse
                {
                    Created = new DateTimeOffset(message.CompletedAt).ToUnixTimeSeconds(),
                    Data = new List<VideoData>
                    {
                        new VideoData
                        {
                            Url = message.VideoUrl,
                            Metadata = new VideoMetadata
                            {
                                Width = width,
                                Height = height,
                                Duration = message.Duration,
                                FileSizeBytes = message.FileSize
                            }
                        }
                    },
                    Model = message.Model,
                    Usage = new VideoGenerationUsage
                    {
                        VideosGenerated = 1,
                        TotalDurationSeconds = message.Duration
                    }
                };

                await _asyncTaskService.UpdateTaskStatusAsync(
                    message.RequestId,
                    TaskState.Completed,
                    progress: 100,
                    result: result,
                    error: null,
                    cancellationToken: context.CancellationToken);

                // Update the RequestLog with the actual cost and metadata
                // The middleware logged the request with $0 cost because duration wasn't known at submission time
                try
                {
                    var updated = await _requestLogRepository.UpdateCostByTaskIdAsync(
                        taskId: message.RequestId,
                        cost: message.Cost,
                        modelName: message.Model,
                        durationSeconds: message.Duration,
                        resolution: message.Resolution,
                        cancellationToken: context.CancellationToken);

                    if (updated)
                    {
                        _logger.LogInformation(
                            "Updated RequestLog for task {TaskId} with actual cost ${Cost} (duration: {Duration}s)",
                            message.RequestId, message.Cost, message.Duration);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Could not find RequestLog for task {TaskId} to update cost - log may not exist or taskId not stored in metadata",
                            message.RequestId);
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail the handler - the video was generated successfully
                    _logger.LogError(ex,
                        "Failed to update RequestLog cost for task {TaskId}, actual cost ${Cost} may not be reflected in logs",
                        message.RequestId, message.Cost);
                }

                // Clear progress cache for this task
                var progressCacheKey = CacheKeys.MediaProgress.VideoProgress(message.RequestId);
                _progressCache.Remove(progressCacheKey);

                // Store completion info for analytics and audit
                var completionData = new
                {
                    RequestId = message.RequestId,
                    VideoUrl = message.VideoUrl,
                    PreviewUrl = message.PreviewUrl,
                    VideoDuration = message.Duration,
                    Resolution = message.Resolution,
                    FileSize = message.FileSize,
                    Provider = message.Provider,
                    Model = message.Model,
                    GenerationDuration = message.GenerationDuration.TotalSeconds,
                    Cost = message.Cost,
                    CompletedAt = message.CompletedAt
                };

                // Cache completion data for recent tasks (24 hours)
                UpdateCompletedTasksCache(completionData);

                // Log performance metrics
                _logger.LogInformation("Video generation performance - Provider: {Provider}, Model: {Model}, Generation time: {GenerationTime}s, Video duration: {VideoDuration}s, Cost: ${Cost}",
                    LoggingSanitizer.S(message.Provider), LoggingSanitizer.S(message.Model), message.GenerationDuration.TotalSeconds, message.Duration, message.Cost);

                // Track provider-specific metrics
                LogProviderMetrics(message.Provider, message.Model, message.GenerationDuration, message.Duration, message.Cost);

                // Send completion notification via notification service
                await _notificationService.NotifyVideoGenerationCompletedAsync(
                    message.RequestId,
                    message.VideoUrl,
                    message.GenerationDuration,
                    message.Cost,
                    previewUrl: message.PreviewUrl,
                    resolution: message.Resolution,
                    fileSize: message.FileSize,
                    provider: message.Provider,
                    model: message.Model,
                    completedAt: message.CompletedAt,
                    generationDurationSeconds: message.GenerationDuration.TotalSeconds);

                _logger.LogInformation("Video generation completed for request {RequestId}", message.RequestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling video generation completion for request {RequestId}", message.RequestId);
                throw; // Let MassTransit handle retry
            }
        }

        private void UpdateCompletedTasksCache(object completionData)
        {
            // Maintain a rolling list of recently completed tasks
            var completedTasks = _progressCache.Get<List<object>>(CompletedTasksCacheKey) ?? new List<object>();

            // Add new completion
            completedTasks.Add(completionData);

            // Keep only last 100 completed tasks
            if (completedTasks.Count() > 100)
            {
                completedTasks = completedTasks.Skip(completedTasks.Count() - 100).ToList();
            }

            // Cache for 24 hours
            _progressCache.Set(CompletedTasksCacheKey, completedTasks, TimeSpan.FromHours(24));
        }

        private void LogProviderMetrics(string provider, string model, TimeSpan generationDuration, double videoDuration, decimal cost)
        {
            // Log provider-specific metrics for monitoring and optimization
            var metrics = new Dictionary<string, object>
            {
                ["provider"] = provider,
                ["model"] = model,
                ["generation_duration_seconds"] = generationDuration.TotalSeconds,
                ["video_duration_seconds"] = videoDuration,
                ["cost"] = cost,
                ["cost_per_second"] = videoDuration > 0 ? cost / (decimal)videoDuration : 0,
                ["generation_speed_ratio"] = generationDuration.TotalSeconds > 0 ? videoDuration / generationDuration.TotalSeconds : 0
            };

            _logger.LogInformation("Video generation metrics: {Metrics}", metrics);
        }
    }
}
