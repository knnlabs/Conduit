using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Services;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using ConduitLLM.Http.Interfaces;
namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles ImageGenerationCompleted events to update task status, trigger post-processing, and send notifications.
    /// </summary>
    public class ImageGenerationCompletedHandler : IConsumer<ImageGenerationCompleted>
    {
        private readonly IMemoryCache _progressCache;
        private readonly IImageGenerationNotificationService _notificationService;
        private readonly ILogger<ImageGenerationCompletedHandler> _logger;
        private const string ProgressCacheKeyPrefix = "image_generation_progress_";
        private const string CompletedTasksCacheKey = "completed_image_tasks";

        public ImageGenerationCompletedHandler(
            IMemoryCache progressCache,
            IImageGenerationNotificationService notificationService,
            ILogger<ImageGenerationCompletedHandler> logger)
        {
            _progressCache = progressCache;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<ImageGenerationCompleted> context)
        {
            var message = context.Message;
            
            _logger.LogInformation("Processing image generation completion for task {TaskId}: {ImageCount} images generated in {Duration}s (cost: ${Cost})", 
                message.TaskId, message.Images.Count, message.Duration.TotalSeconds, message.Cost);

            try
            {
                // Clear progress cache for this task
                var progressCacheKey = $"{ProgressCacheKeyPrefix}{message.TaskId}";
                _progressCache.Remove(progressCacheKey);
                
                // Store completion info for analytics and audit
                var completionData = new
                {
                    TaskId = message.TaskId,
                    VirtualKeyId = message.VirtualKeyId,
                    ImageCount = message.Images.Count,
                    Provider = message.Provider,
                    Model = message.Model,
                    Duration = message.Duration.TotalSeconds,
                    Cost = message.Cost,
                    CompletedAt = DateTime.UtcNow,
                    ImageUrls = message.Images.Select(img => img.Url).ToList()
                };
                
                // Cache completion data for recent tasks (24 hours)
                UpdateCompletedTasksCache(completionData);
                
                // Log performance metrics
                var avgTimePerImage = message.Duration.TotalSeconds / Math.Max(1, message.Images.Count);
                _logger.LogInformation("Image generation performance - Provider: {Provider}, Model: {Model}, Avg time per image: {AvgTime}s, Total cost: ${Cost}",
                    message.Provider, message.Model, avgTimePerImage, message.Cost);
                
                // Track provider-specific metrics
                LogProviderMetrics(message.Provider, message.Model, message.Images.Count, message.Duration, message.Cost);
                
                // Future: Trigger post-processing workflows
                // - Image optimization
                // - Metadata extraction
                // - CDN cache warming
                
                // Send completion notification to WebUI
                await _notificationService.NotifyImageGenerationCompletedAsync(
                    message.TaskId,
                    message.Images.Select(img => img.Url ?? string.Empty).ToArray(),
                    message.Duration,
                    message.Cost);
                
                // Future: Send webhook notification if configured
                // await _webhookService.SendImageGenerationCompletedWebhook(message);
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image generation completion for task {TaskId}", message.TaskId);
                throw; // Let MassTransit handle retry
            }

            await Task.CompletedTask;
        }

        private void UpdateCompletedTasksCache(object completionData)
        {
            // Maintain a rolling list of recently completed tasks
            var completedTasks = _progressCache.Get<List<object>>(CompletedTasksCacheKey) ?? new List<object>();
            
            // Add new completion
            completedTasks.Add(completionData);
            
            // Keep only last 100 completed tasks
            if (completedTasks.Count > 100)
            {
                completedTasks = completedTasks.Skip(completedTasks.Count - 100).ToList();
            }
            
            // Cache for 24 hours
            _progressCache.Set(CompletedTasksCacheKey, completedTasks, TimeSpan.FromHours(24));
        }

        private void LogProviderMetrics(string provider, string model, int imageCount, TimeSpan duration, decimal cost)
        {
            // Log provider-specific metrics for monitoring and optimization
            var metrics = new Dictionary<string, object>
            {
                ["provider"] = provider,
                ["model"] = model,
                ["image_count"] = imageCount,
                ["duration_seconds"] = duration.TotalSeconds,
                ["cost"] = cost,
                ["cost_per_image"] = imageCount > 0 ? cost / imageCount : 0,
                ["images_per_second"] = duration.TotalSeconds > 0 ? imageCount / duration.TotalSeconds : 0
            };
            
            _logger.LogInformation("Image generation metrics: {Metrics}", metrics);
        }
    }
}