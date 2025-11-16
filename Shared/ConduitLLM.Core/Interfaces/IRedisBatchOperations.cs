using StackExchange.Redis;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for batch Redis operations to optimize performance
    /// </summary>
    public interface IRedisBatchOperations
    {
        /// <summary>
        /// Execute a batch of Redis operations
        /// </summary>
        Task<RedisBatchOperationResult> ExecuteBatchAsync(Func<IDatabase, IBatch, Task> batchOperations);
        
        /// <summary>
        /// Get multiple values in a single operation
        /// </summary>
        Task<T[]> BatchGetAsync<T>(string[] keys);
        
        /// <summary>
        /// Delete multiple keys in a single operation
        /// </summary>
        Task<BatchDeleteResult> BatchDeleteAsync(string[] keys);
        
        /// <summary>
        /// Set multiple key-value pairs in a single operation
        /// </summary>
        Task<BatchSetResult> BatchSetAsync<T>(Dictionary<string, T> keyValuePairs, TimeSpan? expiry = null);
        
        /// <summary>
        /// Publish multiple messages to channels in a batch
        /// </summary>
        Task<BatchPublishResult> BatchPublishAsync(Dictionary<string, string> channelMessages);
    }
    
    /// <summary>
    /// Result of a Redis batch operation
    /// </summary>
    public class RedisBatchOperationResult
    {
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
        public int OperationCount { get; set; }
        public string? Error { get; set; }
        public string ErrorType { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Result of a batch delete operation
    /// </summary>
    public class BatchDeleteResult : RedisBatchOperationResult
    {
        public int DeletedCount { get; set; }
        public string[] FailedKeys { get; set; } = Array.Empty<string>();
    }
    
    /// <summary>
    /// Result of a batch set operation
    /// </summary>
    public class BatchSetResult : RedisBatchOperationResult
    {
        public int SetCount { get; set; }
        public string[] FailedKeys { get; set; } = Array.Empty<string>();
    }
    
    /// <summary>
    /// Result of a batch publish operation
    /// </summary>
    public class BatchPublishResult : RedisBatchOperationResult
    {
        public int PublishedCount { get; set; }
        public string[] FailedChannels { get; set; } = Array.Empty<string>();
    }
    
    /// <summary>
    /// Interface for caches that support batch invalidation
    /// </summary>
    public interface IBatchInvalidatable
    {
        /// <summary>
        /// Invalidate multiple cache entries in a batch
        /// </summary>
        Task<BatchInvalidationResult> InvalidateBatchAsync(
            IEnumerable<InvalidationRequest> requests, 
            CancellationToken cancellationToken = default);
    }
}