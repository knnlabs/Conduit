using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Service for retrieving Redis-specific cache metrics
    /// </summary>
    /// <remarks>
    /// This service provides access to detailed Redis cache metrics such as memory usage,
    /// connection status, and other Redis-specific information. It extends the basic
    /// cache metrics with Redis-specific insights.
    /// </remarks>
    public interface IRedisCacheMetricsService
    {
        /// <summary>
        /// Gets the Redis connection status
        /// </summary>
        /// <returns>True if connected, false otherwise</returns>
        Task<bool> IsConnectedAsync();

        /// <summary>
        /// Gets the Redis server information
        /// </summary>
        /// <returns>A dictionary of Redis server info</returns>
        Task<IDictionary<string, string>> GetServerInfoAsync();

        /// <summary>
        /// Gets the Redis memory usage statistics
        /// </summary>
        /// <returns>Redis memory stats</returns>
        Task<RedisMemoryStats> GetMemoryStatsAsync();

        /// <summary>
        /// Gets the Redis client connection information
        /// </summary>
        /// <returns>Redis client info</returns>
        Task<RedisClientInfo> GetClientInfoAsync();

        /// <summary>
        /// Gets the Redis database stats
        /// </summary>
        /// <returns>Redis database stats</returns>
        Task<RedisDatabaseStats> GetDatabaseStatsAsync();

        /// <summary>
        /// Tests a Redis connection with the specified connection string
        /// </summary>
        /// <param name="connectionString">The Redis connection string to test</param>
        /// <returns>A result object indicating success or failure with error details</returns>
        Task<RedisConnectionTestResult> TestRedisConnectionAsync(string connectionString);
    }

    /// <summary>
    /// Redis memory usage statistics
    /// </summary>
    public class RedisMemoryStats
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
        /// Memory used for caching in bytes
        /// </summary>
        public long CachedMemory { get; set; }

        /// <summary>
        /// Memory used by Lua scripts in bytes
        /// </summary>
        public long LuaMemory { get; set; }
    }

    /// <summary>
    /// Redis client connection information
    /// </summary>
    public class RedisClientInfo
    {
        /// <summary>
        /// Number of connected clients
        /// </summary>
        public int ConnectedClients { get; set; }

        /// <summary>
        /// Number of blocked clients
        /// </summary>
        public int BlockedClients { get; set; }

        /// <summary>
        /// Number of client connection attempts
        /// </summary>
        public long TotalConnectionsReceived { get; set; }

        /// <summary>
        /// Number of rejected connections
        /// </summary>
        public long RejectedConnections { get; set; }
    }

    /// <summary>
    /// Redis database statistics
    /// </summary>
    public class RedisDatabaseStats
    {
        /// <summary>
        /// Total number of keys in the database
        /// </summary>
        public long KeyCount { get; set; }

        /// <summary>
        /// Total number of key expirations
        /// </summary>
        public long ExpiredKeys { get; set; }

        /// <summary>
        /// Total number of key evictions
        /// </summary>
        public long EvictedKeys { get; set; }

        /// <summary>
        /// Redis database hits
        /// </summary>
        public long Hits { get; set; }

        /// <summary>
        /// Redis database misses
        /// </summary>
        public long Misses { get; set; }

        /// <summary>
        /// Hit rate percentage
        /// </summary>
        public double HitRate => (Hits + Misses) > 0 ?
            (double)Hits / (Hits + Misses) * 100 : 0;
    }

    // The RedisConnectionTestResult class is already defined in ICacheStatusService.cs
}
