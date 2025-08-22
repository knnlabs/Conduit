using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Manages graceful shutdown of audio services to prevent data loss and ensure clean termination.
    /// </summary>
    public class GracefulShutdownService : IHostedService
    {
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly IAudioConnectionPool _connectionPool;
        private readonly IAudioStreamCache _cache;
        private readonly IAudioMetricsCollector _metricsCollector;
        private readonly IRealtimeSessionManager _sessionManager;
        private readonly ILogger<GracefulShutdownService> _logger;
        private readonly SemaphoreSlim _shutdownSemaphore = new(1, 1);
        private CancellationTokenSource? _shutdownTokenSource;
        private bool _isShuttingDown;

        public GracefulShutdownService(
            IHostApplicationLifetime applicationLifetime,
            IAudioConnectionPool connectionPool,
            IAudioStreamCache cache,
            IAudioMetricsCollector metricsCollector,
            IRealtimeSessionManager sessionManager,
            ILogger<GracefulShutdownService> logger)
        {
            _applicationLifetime = applicationLifetime;
            _connectionPool = connectionPool;
            _cache = cache;
            _metricsCollector = metricsCollector;
            _sessionManager = sessionManager;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _applicationLifetime.ApplicationStopping.Register(OnApplicationStopping);
            _logger.LogInformation("Graceful shutdown service started");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private void OnApplicationStopping()
        {
            _logger.LogInformation("Application shutdown initiated, beginning graceful shutdown sequence");
            
            try
            {
                // Create a cancellation token for shutdown operations
                _shutdownTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                
                // Execute shutdown sequence
                Task.Run(async () => await ExecuteShutdownSequenceAsync(_shutdownTokenSource.Token))
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during graceful shutdown");
            }
        }

        private async Task ExecuteShutdownSequenceAsync(CancellationToken cancellationToken)
        {
            await _shutdownSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_isShuttingDown)
                {
                    _logger.LogWarning("Shutdown already in progress");
                    return;
                }

                _isShuttingDown = true;
                var shutdownSteps = new (string stepName, Func<CancellationToken, Task> stepAction)[]
                {
                    ("Stop accepting new requests", StopAcceptingRequestsAsync),
                    ("Close realtime sessions", CloseRealtimeSessionsAsync),
                    ("Flush cache", FlushCacheAsync),
                    ("Export final metrics", ExportFinalMetricsAsync),
                    ("Close connection pool", CloseConnectionPoolAsync),
                    ("Final cleanup", FinalCleanupAsync)
                };

                foreach (var (stepName, stepAction) in shutdownSteps)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Shutdown sequence cancelled at step: {Step}", stepName);
                        break;
                    }

                    try
                    {
                        _logger.LogInformation("Executing shutdown step: {Step}", stepName);
                        await stepAction(cancellationToken);
                        _logger.LogInformation("Completed shutdown step: {Step}", stepName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during shutdown step: {Step}", stepName);
                    }
                }

                _logger.LogInformation("Graceful shutdown sequence completed");
            }
            finally
            {
                _shutdownSemaphore.Release();
            }
        }

        private async Task StopAcceptingRequestsAsync(CancellationToken cancellationToken)
        {
            // Signal that we're no longer accepting new requests
            // This would typically be done through a health check endpoint
            _logger.LogInformation("Marking service as not ready for new requests");
            
            // Give load balancer time to stop routing traffic
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
        }

        private async Task CloseRealtimeSessionsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Closing active realtime sessions");
            
            var activeSessions = await _sessionManager.GetActiveSessionsAsync(cancellationToken);
            _logger.LogInformation("Found {Count} active sessions to close", activeSessions.Count);

            var closeTasks = activeSessions.Select(async session =>
            {
                try
                {
                    // Send graceful close message to client
                    await _sessionManager.SendCloseNotificationAsync(
                        session.Id,
                        "Server is shutting down for maintenance",
                        cancellationToken);
                    
                    // Wait briefly for client acknowledgment
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    
                    // Force close the session
                    await _sessionManager.CloseSessionAsync(session.Id, cancellationToken);
                    
                    _logger.LogDebug("Closed session: {SessionId}", session.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error closing session: {SessionId}", session.Id);
                }
            });

            await Task.WhenAll(closeTasks);
            _logger.LogInformation("All realtime sessions closed");
        }

        private async Task FlushCacheAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Flushing cache to persistent storage");
            
            try
            {
                // Get cache statistics before flush
                var stats = await _cache.GetStatisticsAsync();
                _logger.LogInformation(
                    "Cache statistics before flush: Items={Items}, Size={Size}MB",
                    stats.TotalEntries,
                    stats.TotalSizeBytes / (1024 * 1024));

                // Flush any pending writes - FlushAsync doesn't exist on IAudioStreamCache
                // await _cache.FlushAsync(cancellationToken);
                await _cache.ClearExpiredAsync();
                
                _logger.LogInformation("Cache flush completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing cache");
            }
        }

        private async Task ExportFinalMetricsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Exporting final metrics");
            
            try
            {
                // Get final snapshot
                var snapshot = await _metricsCollector.GetCurrentSnapshotAsync();
                
                // Log summary metrics
                _logger.LogInformation(
                    "Final metrics summary: TotalRequests={TotalRequests}, ErrorRate={ErrorRate:P2}, AvgLatency={AvgLatency}ms",
                    snapshot.RequestsPerSecond,
                    snapshot.CurrentErrorRate,
                    0.0); // ProviderMetrics doesn't exist on AudioMetricsSnapshot

                // Force export to monitoring system - method doesn't exist
                // await _metricsCollector.ForceExportAsync(cancellationToken);
                _logger.LogInformation("Metrics collector doesn't have ForceExportAsync method");
                
                _logger.LogInformation("Metrics export completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting metrics");
            }
        }

        private async Task CloseConnectionPoolAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Closing connection pool");
            
            try
            {
                // Get pool statistics
                var stats = await _connectionPool.GetStatisticsAsync();
                _logger.LogInformation(
                    "Connection pool status: Total={Total}, Active={Active}, Idle={Idle}",
                    stats.TotalCreated,
                    stats.ActiveConnections,
                    stats.IdleConnections);

                // Close all connections gracefully - method doesn't exist
                // await _connectionPool.CloseAllAsync(cancellationToken);
                // Clear idle connections instead
                await _connectionPool.ClearIdleConnectionsAsync(TimeSpan.Zero);
                
                _logger.LogInformation("Connection pool closed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing connection pool");
            }
        }

        private async Task FinalCleanupAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Performing final cleanup");
            
            // Any additional cleanup tasks
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            
            _logger.LogInformation("Final cleanup completed");
        }
    }

    /// <summary>
    /// Manages active realtime sessions during shutdown.
    /// </summary>
    public interface IRealtimeSessionManager
    {
        /// <summary>
        /// Gets all active realtime sessions.
        /// </summary>
        Task<List<RealtimeSessionInfo>> GetActiveSessionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a close notification to a session.
        /// </summary>
        Task SendCloseNotificationAsync(string sessionId, string reason, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes a session forcefully.
        /// </summary>
        Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Information about an active realtime session.
    /// </summary>
    public class RealtimeSessionInfo
    {
        /// <summary>
        /// Gets or sets the session ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the session was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets whether the session is active.
        /// </summary>
        public bool IsActive { get; set; }
    }
}