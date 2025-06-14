using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Services;
using ConduitLLM.WebUI.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service for retrieving Redis-specific cache metrics
    /// </summary>
    /// <remarks>
    /// This service provides access to detailed Redis cache metrics such as memory usage,
    /// connection status, and other Redis-specific information. It uses the StackExchange.Redis
    /// library to connect to Redis and retrieve metrics.
    /// </remarks>
    public class RedisCacheMetricsService : IRedisCacheMetricsService
    {
        private readonly ILogger<RedisCacheMetricsService> _logger;
        private readonly CacheOptions _cacheOptions;
        private readonly RedisConnectionFactory _connectionFactory;

        /// <summary>
        /// Creates a new instance of RedisCacheMetricsService
        /// </summary>
        /// <param name="options">Cache configuration options</param>
        /// <param name="connectionFactory">Redis connection factory</param>
        /// <param name="logger">Logger instance</param>
        public RedisCacheMetricsService(
            IOptions<CacheOptions> options,
            RedisConnectionFactory connectionFactory,
            ILogger<RedisCacheMetricsService> logger)
        {
            _cacheOptions = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<bool> IsConnectedAsync()
        {
            try
            {
                if (_cacheOptions.CacheType?.ToLowerInvariant() != "redis" || !_cacheOptions.IsEnabled)
                {
                    return false;
                }

                var connection = await GetConnectionAsync();
                return connection.IsConnected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Redis connection status");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<IDictionary<string, string>> GetServerInfoAsync()
        {
            try
            {
                var result = new Dictionary<string, string>();

                if (!await IsConnectedAsync())
                {
                    return result;
                }

                var connection = await GetConnectionAsync();
                var server = GetServer(connection);

                if (server == null)
                {
                    return result;
                }

                var info = server.Info();
                foreach (var group in info)
                {
                    foreach (var entry in group)
                    {
                        result[$"{group.Key}:{entry.Key}"] = entry.Value;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Redis server info");
                return new Dictionary<string, string>();
            }
        }

        /// <inheritdoc/>
        public async Task<RedisMemoryStats> GetMemoryStatsAsync()
        {
            try
            {
                var result = new RedisMemoryStats();

                if (!await IsConnectedAsync())
                {
                    return result;
                }

                var serverInfo = await GetServerInfoAsync();

                if (serverInfo.TryGetValue("memory:used_memory", out var usedMemory))
                {
                    result.UsedMemory = ParseLong(usedMemory);
                }

                if (serverInfo.TryGetValue("memory:used_memory_peak", out var peakMemory))
                {
                    result.PeakMemory = ParseLong(peakMemory);
                }

                if (serverInfo.TryGetValue("memory:mem_fragmentation_ratio", out var fragRatio))
                {
                    result.FragmentationRatio = ParseDouble(fragRatio);
                }

                if (serverInfo.TryGetValue("memory:used_memory_lua", out var luaMemory))
                {
                    result.LuaMemory = ParseLong(luaMemory);
                }

                if (serverInfo.TryGetValue("memory:used_memory_dataset", out var datasetMemory))
                {
                    result.CachedMemory = ParseLong(datasetMemory);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Redis memory stats");
                return new RedisMemoryStats();
            }
        }

        /// <inheritdoc/>
        public async Task<RedisClientInfo> GetClientInfoAsync()
        {
            try
            {
                var result = new RedisClientInfo();

                if (!await IsConnectedAsync())
                {
                    return result;
                }

                var serverInfo = await GetServerInfoAsync();

                if (serverInfo.TryGetValue("clients:connected_clients", out var clients))
                {
                    result.ConnectedClients = ParseInt(clients);
                }

                if (serverInfo.TryGetValue("clients:blocked_clients", out var blocked))
                {
                    result.BlockedClients = ParseInt(blocked);
                }

                if (serverInfo.TryGetValue("stats:total_connections_received", out var connections))
                {
                    result.TotalConnectionsReceived = ParseLong(connections);
                }

                if (serverInfo.TryGetValue("stats:rejected_connections", out var rejected))
                {
                    result.RejectedConnections = ParseLong(rejected);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Redis client info");
                return new RedisClientInfo();
            }
        }

        /// <inheritdoc/>
        public async Task<RedisDatabaseStats> GetDatabaseStatsAsync()
        {
            try
            {
                var result = new RedisDatabaseStats();

                if (!await IsConnectedAsync())
                {
                    return result;
                }

                var connection = await GetConnectionAsync();
                var server = GetServer(connection);

                if (server == null)
                {
                    return result;
                }

                // Get database stats
                var serverInfo = await GetServerInfoAsync();

                // Get keyspace info for each database
                long totalKeys = 0;
                foreach (var key in serverInfo.Keys.Where(k => k.StartsWith("keyspace:db")))
                {
                    // Extract key count from keyspace info (format: "keys=100,expires=50,avg_ttl=3600")
                    var value = serverInfo[key];
                    var keysPart = value.Split(',').FirstOrDefault(p => p.StartsWith("keys="));

                    if (!string.IsNullOrEmpty(keysPart))
                    {
                        var keyCount = ParseLong(keysPart.Substring(5));
                        totalKeys += keyCount;
                    }
                }

                result.KeyCount = totalKeys;

                if (serverInfo.TryGetValue("stats:keyspace_hits", out var hits))
                {
                    result.Hits = ParseLong(hits);
                }

                if (serverInfo.TryGetValue("stats:keyspace_misses", out var misses))
                {
                    result.Misses = ParseLong(misses);
                }

                if (serverInfo.TryGetValue("stats:expired_keys", out var expired))
                {
                    result.ExpiredKeys = ParseLong(expired);
                }

                if (serverInfo.TryGetValue("stats:evicted_keys", out var evicted))
                {
                    result.EvictedKeys = ParseLong(evicted);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Redis database stats");
                return new RedisDatabaseStats();
            }
        }

        private async Task<IConnectionMultiplexer> GetConnectionAsync()
        {
            return await _connectionFactory.GetConnectionAsync();
        }

        private IServer? GetServer(IConnectionMultiplexer connection)
        {
            try
            {
                var endpoints = connection.GetEndPoints();

                if (endpoints == null || endpoints.Length == 0)
                {
                    _logger.LogWarning("No Redis endpoints found");
                    return null;
                }

                return connection.GetServer(endpoints[0]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Redis server");
                return null;
            }
        }

        private static int ParseInt(string value)
        {
            return int.TryParse(value, out var result) ? result : 0;
        }

        private static long ParseLong(string value)
        {
            return long.TryParse(value, out var result) ? result : 0;
        }

        private static double ParseDouble(string value)
        {
            return double.TryParse(value, out var result) ? result : 0;
        }

        /// <summary>
        /// Tests a Redis connection with the provided connection string
        /// </summary>
        /// <param name="connectionString">Redis connection string to test</param>
        /// <returns>Test result with connection status</returns>
        public async Task<RedisConnectionTestResult> TestRedisConnectionAsync(string connectionString)
        {
            var result = new RedisConnectionTestResult
            {
                IsSuccess = false
            };

            if (string.IsNullOrEmpty(connectionString))
            {
                result.ErrorMessage = "Connection string is empty";
                return result;
            }

            var stopwatch = new System.Diagnostics.Stopwatch();

            try
            {
                stopwatch.Start();
                var connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
                stopwatch.Stop();

                if (connection.IsConnected)
                {
                    var server = connection.GetServer(connection.GetEndPoints()[0]);
                    var info = server.Info("server").FirstOrDefault();

                    result.IsSuccess = true;
                    result.LatencyMs = stopwatch.ElapsedMilliseconds;

                    if (info != null)
                    {
                        var versionEntry = info.FirstOrDefault(x => x.Key == "redis_version");
                        if (!versionEntry.Equals(default(KeyValuePair<string, string>)))
                        {
                            result.ServerVersion = versionEntry.Value;
                        }
                    }
                }
                else
                {
                    result.ErrorMessage = "Failed to connect to Redis";
                }

                // Dispose of the connection - we don't want to keep it around just for the test
                connection.Dispose();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.ErrorMessage = ex.Message;
                _logger.LogWarning(ex, "Error testing Redis connection");
            }

            return result;
        }
    }
}
