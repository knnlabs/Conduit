using ConduitLLM.Configuration.DTOs.Security;
using ConduitLLM.Security.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Security.Services
{
    /// <summary>
    /// Security analysis methods for SecurityEventMonitoringService
    /// </summary>
    public partial class SecurityEventMonitoringService
    {
        /// <summary>
        /// Perform security analysis (timer callback)
        /// </summary>
        private void PerformSecurityAnalysis(object? state)
        {
            _ = PerformSecurityAnalysisAsync();
        }

        /// <summary>
        /// Perform comprehensive security analysis
        /// </summary>
        private async Task PerformSecurityAnalysisAsync()
        {
            if (!await _analysisSemaphore.WaitAsync(0))
            {
                _logger.LogDebug("Security analysis already in progress, skipping");
                return;
            }

            try
            {
                await DetectBruteForceAttacks();
                await DetectDistributedAttacks();
                await DetectAnomalousPatterns();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during security analysis");
            }
            finally
            {
                _analysisSemaphore.Release();
            }
        }

        /// <summary>
        /// Detect brute force attacks from single IP addresses
        /// </summary>
        private async Task DetectBruteForceAttacks()
        {
            var windowStart = DateTime.UtcNow.AddMinutes(-_options.BruteForceDetectionWindowMinutes);
            
            foreach (var ipProfile in _ipProfiles.Where(p => !p.Value.IsBanned))
            {
                var recentFailures = _recentEvents
                    .Where(e => e.IpAddress == ipProfile.Key &&
                               e.EventType == SecurityEventType.AuthenticationFailure &&
                               e.Timestamp >= windowStart)
                    .Count();

                if (recentFailures >= _options.BruteForceThreshold)
                {
                    _logger.LogWarning("Brute force attack detected from IP {IpAddress} with {Failures} failures",
                        ipProfile.Key, recentFailures);
                    
                    RecordSuspiciousActivity(ipProfile.Key, "Brute Force Attack",
                        $"Detected {recentFailures} authentication failures in {_options.BruteForceDetectionWindowMinutes} minutes");
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Detect distributed attacks across multiple IP addresses
        /// </summary>
        private async Task DetectDistributedAttacks()
        {
            var windowStart = DateTime.UtcNow.AddMinutes(-_options.DistributedAttackWindowMinutes);
            
            // Group by targeted virtual keys
            var keyTargets = _recentEvents
                .Where(e => e.Timestamp >= windowStart &&
                           e.EventType == SecurityEventType.AuthenticationFailure &&
                           !string.IsNullOrEmpty(e.VirtualKey))
                .GroupBy(e => e.VirtualKey)
                .Where(g => g.Select(e => e.IpAddress).Distinct().Count() >= _options.DistributedAttackIpThreshold)
                .ToList();

            foreach (var target in keyTargets)
            {
                var attackingIps = target.Select(e => e.IpAddress).Distinct().ToList();
                
                _logger.LogWarning("Distributed attack detected on virtual key {VirtualKey} from {IpCount} IPs",
                    target.Key, attackingIps.Count());
                
                foreach (var ip in attackingIps)
                {
                    RecordSuspiciousActivity(ip, "Distributed Attack",
                        $"Part of distributed attack on virtual key from {attackingIps.Count()} IPs");
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Detect anomalous access patterns and endpoint scanning
        /// </summary>
        private async Task DetectAnomalousPatterns()
        {
            var windowMinutes = 15;
            var windowStart = DateTime.UtcNow.AddMinutes(-windowMinutes);
            
            // Detect unusual endpoint access patterns
            foreach (var profile in _ipProfiles.Where(p => !p.Value.IsBanned))
            {
                var identifier = $"ip:{profile.Key}";
                var state = _anomalyStates.AddOrUpdate(identifier,
                    key => new AnomalyDetectionState
                    {
                        Identifier = key,
                        WindowStart = windowStart
                    },
                    (key, existing) =>
                    {
                        if (existing.WindowStart < windowStart.AddMinutes(-windowMinutes))
                        {
                            existing.EndpointAccess.Clear();
                            existing.WindowStart = windowStart;
                            existing.TotalRequests = 0;
                        }
                        return existing;
                    });

                // Count endpoint accesses
                var recentAccesses = _recentEvents
                    .Where(e => e.IpAddress == profile.Key &&
                               e.Timestamp >= state.WindowStart &&
                               !string.IsNullOrEmpty(e.Endpoint))
                    .ToList();

                foreach (var access in recentAccesses)
                {
                    var endpoint = access.Endpoint!; // We know it's not null from the filter above
                    state.EndpointAccess.TryGetValue(endpoint, out var count);
                    state.EndpointAccess[endpoint] = count + 1;
                    state.TotalRequests++;
                }

                // Detect anomalies
                if (state.EndpointAccess.Count() == 0 && state.EndpointAccess.Count() > _options.AnomalousEndpointThreshold &&
                    state.TotalRequests > 50)
                {
                    RecordAnomalousAccess(profile.Key, "", "Endpoint Scanning",
                        $"Accessed {state.EndpointAccess.Count()} different endpoints in {windowMinutes} minutes");
                }
            }

            await Task.CompletedTask;
        }
    }
}