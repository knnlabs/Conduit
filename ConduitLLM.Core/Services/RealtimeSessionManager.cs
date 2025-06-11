using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Background service that manages real-time session lifecycle.
    /// </summary>
    public class RealtimeSessionManager : BackgroundService
    {
        private readonly ILogger<RealtimeSessionManager> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly RealtimeSessionOptions _options;
        private readonly Timer _cleanupTimer;
        private readonly Timer _metricsTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="RealtimeSessionManager"/> class.
        /// </summary>
        public RealtimeSessionManager(
            ILogger<RealtimeSessionManager> logger,
            IServiceProvider serviceProvider,
            IOptions<RealtimeSessionOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));

            _cleanupTimer = new Timer(
                CleanupCallback,
                null,
                Timeout.Infinite,
                Timeout.Infinite);

            _metricsTimer = new Timer(
                MetricsCallback,
                null,
                Timeout.Infinite,
                Timeout.Infinite);
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Real-time session manager started");

            // Start timers
            _cleanupTimer.Change(
                _options.CleanupInterval,
                _options.CleanupInterval);

            _metricsTimer.Change(
                _options.MetricsInterval,
                _options.MetricsInterval);

            // Keep service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        /// <inheritdoc />
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Real-time session manager stopping");

            _cleanupTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _metricsTimer?.Change(Timeout.Infinite, Timeout.Infinite);

            await base.StopAsync(cancellationToken);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            _cleanupTimer?.Dispose();
            _metricsTimer?.Dispose();
            base.Dispose();
        }

        private async void CleanupCallback(object? state)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var sessionStore = scope.ServiceProvider.GetRequiredService<IRealtimeSessionStore>();
                
                var cleaned = await sessionStore.CleanupExpiredSessionsAsync(
                    _options.MaxSessionAge,
                    CancellationToken.None);

                if (cleaned > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} expired sessions", cleaned);
                }

                // Also check for zombie sessions (active but not updated recently)
                await CleanupZombieSessionsAsync(sessionStore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session cleanup");
            }
        }

        private async void MetricsCallback(object? state)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var sessionStore = scope.ServiceProvider.GetRequiredService<IRealtimeSessionStore>();
                var metricsCollector = scope.ServiceProvider.GetRequiredService<IAudioMetricsCollector>();

                var sessions = await sessionStore.GetActiveSessionsAsync(CancellationToken.None);
                
                // Collect aggregate metrics
                var totalSessions = sessions.Count;
                var sessionsByProvider = sessions.GroupBy(s => s.Provider)
                    .ToDictionary(g => g.Key, g => g.Count());
                
                var totalInputDuration = sessions.Sum(s => s.Statistics.InputAudioDuration.TotalSeconds);
                var totalOutputDuration = sessions.Sum(s => s.Statistics.OutputAudioDuration.TotalSeconds);

                _logger.LogInformation(
                    "Real-time sessions: {Total} active, Input: {InputDuration:F1}s, Output: {OutputDuration:F1}s",
                    totalSessions, totalInputDuration, totalOutputDuration);

                // Report to metrics collector
                foreach (var (provider, count) in sessionsByProvider)
                {
                    var providerSessions = sessions.Where(s => s.Provider == provider).ToList();
                    if (providerSessions.Any())
                    {
                        // Report each session individually
                        foreach (var session in providerSessions)
                        {
                            await metricsCollector.RecordRealtimeMetricAsync(new RealtimeMetric
                            {
                                Provider = provider,
                                SessionId = session.Id,
                                SessionDurationSeconds = (DateTime.UtcNow - session.CreatedAt).TotalSeconds,
                                TotalAudioSentSeconds = session.Statistics.InputAudioDuration.TotalSeconds,
                                TotalAudioReceivedSeconds = session.Statistics.OutputAudioDuration.TotalSeconds,
                                TurnCount = session.Statistics.TurnCount
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during metrics collection");
            }
        }

        private async Task CleanupZombieSessionsAsync(IRealtimeSessionStore sessionStore)
        {
            var sessions = await sessionStore.GetActiveSessionsAsync(CancellationToken.None);
            var zombieThreshold = DateTime.UtcNow - _options.ZombieSessionThreshold;
            var zombies = new List<RealtimeSession>();

            foreach (var session in sessions)
            {
                // Check if session hasn't been updated recently
                var lastActivity = session.Statistics.Duration > TimeSpan.Zero
                    ? session.CreatedAt + session.Statistics.Duration
                    : session.CreatedAt;

                if (lastActivity < zombieThreshold && session.State != SessionState.Closed)
                {
                    zombies.Add(session);
                }
            }

            if (zombies.Any())
            {
                _logger.LogWarning("Found {Count} zombie sessions", zombies.Count);

                foreach (var zombie in zombies)
                {
                    zombie.State = SessionState.Error;
                    zombie.Statistics.ErrorCount++;
                    
                    await sessionStore.UpdateSessionAsync(zombie, CancellationToken.None);
                    
                    // Optionally terminate the zombie session
                    if (_options.AutoTerminateZombies)
                    {
                        await sessionStore.RemoveSessionAsync(zombie.Id, CancellationToken.None);
                        _logger.LogInformation("Terminated zombie session {SessionId}", zombie.Id);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Options for real-time session management.
    /// </summary>
    public class RealtimeSessionOptions
    {
        /// <summary>
        /// Gets or sets the interval for cleanup operations.
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the interval for metrics collection.
        /// </summary>
        public TimeSpan MetricsInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets the maximum age for sessions before cleanup.
        /// </summary>
        public TimeSpan MaxSessionAge { get; set; } = TimeSpan.FromHours(2);

        /// <summary>
        /// Gets or sets the threshold for identifying zombie sessions.
        /// </summary>
        public TimeSpan ZombieSessionThreshold { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Gets or sets whether to automatically terminate zombie sessions.
        /// </summary>
        public bool AutoTerminateZombies { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of concurrent sessions per virtual key.
        /// </summary>
        public int MaxSessionsPerVirtualKey { get; set; } = 10;

        /// <summary>
        /// Gets or sets whether to enable session persistence across restarts.
        /// </summary>
        public bool EnablePersistence { get; set; } = true;
    }
}