namespace ConduitLLM.WebUI.Models
{
    /// <summary>
    /// Status information for the cache
    /// </summary>
    /// <remarks>
    /// This class provides information about the current state of the cache,
    /// including configuration details, usage statistics, and performance metrics.
    /// </remarks>
    public class CacheStatus
    {
        /// <summary>
        /// Whether the cache is enabled
        /// </summary>
        public bool IsEnabled { get; set; }
        
        /// <summary>
        /// The type of cache being used (Memory or Redis)
        /// </summary>
        public string CacheType { get; set; } = "Memory";
        
        /// <summary>
        /// The number of items in the cache
        /// </summary>
        public int TotalItems { get; set; }
        
        /// <summary>
        /// The cache hit rate (0.0 to 1.0)
        /// </summary>
        public double HitRate { get; set; }
        
        /// <summary>
        /// The memory usage of the cache in bytes
        /// </summary>
        public long MemoryUsageBytes { get; set; }
        
        /// <summary>
        /// The average response time in milliseconds
        /// </summary>
        public double AvgResponseTime { get; set; }
        
        /// <summary>
        /// Whether Redis is connected (Redis cache only)
        /// </summary>
        public bool IsRedisConnected { get; set; }
        
        /// <summary>
        /// Redis connection information (Redis cache only)
        /// </summary>
        public RedisConnectionInfo? RedisConnection { get; set; }
        
        /// <summary>
        /// Redis memory stats (Redis cache only)
        /// </summary>
        public RedisMemoryInfo? RedisMemory { get; set; }
        
        /// <summary>
        /// Redis database stats (Redis cache only)
        /// </summary>
        public RedisDatabaseInfo? RedisDatabase { get; set; }
        
        /// <summary>
        /// Optional status message providing additional information about the cache state
        /// </summary>
        /// <remarks>
        /// This property can contain error messages, warnings, or other information about
        /// the current state of the cache. It is particularly useful for diagnosing issues.
        /// </remarks>
        public string? StatusMessage { get; set; }
    }
    
    /// <summary>
    /// Redis connection information
    /// </summary>
    public class RedisConnectionInfo
    {
        /// <summary>
        /// The Redis server endpoint
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;
        
        /// <summary>
        /// The Redis server version
        /// </summary>
        public string Version { get; set; } = string.Empty;
        
        /// <summary>
        /// The number of connected clients
        /// </summary>
        public int ConnectedClients { get; set; }
        
        /// <summary>
        /// The Redis instance name
        /// </summary>
        public string InstanceName { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Redis memory information
    /// </summary>
    public class RedisMemoryInfo
    {
        /// <summary>
        /// Total memory used by Redis in bytes
        /// </summary>
        public long UsedMemory { get; set; }
        
        /// <summary>
        /// Peak memory usage in bytes
        /// </summary>
        public long PeakMemory { get; set; }
        
        /// <summary>
        /// Memory fragmentation ratio
        /// </summary>
        public double FragmentationRatio { get; set; }
        
        /// <summary>
        /// Memory allocated for caching in bytes
        /// </summary>
        public long CachedMemory { get; set; }
    }
    
    /// <summary>
    /// Redis database information
    /// </summary>
    public class RedisDatabaseInfo
    {
        /// <summary>
        /// Total number of keys in the database
        /// </summary>
        public long KeyCount { get; set; }
        
        /// <summary>
        /// Number of expired keys
        /// </summary>
        public long ExpiredKeys { get; set; }
        
        /// <summary>
        /// Number of evicted keys
        /// </summary>
        public long EvictedKeys { get; set; }
        
        /// <summary>
        /// Cache hits
        /// </summary>
        public long Hits { get; set; }
        
        /// <summary>
        /// Cache misses
        /// </summary>
        public long Misses { get; set; }
        
        /// <summary>
        /// Hit rate percentage
        /// </summary>
        public double HitRatePercentage { get; set; }
    }
}