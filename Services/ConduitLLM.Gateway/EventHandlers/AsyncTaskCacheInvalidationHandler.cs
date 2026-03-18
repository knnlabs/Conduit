using ConduitLLM.Core.Events;

using MassTransit;

using Microsoft.Extensions.Caching.Distributed;

namespace ConduitLLM.Gateway.EventHandlers
{
    /// <summary>
    /// Handles async task events to invalidate cache entries.
    /// </summary>
    public class AsyncTaskCacheInvalidationHandler : 
        IConsumer<AsyncTaskCreated>,
        IConsumer<AsyncTaskUpdated>,
        IConsumer<AsyncTaskDeleted>
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<AsyncTaskCacheInvalidationHandler> _logger;
        private const string TASK_KEY_PREFIX = "async:task:";

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncTaskCacheInvalidationHandler"/> class.
        /// </summary>
        /// <param name="cache">The distributed cache service.</param>
        /// <param name="logger">The logger instance.</param>
        public AsyncTaskCacheInvalidationHandler(
            IDistributedCache cache,
            ILogger<AsyncTaskCacheInvalidationHandler> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task Consume(ConsumeContext<AsyncTaskCreated> context)
        {
            var message = context.Message;

            // For created events, we don't need to invalidate cache
            // The task was just created in DB and will be cached on first access
            _logger.LogDebug("Async task created event received for task {TaskId} (type: {TaskType}, no cache invalidation needed)",
                message.TaskId, message.TaskType);

            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task Consume(ConsumeContext<AsyncTaskUpdated> context)
        {
            var message = context.Message;

            _logger.LogDebug("Processing AsyncTaskUpdated event for task {TaskId}, new state: {State}",
                message.TaskId, message.State);

            try
            {
                // Invalidate cache for updated task
                var cacheKey = GetTaskKey(message.TaskId);
                await _cache.RemoveAsync(cacheKey);

                _logger.LogInformation(
                    "Cache invalidated for async task {TaskId} after update to state {State}",
                    message.TaskId,
                    message.State);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invalidate cache for async task {TaskId} (state: {State})",
                    message.TaskId, message.State);
                // Don't throw - cache invalidation failures shouldn't break the system
            }
        }

        /// <inheritdoc/>
        public async Task Consume(ConsumeContext<AsyncTaskDeleted> context)
        {
            var message = context.Message;

            _logger.LogDebug("Processing AsyncTaskDeleted event for task {TaskId}", message.TaskId);

            try
            {
                // Invalidate cache for deleted task
                var cacheKey = GetTaskKey(message.TaskId);
                await _cache.RemoveAsync(cacheKey);

                _logger.LogInformation("Cache invalidated for deleted async task {TaskId}", message.TaskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invalidate cache for deleted async task {TaskId}", message.TaskId);
                // Don't throw - cache invalidation failures shouldn't break the system
            }
        }

        private static string GetTaskKey(string taskId) => $"{TASK_KEY_PREFIX}{taskId}";
    }
}