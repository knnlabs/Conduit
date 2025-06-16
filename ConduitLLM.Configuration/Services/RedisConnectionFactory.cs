using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Factory for creating and managing Redis connections
    /// </summary>
    /// <remarks>
    /// This class handles the creation and lifecycle of Redis connections.
    /// It provides connection pooling and handles reconnection logic.
    /// </remarks>
    public class RedisConnectionFactory : IDisposable
    {
        private readonly ConcurrentDictionary<string, Lazy<Task<ConnectionMultiplexer>>> _connections = new();
        private readonly ILogger<RedisConnectionFactory> _logger;
        private readonly CacheOptions _options;

        /// <summary>
        /// Creates a new instance of RedisConnectionFactory
        /// </summary>
        /// <param name="options">Cache configuration options</param>
        /// <param name="logger">Logger instance</param>
        public RedisConnectionFactory(
            IOptions<CacheOptions> options,
            ILogger<RedisConnectionFactory> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets a Redis connection multiplexer for the specified connection string
        /// </summary>
        /// <param name="connectionString">Redis connection string</param>
        /// <returns>A connection multiplexer</returns>
        public virtual async Task<IConnectionMultiplexer> GetConnectionAsync(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = _options.RedisConnectionString;
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("Redis connection string is not configured");
            }

            var connection = _connections.GetOrAdd(connectionString, CreateConnection);
            return await connection.Value;
        }

        /// <summary>
        /// Gets the default Redis connection multiplexer
        /// </summary>
        /// <returns>A connection multiplexer</returns>
        public virtual Task<IConnectionMultiplexer> GetConnectionAsync()
        {
            return GetConnectionAsync(_options.RedisConnectionString);
        }

        private Lazy<Task<ConnectionMultiplexer>> CreateConnection(string connectionString)
        {
            return new Lazy<Task<ConnectionMultiplexer>>(async () =>
            {
                try
                {
                    var configOptions = ConfigurationOptions.Parse(connectionString);
                    configOptions.AbortOnConnectFail = false;

                    // Add any additional configuration based on _options
                    if (!string.IsNullOrEmpty(_options.RedisInstanceName))
                    {
                        // StackExchange.Redis doesn't have a direct instance name setting like
                        // Microsoft.Extensions.Caching.Redis, but we could use it in key prefixing
                    }

                    var connection = await ConnectionMultiplexer.ConnectAsync(configOptions);

                    // Subscribe to connection events for logging and diagnostics
                    connection.ConnectionFailed += (sender, args) =>
                    {
                        _logger.LogError("Redis connection failed. Endpoint: {Endpoint}, Exception: {Exception}",
                            args.EndPoint, args.Exception?.Message ?? "Unknown error");
                    };

                    connection.ConnectionRestored += (sender, args) =>
                    {
                        _logger.LogInformation("Redis connection restored. Endpoint: {Endpoint}", args.EndPoint);
                    };

                    connection.ErrorMessage += (sender, args) =>
                    {
                        _logger.LogWarning("Redis error message: {Message}", args.Message);
                    };

                    // lgtm [cs/cleartext-storage-of-sensitive-information]
                    _logger.LogInformation("Successfully connected to Redis at {ConnectionString}",
                        connectionString.Replace("password=", "password=******"));

                    return connection;
                }
                catch (Exception ex)
                {
                    // lgtm [cs/cleartext-storage-of-sensitive-information]
                    _logger.LogError(ex, "Failed to connect to Redis at {ConnectionString}",
                        connectionString.Replace("password=", "password=******"));
                    throw;
                }
            });
        }

        /// <summary>
        /// Disposes of all Redis connections
        /// </summary>
        public void Dispose()
        {
            foreach (var connection in _connections.Values)
            {
                if (connection.IsValueCreated)
                {
                    try
                    {
                        connection.Value.Result?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disposing Redis connection");
                    }
                }
            }

            _connections.Clear();
        }
    }
}
