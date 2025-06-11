using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for managing pooled connections to audio providers.
    /// </summary>
    public interface IAudioConnectionPool
    {
        /// <summary>
        /// Gets or creates a pooled connection for a provider.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A pooled connection.</returns>
        Task<IAudioProviderConnection> GetConnectionAsync(
            string provider,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a connection to the pool.
        /// </summary>
        /// <param name="connection">The connection to return.</param>
        Task ReturnConnectionAsync(IAudioProviderConnection connection);

        /// <summary>
        /// Gets statistics about the connection pool.
        /// </summary>
        /// <param name="provider">Optional provider to filter by.</param>
        /// <returns>Connection pool statistics.</returns>
        Task<ConnectionPoolStatistics> GetStatisticsAsync(string? provider = null);

        /// <summary>
        /// Clears idle connections from the pool.
        /// </summary>
        /// <param name="maxIdleTime">Maximum idle time before clearing.</param>
        /// <returns>Number of connections cleared.</returns>
        Task<int> ClearIdleConnectionsAsync(TimeSpan maxIdleTime);

        /// <summary>
        /// Warms up the connection pool by pre-creating connections.
        /// </summary>
        /// <param name="provider">The provider to warm up.</param>
        /// <param name="connectionCount">Number of connections to create.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task WarmupAsync(
            string provider,
            int connectionCount,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a pooled connection to an audio provider.
    /// </summary>
    public interface IAudioProviderConnection : IDisposable
    {
        /// <summary>
        /// Gets the provider name.
        /// </summary>
        string Provider { get; }

        /// <summary>
        /// Gets the connection ID.
        /// </summary>
        string ConnectionId { get; }

        /// <summary>
        /// Gets whether the connection is healthy.
        /// </summary>
        bool IsHealthy { get; }

        /// <summary>
        /// Gets when the connection was created.
        /// </summary>
        DateTime CreatedAt { get; }

        /// <summary>
        /// Gets when the connection was last used.
        /// </summary>
        DateTime LastUsedAt { get; }

        /// <summary>
        /// Gets the underlying HTTP client.
        /// </summary>
        HttpClient HttpClient { get; }

        /// <summary>
        /// Validates the connection is still healthy.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if healthy, false otherwise.</returns>
        Task<bool> ValidateAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Statistics about the connection pool.
    /// </summary>
    public class ConnectionPoolStatistics
    {
        /// <summary>
        /// Gets or sets the total connections created.
        /// </summary>
        public int TotalCreated { get; set; }

        /// <summary>
        /// Gets or sets the active connections.
        /// </summary>
        public int ActiveConnections { get; set; }

        /// <summary>
        /// Gets or sets the idle connections.
        /// </summary>
        public int IdleConnections { get; set; }

        /// <summary>
        /// Gets or sets the unhealthy connections.
        /// </summary>
        public int UnhealthyConnections { get; set; }

        /// <summary>
        /// Gets or sets the total requests served.
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Gets or sets the cache hit rate.
        /// </summary>
        public double HitRate { get; set; }

        /// <summary>
        /// Gets or sets per-provider statistics.
        /// </summary>
        public Dictionary<string, ProviderPoolStatistics> ProviderStats { get; set; } = new();
    }

    /// <summary>
    /// Statistics for a specific provider's connection pool.
    /// </summary>
    public class ProviderPoolStatistics
    {
        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of connections.
        /// </summary>
        public int ConnectionCount { get; set; }

        /// <summary>
        /// Gets or sets the number of active connections.
        /// </summary>
        public int ActiveCount { get; set; }

        /// <summary>
        /// Gets or sets the average connection age.
        /// </summary>
        public TimeSpan AverageAge { get; set; }

        /// <summary>
        /// Gets or sets the requests per connection.
        /// </summary>
        public double RequestsPerConnection { get; set; }
    }
}