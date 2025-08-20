using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Configuration.DTOs.Security;
using ConduitLLM.Security.Interfaces;
using ConduitLLM.Security.Models;

namespace ConduitLLM.Security.Services
{
    /// <summary>
    /// Implementation of security event monitoring service
    /// </summary>
    public partial class SecurityEventMonitoringService : ISecurityEventMonitoringService, IHostedService, IDisposable
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<SecurityEventMonitoringService> _logger;
        private readonly SecurityMonitoringOptions _options;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        // Event storage
        private readonly ConcurrentQueue<SecurityEvent> _recentEvents;
        private readonly ConcurrentDictionary<string, IpActivityProfile> _ipProfiles;
        private readonly ConcurrentDictionary<string, VirtualKeyActivityProfile> _keyProfiles;
        private readonly ConcurrentDictionary<string, AnomalyDetectionState> _anomalyStates;

        private Timer? _analysisTimer;
        private Timer? _cleanupTimer;
        private readonly SemaphoreSlim _analysisSemaphore;

        public SecurityEventMonitoringService(
            IMemoryCache cache,
            ILogger<SecurityEventMonitoringService> logger,
            IOptions<SecurityMonitoringOptions> options,
            IServiceScopeFactory serviceScopeFactory)
        {
            _cache = cache;
            _logger = logger;
            _options = options.Value;
            _serviceScopeFactory = serviceScopeFactory;

            _recentEvents = new ConcurrentQueue<SecurityEvent>();
            _ipProfiles = new ConcurrentDictionary<string, IpActivityProfile>();
            _keyProfiles = new ConcurrentDictionary<string, VirtualKeyActivityProfile>();
            _anomalyStates = new ConcurrentDictionary<string, AnomalyDetectionState>();
            _analysisSemaphore = new SemaphoreSlim(1, 1);
        }



        /// <summary>
        /// Start the background analysis service
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Security Event Monitoring Service");

            // Start periodic analysis
            _analysisTimer = new Timer(
                PerformSecurityAnalysis,
                null,
                TimeSpan.FromSeconds(_options.AnalysisIntervalSeconds),
                TimeSpan.FromSeconds(_options.AnalysisIntervalSeconds));

            // Start cleanup timer
            _cleanupTimer = new Timer(
                PerformCleanup,
                null,
                TimeSpan.FromHours(1),
                TimeSpan.FromHours(1));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop the background service
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Security Event Monitoring Service");

            _analysisTimer?.Change(Timeout.Infinite, 0);
            _cleanupTimer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            _analysisTimer?.Dispose();
            _cleanupTimer?.Dispose();
            _analysisSemaphore?.Dispose();
        }

        /// <summary>
        /// Helper method to update IP activity profiles
        /// </summary>
        private void UpdateIpProfile(string ipAddress, Action<IpActivityProfile> updateAction)
        {
            _ipProfiles.AddOrUpdate(ipAddress,
                key =>
                {
                    var profile = new IpActivityProfile { IpAddress = key };
                    updateAction(profile);
                    return profile;
                },
                (key, existing) =>
                {
                    updateAction(existing);
                    existing.LastActivity = DateTime.UtcNow;
                    return existing;
                });
        }

        /// <summary>
        /// Helper method to update virtual key activity profiles
        /// </summary>
        private void UpdateKeyProfile(string virtualKey, Action<VirtualKeyActivityProfile> updateAction)
        {
            _keyProfiles.AddOrUpdate(virtualKey,
                key =>
                {
                    var profile = new VirtualKeyActivityProfile { VirtualKey = key };
                    updateAction(profile);
                    return profile;
                },
                (key, existing) =>
                {
                    updateAction(existing);
                    existing.LastActivity = DateTime.UtcNow;
                    return existing;
                });
        }

        /// <summary>
        /// Helper method to trim the event queue to maximum retention size
        /// </summary>
        private void TrimEventQueue()
        {
            while (_recentEvents.Count() > _options.MaxEventsRetention)
            {
                _recentEvents.TryDequeue(out _);
            }
        }


        /// <summary>
        /// Perform cleanup of old security monitoring data
        /// </summary>
        private void PerformCleanup(object? state)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddHours(-_options.DataRetentionHours);
                
                // Clean old events
                var eventsToKeep = new List<SecurityEvent>();
                while (_recentEvents.TryDequeue(out var evt))
                {
                    if (evt.Timestamp >= cutoff)
                    {
                        eventsToKeep.Add(evt);
                    }
                }
                
                foreach (var evt in eventsToKeep)
                {
                    _recentEvents.Enqueue(evt);
                }

                // Clean inactive IP profiles
                var inactiveIps = _ipProfiles
                    .Where(p => p.Value.LastActivity < cutoff && !p.Value.IsBanned)
                    .Select(p => p.Key)
                    .ToList();

                foreach (var ip in inactiveIps)
                {
                    _ipProfiles.TryRemove(ip, out _);
                }

                // Clean inactive key profiles
                var inactiveKeys = _keyProfiles
                    .Where(p => p.Value.LastActivity < cutoff)
                    .Select(p => p.Key)
                    .ToList();

                foreach (var key in inactiveKeys)
                {
                    _keyProfiles.TryRemove(key, out _);
                }

                _logger.LogInformation("Security monitoring cleanup completed. Removed {IpCount} inactive IPs and {KeyCount} inactive keys",
                    inactiveIps.Count(), inactiveKeys.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during security monitoring cleanup");
            }
        }
    }
}