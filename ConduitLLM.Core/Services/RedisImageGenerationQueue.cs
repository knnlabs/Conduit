using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Redis-based implementation of the image generation queue.
    /// Uses Redis Streams for reliable task distribution across multiple instances.
    /// </summary>
    public class RedisImageGenerationQueue : IImageGenerationQueue
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisImageGenerationQueue> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        
        private const string StreamKey = "conduit:imagegen:stream";
        private const string ConsumerGroup = "conduit-imagegen";
        private const string ClaimsHashKey = "conduit:imagegen:claims";
        private const string ActiveTasksSetKey = "conduit:imagegen:active";
        private const string TaskDataHashKey = "conduit:imagegen:tasks";
        private const string RetryQueueKey = "conduit:imagegen:retry";

        public RedisImageGenerationQueue(
            IConnectionMultiplexer redis,
            ILogger<RedisImageGenerationQueue> logger)
        {
            _redis = redis;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            // Ensure consumer group exists
            EnsureConsumerGroupExists().GetAwaiter().GetResult();
        }

        private async Task EnsureConsumerGroupExists()
        {
            try
            {
                var db = _redis.GetDatabase();
                // Try to create the consumer group, ignore if it already exists
                await db.StreamCreateConsumerGroupAsync(StreamKey, ConsumerGroup, "0");
            }
            catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
            {
                // Consumer group already exists, this is fine
                _logger.LogDebug("Consumer group {Group} already exists", ConsumerGroup);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating consumer group");
            }
        }

        public async Task<string> EnqueueAsync(ImageGenerationRequested request, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = _redis.GetDatabase();
                var taskData = JsonSerializer.Serialize(request, _jsonOptions);
                
                // Add to Redis Stream
                var messageId = await db.StreamAddAsync(StreamKey, new[]
                {
                    new NameValueEntry("taskId", request.TaskId),
                    new NameValueEntry("priority", request.Priority.ToString()),
                    new NameValueEntry("virtualKeyId", request.VirtualKeyId.ToString()),
                    new NameValueEntry("data", taskData)
                });
                
                // Store full task data in hash for quick lookup
                await db.HashSetAsync(TaskDataHashKey, request.TaskId, taskData);
                
                _logger.LogInformation("Enqueued image generation task {TaskId} with message ID {MessageId}", 
                    request.TaskId, messageId);
                
                return request.TaskId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enqueuing image generation task");
                throw;
            }
        }

        public async Task<ImageGenerationRequested?> DequeueAsync(string instanceId, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = _redis.GetDatabase();
                
                // First, check for any retry tasks that are due
                var retryTask = await CheckRetryQueueAsync(instanceId);
                if (retryTask != null)
                {
                    return retryTask;
                }
                
                // Try to read from the stream
                var messages = await db.StreamReadGroupAsync(
                    StreamKey,
                    ConsumerGroup,
                    instanceId,
                    ">", // Read only new messages
                    count: 1,
                    noAck: false); // Auto-acknowledge for now
                
                if (messages.Length == 0)
                {
                    return null;
                }
                
                var message = messages[0];
                var taskId = message.Values.FirstOrDefault(v => v.Name == "taskId").Value.ToString();
                var data = message.Values.FirstOrDefault(v => v.Name == "data").Value.ToString();
                
                if (string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(data))
                {
                    _logger.LogWarning("Invalid message in stream: {MessageId}", message.Id);
                    return null;
                }
                
                // Try to claim the task
                var claimed = await TryClaimTaskAsync(taskId, instanceId);
                if (!claimed)
                {
                    _logger.LogDebug("Task {TaskId} already claimed by another instance", taskId);
                    return null;
                }
                
                // Add to active tasks set
                await db.SetAddAsync(ActiveTasksSetKey, taskId);
                
                // Deserialize and return the task
                var request = JsonSerializer.Deserialize<ImageGenerationRequested>(data, _jsonOptions);
                
                _logger.LogInformation("Instance {InstanceId} claimed task {TaskId}", instanceId, taskId);
                return request;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dequeuing image generation task");
                return null;
            }
        }

        private async Task<bool> TryClaimTaskAsync(string taskId, string instanceId)
        {
            var db = _redis.GetDatabase();
            var claimKey = $"{ClaimsHashKey}:{taskId}";
            
            // Use SET NX with expiry for atomic claim
            var claimData = JsonSerializer.Serialize(new
            {
                instanceId,
                claimedAt = DateTime.UtcNow,
                lastHeartbeat = DateTime.UtcNow
            });
            
            return await db.StringSetAsync(claimKey, claimData, TimeSpan.FromMinutes(5), When.NotExists);
        }

        private async Task<ImageGenerationRequested?> CheckRetryQueueAsync(string instanceId)
        {
            var db = _redis.GetDatabase();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            // Get tasks that are ready for retry (score <= now)
            var retryTasks = await db.SortedSetRangeByScoreWithScoresAsync(
                RetryQueueKey,
                start: 0,
                stop: now,
                take: 1);
            
            if (retryTasks.Length == 0)
            {
                return null;
            }
            
            var taskId = retryTasks[0].Element.ToString();
            
            // Try to claim the retry task
            var claimed = await TryClaimTaskAsync(taskId, instanceId);
            if (!claimed)
            {
                return null;
            }
            
            // Remove from retry queue
            await db.SortedSetRemoveAsync(RetryQueueKey, taskId);
            
            // Get task data
            var taskData = await db.HashGetAsync(TaskDataHashKey, taskId);
            if (taskData.IsNullOrEmpty)
            {
                _logger.LogWarning("Retry task {TaskId} has no data", taskId);
                return null;
            }
            
            return JsonSerializer.Deserialize<ImageGenerationRequested>(taskData!, _jsonOptions);
        }

        public async Task AcknowledgeAsync(string taskId, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = _redis.GetDatabase();
                
                // Remove from active tasks
                await db.SetRemoveAsync(ActiveTasksSetKey, taskId);
                
                // Remove claim
                var claimKey = $"{ClaimsHashKey}:{taskId}";
                await db.KeyDeleteAsync(claimKey);
                
                // Clean up task data
                await db.HashDeleteAsync(TaskDataHashKey, taskId);
                
                _logger.LogInformation("Acknowledged completion of task {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error acknowledging task {TaskId}", taskId);
            }
        }

        public async Task ReturnToQueueAsync(string taskId, string error, TimeSpan? retryAfter = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = _redis.GetDatabase();
                
                // Remove from active tasks
                await db.SetRemoveAsync(ActiveTasksSetKey, taskId);
                
                // Remove claim
                var claimKey = $"{ClaimsHashKey}:{taskId}";
                await db.KeyDeleteAsync(claimKey);
                
                // Add to retry queue with delay
                var retryDelay = retryAfter ?? TimeSpan.FromSeconds(30);
                var retryTime = DateTimeOffset.UtcNow.Add(retryDelay).ToUnixTimeMilliseconds();
                
                await db.SortedSetAddAsync(RetryQueueKey, taskId, retryTime);
                
                // Update task data with error info
                var taskData = await db.HashGetAsync(TaskDataHashKey, taskId);
                if (!taskData.IsNullOrEmpty)
                {
                    var request = JsonSerializer.Deserialize<ImageGenerationRequested>(taskData!, _jsonOptions);
                    if (request != null)
                    {
                        // Store error information separately
                        var errorKey = $"conduit:imagegen:errors:{taskId}";
                        await db.StringSetAsync(errorKey, JsonSerializer.Serialize(new
                        {
                            error,
                            failedAt = DateTime.UtcNow,
                            retryAfter = retryTime
                        }), TimeSpan.FromHours(24));
                    }
                }
                
                _logger.LogInformation("Returned task {TaskId} to retry queue with delay {Delay}s due to: {Error}", 
                    taskId, retryDelay.TotalSeconds, error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error returning task {TaskId} to queue", taskId);
            }
        }

        public async Task<long> GetQueueDepthAsync(CancellationToken cancellationToken = default)
        {
            var db = _redis.GetDatabase();
            
            // Get pending messages in stream
            var pendingInfo = await db.StreamPendingAsync(StreamKey, ConsumerGroup);
            var retryCount = await db.SortedSetLengthAsync(RetryQueueKey);
            
            return pendingInfo.PendingMessageCount + retryCount;
        }

        public async Task<long> GetActiveTaskCountAsync(CancellationToken cancellationToken = default)
        {
            var db = _redis.GetDatabase();
            return await db.SetLengthAsync(ActiveTasksSetKey);
        }

        public async Task<bool> ExtendClaimAsync(string taskId, string instanceId, TimeSpan extension, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = _redis.GetDatabase();
                var claimKey = $"{ClaimsHashKey}:{taskId}";
                
                // Get current claim
                var currentClaim = await db.StringGetAsync(claimKey);
                if (currentClaim.IsNullOrEmpty)
                {
                    return false;
                }
                
                var claim = JsonSerializer.Deserialize<dynamic>(currentClaim!);
                if (claim?.instanceId != instanceId)
                {
                    return false;
                }
                
                // Update claim with new expiry
                var updatedClaim = JsonSerializer.Serialize(new
                {
                    instanceId,
                    claimedAt = claim.claimedAt,
                    lastHeartbeat = DateTime.UtcNow
                });
                
                return await db.StringSetAsync(claimKey, updatedClaim, extension);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extending claim for task {TaskId}", taskId);
                return false;
            }
        }

        public async Task<int> RecoverOrphanedTasksAsync(TimeSpan claimTimeout, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = _redis.GetDatabase();
                var recovered = 0;
                var cutoffTime = DateTime.UtcNow.Subtract(claimTimeout);
                
                // Get all active tasks
                var activeTasks = await db.SetMembersAsync(ActiveTasksSetKey);
                
                foreach (var taskId in activeTasks)
                {
                    var claimKey = $"{ClaimsHashKey}:{taskId}";
                    var claimData = await db.StringGetAsync(claimKey);
                    
                    if (!claimData.IsNullOrEmpty)
                    {
                        try
                        {
                            var claim = JsonSerializer.Deserialize<ClaimInfo>(claimData!);
                            if (claim != null && claim.LastHeartbeat < cutoffTime)
                            {
                                // Task is orphaned, return it to queue
                                await ReturnToQueueAsync(taskId!, "Task orphaned - claim timeout exceeded", TimeSpan.FromSeconds(5));
                                recovered++;
                            }
                        }
                        catch (JsonException)
                        {
                            // Invalid claim data, clean it up
                            await db.KeyDeleteAsync(claimKey);
                        }
                    }
                }
                
                if (recovered > 0)
                {
                    _logger.LogWarning("Recovered {Count} orphaned image generation tasks", recovered);
                }
                
                return recovered;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recovering orphaned tasks");
                return 0;
            }
        }

        private class ClaimInfo
        {
            public string InstanceId { get; set; } = string.Empty;
            public DateTime ClaimedAt { get; set; }
            public DateTime LastHeartbeat { get; set; }
        }
    }
}