using System.Text.Json;
using StackExchange.Redis;

namespace ConduitLLM.Gateway.Services
{
    public partial class DistributedPerformanceMonitoringService
    {
        public async Task RecordRequestMetricAsync(string endpoint, double responseTimeMs, bool isSuccess)
        {
            try
            {
                var metric = new
                {
                    InstanceId,
                    Endpoint = endpoint,
                    ResponseTimeMs = responseTimeMs,
                    IsSuccess = isSuccess,
                    Timestamp = DateTime.UtcNow.Ticks
                };

                // Add to Redis stream for time-series data
                await _database.StreamAddAsync(RequestsStreamKey, "data", JsonSerializer.Serialize(metric));

                // Trim stream to keep only recent data (last hour)
                await _database.StreamTrimAsync(RequestsStreamKey, _options.MaxMetricsRetention, false);

            // Update endpoint-specific aggregated metrics atomically
            var endpointKey = $"{EndpointMetricsPrefix}:{endpoint}";
            var script = @"
                local key = KEYS[1]
                local responseTime = tonumber(ARGV[1])
                local isSuccess = ARGV[2] == 'true'

                local current = redis.call('HMGET', key, 'total_requests', 'successful_requests', 'total_response_time', 'max_response_time', 'min_response_time')

                local totalRequests = tonumber(current[1]) or 0
                local successfulRequests = tonumber(current[2]) or 0
                local totalResponseTime = tonumber(current[3]) or 0
                local maxResponseTime = tonumber(current[4]) or responseTime
                local minResponseTime = tonumber(current[5]) or responseTime

                totalRequests = totalRequests + 1
                if isSuccess then
                    successfulRequests = successfulRequests + 1
                end
                totalResponseTime = totalResponseTime + responseTime
                maxResponseTime = math.max(maxResponseTime, responseTime)
                minResponseTime = math.min(minResponseTime, responseTime)

                redis.call('HMSET', key,
                    'endpoint', ARGV[3],
                    'total_requests', totalRequests,
                    'successful_requests', successfulRequests,
                    'total_response_time', totalResponseTime,
                    'max_response_time', maxResponseTime,
                    'min_response_time', minResponseTime,
                    'last_updated', ARGV[4]
                )

                redis.call('EXPIRE', key, 3600)

                return totalRequests
            ";

                await _database.ScriptEvaluateAsync(script, new RedisKey[] { endpointKey },
                    new RedisValue[] { responseTimeMs, isSuccess.ToString(), endpoint, DateTime.UtcNow.Ticks });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record request metric for endpoint {Endpoint}", endpoint);
            }
        }

        public async Task RecordDatabaseQueryMetricAsync(string operation, double executionTimeMs)
        {
            try
            {
                var metric = new
                {
                    InstanceId,
                    Operation = operation,
                    ExecutionTimeMs = executionTimeMs,
                    Timestamp = DateTime.UtcNow.Ticks
                };

                await _database.StreamAddAsync(DatabaseOpsStreamKey, "data", JsonSerializer.Serialize(metric));
                await _database.StreamTrimAsync(DatabaseOpsStreamKey, _options.MaxMetricsRetention, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record database query metric for operation {Operation}", operation);
            }
        }

        public async Task RecordCacheMetricAsync(string operation, bool isHit)
        {
            var cacheKey = $"{CacheMetricsPrefix}:{operation}";
            var script = @"
                local key = KEYS[1]
                local isHit = ARGV[1] == 'true'

                local current = redis.call('HMGET', key, 'total_requests', 'hits')
                local totalRequests = tonumber(current[1]) or 0
                local hits = tonumber(current[2]) or 0

                totalRequests = totalRequests + 1
                if isHit then
                    hits = hits + 1
                end

                redis.call('HMSET', key,
                    'operation', ARGV[2],
                    'total_requests', totalRequests,
                    'hits', hits,
                    'last_updated', ARGV[3]
                )

                redis.call('EXPIRE', key, 3600)
                return totalRequests
            ";

            try
            {
                await _database.ScriptEvaluateAsync(script, new RedisKey[] { cacheKey },
                    new RedisValue[] { isHit.ToString(), operation, DateTime.UtcNow.Ticks });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record cache metric for operation {Operation}", operation);
            }
        }

        public async Task RecordConnectionPoolMetricAsync(string poolName, int active, int idle, int waitQueue)
        {
            var poolKey = $"{ConnectionPoolMetricsPrefix}:{poolName}";
            var data = new Dictionary<string, RedisValue>
            {
                ["pool_name"] = poolName,
                ["active_connections"] = active,
                ["idle_connections"] = idle,
                ["wait_queue_length"] = waitQueue,
                ["last_updated"] = DateTime.UtcNow.Ticks,
                ["instance_id"] = InstanceId
            };

            try
            {
                // Get current max_active to update it if needed
                var currentMaxActive = await _database.HashGetAsync(poolKey, "max_active");
                var maxActive = Math.Max(active, (int)(currentMaxActive.HasValue ? currentMaxActive : 0));
                data["max_active"] = maxActive;

                await _database.HashSetAsync(poolKey, data.Select(kvp => new HashEntry(kvp.Key, kvp.Value)).ToArray());
                await _database.KeyExpireAsync(poolKey, TimeSpan.FromMinutes(10));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record connection pool metric for pool {PoolName}", poolName);
            }
        }
    }
}
