using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Redis-based implementation of leader election service using distributed locks
    /// </summary>
    public class RedisLeaderElectionService : ILeaderElectionService, IDisposable
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisLeaderElectionService> _logger;
        private readonly string _instanceId;
        private readonly TimeSpan _leadershipTtl;
        private readonly TimeSpan _renewalInterval;
        private readonly ConcurrentDictionary<string, Timer> _renewalTimers;
        private readonly ConcurrentDictionary<string, bool> _activeLeaderships;
        private bool _disposed;

        public RedisLeaderElectionService(
            IConnectionMultiplexer redis,
            ILogger<RedisLeaderElectionService> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Generate unique instance ID
            _instanceId = $"{Environment.MachineName}:{Process.GetCurrentProcess().Id}:{Guid.NewGuid():N}";
            
            // Configure TTL and renewal intervals
            _leadershipTtl = TimeSpan.FromSeconds(60);
            _renewalInterval = TimeSpan.FromSeconds(30); // Renew at half the TTL
            
            _renewalTimers = new ConcurrentDictionary<string, Timer>();
            _activeLeaderships = new ConcurrentDictionary<string, bool>();
            
            _logger.LogInformation("Leader election service initialized with instance ID: {InstanceId}", _instanceId);
        }

        /// <inheritdoc/>
        public async Task<bool> TryAcquireLeadershipAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            var lockKey = GetLockKey(serviceName);
            var db = _redis.GetDatabase();

            try
            {
                // Try to acquire the lock with SET NX (only if not exists) and EX (expiration)
                var acquired = await db.StringSetAsync(
                    lockKey,
                    _instanceId,
                    _leadershipTtl,
                    When.NotExists,
                    CommandFlags.None);

                if (acquired)
                {
                    _activeLeaderships[serviceName] = true;
                    _logger.LogInformation(
                        "Leadership acquired for service {ServiceName} by instance {InstanceId}",
                        serviceName, _instanceId);
                    
                    // Start automatic renewal
                    await StartLeadershipMaintenanceAsync(serviceName, cancellationToken);
                }
                else
                {
                    var currentLeader = await db.StringGetAsync(lockKey);
                    _logger.LogDebug(
                        "Failed to acquire leadership for service {ServiceName}. Current leader: {CurrentLeader}",
                        serviceName, currentLeader);
                }

                return acquired;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error acquiring leadership for service {ServiceName}", serviceName);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task ReleaseLeadershipAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            var lockKey = GetLockKey(serviceName);
            var db = _redis.GetDatabase();

            try
            {
                // Stop renewal timer first
                await StopLeadershipMaintenanceAsync(serviceName);

                // Only delete if we own the lock (atomic check and delete)
                var script = @"
                    if redis.call('get', KEYS[1]) == ARGV[1] then
                        return redis.call('del', KEYS[1])
                    else
                        return 0
                    end";

                var result = await db.ScriptEvaluateAsync(
                    script,
                    new RedisKey[] { lockKey },
                    new RedisValue[] { _instanceId });

                if ((long)result == 1)
                {
                    _activeLeaderships.TryRemove(serviceName, out _);
                    _logger.LogInformation(
                        "Leadership released for service {ServiceName} by instance {InstanceId}",
                        serviceName, _instanceId);
                }
                else
                {
                    _logger.LogWarning(
                        "Could not release leadership for service {ServiceName} - not the current leader",
                        serviceName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing leadership for service {ServiceName}", serviceName);
            }
        }

        /// <inheritdoc/>
        public async Task<bool> IsLeaderAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            var lockKey = GetLockKey(serviceName);
            var db = _redis.GetDatabase();

            try
            {
                var currentLeader = await db.StringGetAsync(lockKey);
                var isLeader = currentLeader == _instanceId;
                
                // Update local cache
                _activeLeaderships[serviceName] = isLeader;
                
                return isLeader;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking leadership status for service {ServiceName}", serviceName);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<string?> GetCurrentLeaderAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            var lockKey = GetLockKey(serviceName);
            var db = _redis.GetDatabase();

            try
            {
                var currentLeader = await db.StringGetAsync(lockKey);
                return currentLeader.HasValue ? currentLeader.ToString() : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current leader for service {ServiceName}", serviceName);
                return null;
            }
        }

        /// <inheritdoc/>
        public Task StartLeadershipMaintenanceAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            // Stop any existing timer
            StopLeadershipMaintenanceAsync(serviceName).Wait();

            // Create new renewal timer
            var timer = new Timer(
                async _ => await RenewLeadershipAsync(serviceName),
                null,
                _renewalInterval,
                _renewalInterval);

            _renewalTimers[serviceName] = timer;
            
            _logger.LogDebug(
                "Started leadership maintenance for service {ServiceName} with renewal interval {RenewalInterval}",
                serviceName, _renewalInterval);

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task StopLeadershipMaintenanceAsync(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            if (_renewalTimers.TryRemove(serviceName, out var timer))
            {
                timer?.Dispose();
                _logger.LogDebug("Stopped leadership maintenance for service {ServiceName}", serviceName);
            }

            return Task.CompletedTask;
        }

        private async Task RenewLeadershipAsync(string serviceName)
        {
            var lockKey = GetLockKey(serviceName);
            var db = _redis.GetDatabase();

            try
            {
                // Atomic check and extend TTL
                var script = @"
                    if redis.call('get', KEYS[1]) == ARGV[1] then
                        return redis.call('expire', KEYS[1], ARGV[2])
                    else
                        return 0
                    end";

                var result = await db.ScriptEvaluateAsync(
                    script,
                    new RedisKey[] { lockKey },
                    new RedisValue[] { _instanceId, (int)_leadershipTtl.TotalSeconds });

                if ((long)result == 1)
                {
                    _logger.LogTrace(
                        "Leadership renewed for service {ServiceName} by instance {InstanceId}",
                        serviceName, _instanceId);
                }
                else
                {
                    // Lost leadership
                    _activeLeaderships.TryRemove(serviceName, out _);
                    await StopLeadershipMaintenanceAsync(serviceName);
                    
                    _logger.LogWarning(
                        "Lost leadership for service {ServiceName}. Stopping renewal.",
                        serviceName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renewing leadership for service {ServiceName}", serviceName);
                
                // On error, check if we're still the leader
                if (!await IsLeaderAsync(serviceName))
                {
                    _activeLeaderships.TryRemove(serviceName, out _);
                    await StopLeadershipMaintenanceAsync(serviceName);
                }
            }
        }

        private string GetLockKey(string serviceName)
        {
            return $"leader:{serviceName}";
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Release all active leaderships
            foreach (var serviceName in _activeLeaderships.Keys)
            {
                try
                {
                    ReleaseLeadershipAsync(serviceName).Wait(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error releasing leadership for service {ServiceName} during disposal", serviceName);
                }
            }

            // Dispose all timers
            foreach (var timer in _renewalTimers.Values)
            {
                timer?.Dispose();
            }

            _renewalTimers.Clear();
            _activeLeaderships.Clear();
        }
    }
}