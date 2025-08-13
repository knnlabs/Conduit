using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Implementation of connection pooling for audio providers.
    /// </summary>
    public class AudioConnectionPool : IAudioConnectionPool, IDisposable
    {
        private readonly ILogger<AudioConnectionPool> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AudioConnectionPoolOptions _options;
        private readonly ConcurrentDictionary<string, ProviderConnectionPool> _pools = new();
        private readonly Timer _cleanupTimer;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioConnectionPool"/> class.
        /// </summary>
        public AudioConnectionPool(
            ILogger<AudioConnectionPool> logger,
            IHttpClientFactory httpClientFactory,
            IOptions<AudioConnectionPoolOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            ArgumentNullException.ThrowIfNull(options);
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));

            // Start cleanup timer
            _cleanupTimer = new Timer(
                CleanupCallback,
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(1));
        }

        /// <inheritdoc />
        public async Task<IAudioProviderConnection> GetConnectionAsync(
            string provider,
            CancellationToken cancellationToken = default)
        {
            var pool = _pools.GetOrAdd(provider, p => new ProviderConnectionPool(p, _options));

            // Try to get an existing healthy connection
            if (pool.TryGetConnection(out var connection) && connection?.IsHealthy == true)
            {
                _logger.LogDebug("Reusing connection {ConnectionId} for {Provider}", connection.ConnectionId, provider);
                return connection;
            }

            // Create a new connection
            connection = await CreateConnectionAsync(provider, cancellationToken);
            pool.AddConnection(connection);

            _logger.LogInformation("Created new connection {ConnectionId} for {Provider}", connection.ConnectionId, provider);
            return connection;
        }

        /// <inheritdoc />
        public Task ReturnConnectionAsync(IAudioProviderConnection connection)
        {
            if (connection == null)
            {
                return Task.CompletedTask;
            }

            if (_pools.TryGetValue(connection.Provider, out var pool))
            {
                pool.ReturnConnection(connection);
                _logger.LogDebug("Returned connection {ConnectionId} to pool", connection.ConnectionId);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<ConnectionPoolStatistics> GetStatisticsAsync(string? provider = null)
        {
            var stats = new ConnectionPoolStatistics();

            var pools = provider != null && _pools.TryGetValue(provider, out var pool)
                ? new[] { pool }
                : _pools.Values.ToArray();

            foreach (var p in pools)
            {
                var poolStats = p.GetStatistics();
                stats.TotalCreated += poolStats.TotalCreated;
                stats.ActiveConnections += poolStats.ActiveConnections;
                stats.IdleConnections += poolStats.IdleConnections;
                stats.UnhealthyConnections += poolStats.UnhealthyConnections;
                stats.TotalRequests += poolStats.TotalRequests;

                stats.ProviderStats[p.Provider] = new ProviderPoolStatistics
                {
                    Provider = p.Provider,
                    ConnectionCount = poolStats.TotalCreated,
                    ActiveCount = poolStats.ActiveConnections,
                    AverageAge = poolStats.AverageAge,
                    RequestsPerConnection = poolStats.TotalCreated > 0
                        ? (double)poolStats.TotalRequests / poolStats.TotalCreated
                        : 0
                };
            }

            stats.HitRate = stats.TotalRequests > 0 && stats.TotalCreated > 0
                ? 1.0 - ((double)stats.TotalCreated / stats.TotalRequests)
                : 0;

            return Task.FromResult(stats);
        }

        /// <inheritdoc />
        public async Task<int> ClearIdleConnectionsAsync(TimeSpan maxIdleTime)
        {
            var totalCleared = 0;

            foreach (var pool in _pools.Values)
            {
                var cleared = await pool.ClearIdleConnectionsAsync(maxIdleTime);
                totalCleared += cleared;
            }

            if (totalCleared > 0)
            {
                _logger.LogInformation("Cleared {Count} idle connections", totalCleared);
            }

            return totalCleared;
        }

        /// <inheritdoc />
        public async Task WarmupAsync(
            string provider,
            int connectionCount,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Warming up {Count} connections for {Provider}", connectionCount, provider);

            var pool = _pools.GetOrAdd(provider, p => new ProviderConnectionPool(p, _options));
            var tasks = new List<Task>();

            for (int i = 0; i < connectionCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var connection = await CreateConnectionAsync(provider, cancellationToken);
                        pool.AddConnection(connection);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create warmup connection for {Provider}", provider);
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
            _logger.LogInformation("Warmup completed for {Provider}", provider);
        }

        private async Task<AudioProviderConnection> CreateConnectionAsync(
            string provider,
            CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient($"AudioProvider_{provider}");

            // Configure HTTP client
            httpClient.Timeout = TimeSpan.FromSeconds(_options.ConnectionTimeout);
            httpClient.DefaultRequestHeaders.Add("X-Provider", provider);

            var connection = new AudioProviderConnection(provider, httpClient);

            // Validate the connection
            if (!await connection.ValidateAsync(cancellationToken))
            {
                throw new InvalidOperationException($"Failed to create healthy connection for {provider}");
            }

            return connection;
        }

        private void CleanupCallback(object? state)
        {
            try
            {
                var task = ClearIdleConnectionsAsync(_options.MaxIdleTime);
                task.Wait(TimeSpan.FromSeconds(30));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connection pool cleanup");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();

                foreach (var pool in _pools.Values)
                {
                    pool.Dispose();
                }

                _pools.Clear();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Connection pool for a specific provider.
    /// </summary>
    internal class ProviderConnectionPool : IDisposable
    {
        private readonly string _provider;
        private readonly AudioConnectionPoolOptions _options;
        private readonly ConcurrentBag<AudioProviderConnection> _connections = new();
        private readonly ConcurrentDictionary<string, AudioProviderConnection> _activeConnections = new();
        private long _totalCreated;
        private long _totalRequests;

        public string Provider => _provider;

        public ProviderConnectionPool(string provider, AudioConnectionPoolOptions options)
        {
            _provider = provider;
            _options = options;
        }

        public bool TryGetConnection(out AudioProviderConnection? connection)
        {
            Interlocked.Increment(ref _totalRequests);

            while (_connections.TryTake(out connection))
            {
                if (connection.IsHealthy && !IsExpired(connection))
                {
                    _activeConnections[connection.ConnectionId] = connection;
                    return true;
                }

                connection.Dispose();
            }

            connection = null;
            return false;
        }

        public void AddConnection(AudioProviderConnection connection)
        {
            Interlocked.Increment(ref _totalCreated);
            _activeConnections[connection.ConnectionId] = connection;
        }

        public void ReturnConnection(IAudioProviderConnection connection)
        {
            if (connection is AudioProviderConnection conn &&
                _activeConnections.TryRemove(conn.ConnectionId, out _))
            {
                if (conn.IsHealthy && !IsExpired(conn) && _connections.Count < _options.MaxConnectionsPerProvider)
                {
                    _connections.Add(conn);
                }
                else
                {
                    conn.Dispose();
                }
            }
        }

        public PoolStatistics GetStatistics()
        {
            var connections = _connections.ToArray();
            var now = DateTime.UtcNow;

            return new PoolStatistics
            {
                TotalCreated = (int)_totalCreated,
                ActiveConnections = _activeConnections.Count,
                IdleConnections = connections.Length,
                UnhealthyConnections = connections.Count(c => !c.IsHealthy),
                TotalRequests = _totalRequests,
                AverageAge = connections.Length > 0
                    ? TimeSpan.FromMilliseconds(connections.Average(c => (now - c.CreatedAt).TotalMilliseconds))
                    : TimeSpan.Zero
            };
        }

        public Task<int> ClearIdleConnectionsAsync(TimeSpan maxIdleTime)
        {
            var cleared = 0;
            var now = DateTime.UtcNow;
            var toDispose = new List<AudioProviderConnection>();

            // Check idle connections
            var connections = _connections.ToArray();
            foreach (var conn in connections)
            {
                if (now - conn.LastUsedAt > maxIdleTime)
                {
                    if (_connections.TryTake(out var removed) && removed.ConnectionId == conn.ConnectionId)
                    {
                        toDispose.Add(removed);
                        cleared++;
                    }
                }
            }

            // Dispose connections
            foreach (var conn in toDispose)
            {
                conn.Dispose();
            }

            return Task.FromResult(cleared);
        }

        private bool IsExpired(AudioProviderConnection connection)
        {
            return DateTime.UtcNow - connection.CreatedAt > _options.MaxConnectionAge;
        }

        public void Dispose()
        {
            foreach (var conn in _connections)
            {
                conn.Dispose();
            }

            foreach (var conn in _activeConnections.Values)
            {
                conn.Dispose();
            }

            _connections.Clear();
            _activeConnections.Clear();
        }
    }

    /// <summary>
    /// Implementation of audio provider connection.
    /// </summary>
    internal class AudioProviderConnection : IAudioProviderConnection
    {
        private readonly HttpClient _httpClient;
        private bool _disposed;

        public string Provider { get; }
        public string ConnectionId { get; }
        public bool IsHealthy { get; private set; }
        public DateTime CreatedAt { get; }
        public DateTime LastUsedAt { get; private set; }
        public HttpClient HttpClient => _httpClient;

        public AudioProviderConnection(string provider, HttpClient httpClient)
        {
            Provider = provider;
            ConnectionId = Guid.NewGuid().ToString();
            _httpClient = httpClient;
            CreatedAt = DateTime.UtcNow;
            LastUsedAt = DateTime.UtcNow;
            IsHealthy = true;
        }

        public async Task<bool> ValidateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Simple health check - adjust based on provider
                var response = await _httpClient.GetAsync("/health", cancellationToken);
                IsHealthy = response.IsSuccessStatusCode;
                LastUsedAt = DateTime.UtcNow;
                return IsHealthy;
            }
            catch
            {
                IsHealthy = false;
                return false;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Internal pool statistics.
    /// </summary>
    internal class PoolStatistics
    {
        public int TotalCreated { get; set; }
        public int ActiveConnections { get; set; }
        public int IdleConnections { get; set; }
        public int UnhealthyConnections { get; set; }
        public long TotalRequests { get; set; }
        public TimeSpan AverageAge { get; set; }
    }

    /// <summary>
    /// Options for audio connection pooling.
    /// </summary>
    public class AudioConnectionPoolOptions
    {
        /// <summary>
        /// Gets or sets the maximum connections per provider.
        /// </summary>
        public int MaxConnectionsPerProvider { get; set; } = 10;

        /// <summary>
        /// Gets or sets the maximum connection age.
        /// </summary>
        public TimeSpan MaxConnectionAge { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets or sets the maximum idle time before cleanup.
        /// </summary>
        public TimeSpan MaxIdleTime { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Gets or sets the connection timeout in seconds.
        /// </summary>
        public int ConnectionTimeout { get; set; } = 30;
    }
}
