using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace ConduitLLM.Configuration.HealthChecks
{
    /// <summary>
    /// Health check for Redis cache connectivity.
    /// </summary>
    public class RedisHealthCheck : IHealthCheck
    {
        private readonly string _connectionString;
        private readonly ILogger<RedisHealthCheck> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisHealthCheck"/> class.
        /// </summary>
        /// <param name="connectionString">Redis connection string.</param>
        /// <param name="logger">Logger instance.</param>
        public RedisHealthCheck(string connectionString, ILogger<RedisHealthCheck> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        /// <summary>
        /// Performs the health check.
        /// </summary>
        /// <param name="context">Health check context.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Health check result.</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var options = ConfigurationOptions.Parse(_connectionString);
                options.ConnectTimeout = 5000; // 5 second timeout
                options.AbortOnConnectFail = false;

                using var connection = await ConnectionMultiplexer.ConnectAsync(options);

                if (!connection.IsConnected)
                {
                    return HealthCheckResult.Unhealthy("Redis connection is not established");
                }

                var database = connection.GetDatabase();
                var endpoints = connection.GetEndPoints();
                var healthData = new Dictionary<string, object>();

                // Ping each endpoint
                foreach (var endpoint in endpoints)
                {
                    var server = connection.GetServer(endpoint);
                    var ping = await database.PingAsync();

                    healthData[$"endpoint_{endpoint}"] = new
                    {
                        connected = server.IsConnected,
                        responseTime = ping.TotalMilliseconds,
                        serverType = server.ServerType.ToString(),
                        version = server.Version?.ToString() ?? "Unknown"
                    };
                }

                // Check if Redis is in a degraded state
                var servers = endpoints.Select(ep => connection.GetServer(ep)).ToList();
                var connectedServers = servers.Count(s => s.IsConnected);

                if (connectedServers == 0)
                {
                    return HealthCheckResult.Unhealthy("No Redis servers are connected", data: healthData);
                }

                if (connectedServers < servers.Count())
                {
                    return HealthCheckResult.Degraded(
                        $"Only {connectedServers} of {servers.Count()} Redis servers are connected",
                        data: healthData);
                }

                // Test basic operations
                var testKey = $"health_check_{Guid.NewGuid():N}";
                await database.StringSetAsync(testKey, "test", TimeSpan.FromSeconds(10));
                var testValue = await database.StringGetAsync(testKey);
                await database.KeyDeleteAsync(testKey);

                if (testValue != "test")
                {
                    return HealthCheckResult.Degraded("Redis read/write test failed", data: healthData);
                }

                return HealthCheckResult.Healthy(
                    $"Redis is healthy ({connectedServers} servers connected)",
                    data: healthData);
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogError(ex, "Redis connection failed");
                return HealthCheckResult.Unhealthy("Redis connection failed", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during Redis health check");
                return HealthCheckResult.Unhealthy("Redis health check failed", ex);
            }
        }
    }
}
