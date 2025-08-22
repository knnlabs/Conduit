using System.Diagnostics;
using System.Text.Json;
using StackExchange.Redis;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Implementation of batch Redis operations using pipelining for optimal performance
    /// </summary>
    public class RedisBatchOperations : IRedisBatchOperations
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisBatchOperations> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public RedisBatchOperations(
            IConnectionMultiplexer redis,
            ILogger<RedisBatchOperations> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<RedisBatchOperationResult> ExecuteBatchAsync(Func<IDatabase, IBatch, Task> batchOperations)
        {
            var db = _redis.GetDatabase();
            var batch = db.CreateBatch();
            var stopwatch = Stopwatch.StartNew();
            var operationCount = 0;

            try
            {
                // Execute the batch operations
                await batchOperations(db, batch);
                
                // Execute the batch
                batch.Execute();
                
                stopwatch.Stop();
                
                return new RedisBatchOperationResult
                {
                    Success = true,
                    Duration = stopwatch.Elapsed,
                    OperationCount = operationCount
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Redis batch operation failed");
                
                return new RedisBatchOperationResult
                {
                    Success = false,
                    Error = ex.Message,
                    ErrorType = ex.GetType().Name,
                    Duration = stopwatch.Elapsed,
                    OperationCount = operationCount
                };
            }
        }

        public async Task<T[]> BatchGetAsync<T>(string[] keys)
        {
            if (keys == null || keys.Length == 0)
            {
                return Array.Empty<T>();
            }

            var db = _redis.GetDatabase();
            var batch = db.CreateBatch();
            var tasks = new Task<RedisValue>[keys.Length];

            // Queue all get operations
            for (int i = 0; i < keys.Length; i++)
            {
                tasks[i] = batch.StringGetAsync(keys[i]);
            }

            // Execute batch
            batch.Execute();

            // Wait for all operations to complete
            var values = await Task.WhenAll(tasks);
            
            // Deserialize results
            var results = new T[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].HasValue)
                {
                    try
                    {
                        results[i] = JsonSerializer.Deserialize<T>(values[i]!, _jsonOptions)!;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize value for key {Key}", keys[i]);
                        results[i] = default!;
                    }
                }
                else
                {
                    results[i] = default!;
                }
            }

            return results;
        }

        public async Task<BatchDeleteResult> BatchDeleteAsync(string[] keys)
        {
            if (keys == null || keys.Length == 0)
            {
                return new BatchDeleteResult
                {
                    Success = true,
                    DeletedCount = 0,
                    Duration = TimeSpan.Zero
                };
            }

            var stopwatch = Stopwatch.StartNew();
            var db = _redis.GetDatabase();
            var batch = db.CreateBatch();
            var deleteTasks = new List<Task<bool>>();
            var failedKeys = new List<string>();

            try
            {
                // Use pipeline for batch delete
                foreach (var key in keys)
                {
                    deleteTasks.Add(batch.KeyDeleteAsync(key));
                }

                // Execute batch
                batch.Execute();

                // Wait for all deletes to complete
                var results = await Task.WhenAll(deleteTasks);
                
                // Count successful deletes
                var deletedCount = 0;
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i])
                    {
                        deletedCount++;
                    }
                    else if (await db.KeyExistsAsync(keys[i]))
                    {
                        // Key still exists but wasn't deleted
                        failedKeys.Add(keys[i]);
                    }
                }

                stopwatch.Stop();
                
                if (failedKeys.Count() > 0)
                {
                    _logger.LogWarning("Failed to delete {Count} keys during batch delete", failedKeys.Count());
                }

                return new BatchDeleteResult
                {
                    Success = true,
                    DeletedCount = deletedCount,
                    FailedKeys = failedKeys.ToArray(),
                    Duration = stopwatch.Elapsed,
                    OperationCount = keys.Length
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Batch delete operation failed");

                return new BatchDeleteResult
                {
                    Success = false,
                    Error = ex.Message,
                    ErrorType = ex.GetType().Name,
                    Duration = stopwatch.Elapsed,
                    OperationCount = keys.Length,
                    FailedKeys = keys
                };
            }
        }

        public async Task<BatchSetResult> BatchSetAsync<T>(Dictionary<string, T> keyValuePairs, TimeSpan? expiry = null)
        {
            if (keyValuePairs == null || keyValuePairs.Count() == 0)
            {
                return new BatchSetResult
                {
                    Success = true,
                    SetCount = 0,
                    Duration = TimeSpan.Zero
                };
            }

            var stopwatch = Stopwatch.StartNew();
            var db = _redis.GetDatabase();
            var batch = db.CreateBatch();
            var setTasks = new List<Task<bool>>();
            var failedKeys = new List<string>();

            try
            {
                // Queue all set operations
                foreach (var kvp in keyValuePairs)
                {
                    var serialized = JsonSerializer.Serialize(kvp.Value, _jsonOptions);
                    setTasks.Add(batch.StringSetAsync(kvp.Key, serialized, expiry));
                }

                // Execute batch
                batch.Execute();

                // Wait for all sets to complete
                var results = await Task.WhenAll(setTasks);
                
                // Count successful sets
                var setCount = 0;
                var keys = keyValuePairs.Keys.ToArray();
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i])
                    {
                        setCount++;
                    }
                    else
                    {
                        failedKeys.Add(keys[i]);
                    }
                }

                stopwatch.Stop();
                
                if (failedKeys.Count() > 0)
                {
                    _logger.LogWarning("Failed to set {Count} keys during batch set", failedKeys.Count());
                }

                return new BatchSetResult
                {
                    Success = true,
                    SetCount = setCount,
                    FailedKeys = failedKeys.ToArray(),
                    Duration = stopwatch.Elapsed,
                    OperationCount = keyValuePairs.Count()
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Batch set operation failed");

                return new BatchSetResult
                {
                    Success = false,
                    Error = ex.Message,
                    ErrorType = ex.GetType().Name,
                    Duration = stopwatch.Elapsed,
                    OperationCount = keyValuePairs.Count(),
                    FailedKeys = keyValuePairs.Keys.ToArray()
                };
            }
        }

        public async Task<BatchPublishResult> BatchPublishAsync(Dictionary<string, string> channelMessages)
        {
            if (channelMessages == null || channelMessages.Count() == 0)
            {
                return new BatchPublishResult
                {
                    Success = true,
                    PublishedCount = 0,
                    Duration = TimeSpan.Zero
                };
            }

            var stopwatch = Stopwatch.StartNew();
            var subscriber = _redis.GetSubscriber();
            var publishTasks = new List<Task<long>>();
            var failedChannels = new List<string>();

            try
            {
                // Queue all publish operations
                foreach (var kvp in channelMessages)
                {
                    publishTasks.Add(subscriber.PublishAsync(
                        RedisChannel.Literal(kvp.Key), 
                        kvp.Value));
                }

                // Wait for all publishes to complete
                var results = await Task.WhenAll(publishTasks);
                
                // Count successful publishes
                var publishedCount = 0;
                var channels = channelMessages.Keys.ToArray();
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i] > 0)
                    {
                        publishedCount++;
                    }
                    else
                    {
                        failedChannels.Add(channels[i]);
                        _logger.LogWarning("No subscribers for channel {Channel}", channels[i]);
                    }
                }

                stopwatch.Stop();

                return new BatchPublishResult
                {
                    Success = true,
                    PublishedCount = publishedCount,
                    FailedChannels = failedChannels.ToArray(),
                    Duration = stopwatch.Elapsed,
                    OperationCount = channelMessages.Count()
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Batch publish operation failed");

                return new BatchPublishResult
                {
                    Success = false,
                    Error = ex.Message,
                    ErrorType = ex.GetType().Name,
                    Duration = stopwatch.Elapsed,
                    OperationCount = channelMessages.Count(),
                    FailedChannels = channelMessages.Keys.ToArray()
                };
            }
        }
    }
}