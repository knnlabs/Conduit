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
    /// Redis-based implementation of leader election service using distributed locks.
    /// Implements fencing tokens to prevent split-brain scenarios.
    /// </summary>
    public class RedisLeaderElectionService : ILeaderElectionService, IAsyncDisposable, IDisposable
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisLeaderElectionService> _logger;
        private readonly string _instanceId;
        private readonly TimeSpan _leadershipTtl;
        private readonly TimeSpan _renewalInterval;
        private readonly ConcurrentDictionary<string, MaintenanceState> _maintenanceStates;
        private readonly ConcurrentDictionary<string, LeadershipState> _leadershipStates;
        private readonly SemaphoreSlim _disposeLock;
        private bool _disposed;

        /// <summary>
        /// Tracks the state of leadership maintenance for a service
        /// </summary>
        private sealed class MaintenanceState : IDisposable
        {
            public CancellationTokenSource CancellationTokenSource { get; }
            public Task? MaintenanceTask { get; set; }
            private bool _disposed;

            public MaintenanceState()
            {
                CancellationTokenSource = new CancellationTokenSource();
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                CancellationTokenSource.Cancel();
                CancellationTokenSource.Dispose();
            }
        }

        /// <summary>
        /// Tracks the leadership state including fencing token
        /// </summary>
        private sealed class LeadershipState
        {
            public bool IsLeader { get; set; }
            public long FencingToken { get; set; }
            public DateTime LastRenewal { get; set; }
        }

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

            _maintenanceStates = new ConcurrentDictionary<string, MaintenanceState>();
            _leadershipStates = new ConcurrentDictionary<string, LeadershipState>();
            _disposeLock = new SemaphoreSlim(1, 1);

            _logger.LogInformation("Leader election service initialized with instance ID: {InstanceId}", _instanceId);
        }

        /// <inheritdoc/>
        public async Task<LeadershipAcquisitionResult> TryAcquireLeadershipAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            var lockKey = GetLockKey(serviceName);
            var fencingKey = GetFencingKey(serviceName);

            try
            {
                var db = _redis.GetDatabase();

                // Lua script to atomically:
                // 1. Try to acquire the lock (SET NX EX)
                // 2. If acquired, increment and return the fencing token
                var acquireScript = @"
                    if redis.call('SET', KEYS[1], ARGV[1], 'NX', 'EX', ARGV[2]) then
                        local token = redis.call('INCR', KEYS[2])
                        return token
                    else
                        return -1
                    end";

                var result = await db.ScriptEvaluateAsync(
                    acquireScript,
                    new RedisKey[] { lockKey, fencingKey },
                    new RedisValue[] { _instanceId, (int)_leadershipTtl.TotalSeconds });

                var fencingToken = (long)result;

                if (fencingToken > 0)
                {
                    var state = _leadershipStates.GetOrAdd(serviceName, _ => new LeadershipState());
                    state.IsLeader = true;
                    state.FencingToken = fencingToken;
                    state.LastRenewal = DateTime.UtcNow;

                    _logger.LogInformation(
                        "Leadership acquired for service {ServiceName} by instance {InstanceId} with fencing token {FencingToken}",
                        serviceName, _instanceId, fencingToken);

                    // Start automatic renewal
                    await StartLeadershipMaintenanceAsync(serviceName, cancellationToken);

                    return LeadershipAcquisitionResult.Success(fencingToken);
                }
                else
                {
                    var currentLeader = await db.StringGetAsync(lockKey);
                    _logger.LogDebug(
                        "Failed to acquire leadership for service {ServiceName}. Current leader: {CurrentLeader}",
                        serviceName, currentLeader);

                    return LeadershipAcquisitionResult.Failure();
                }
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogError(ex, "Redis connection error acquiring leadership for service {ServiceName}", serviceName);
                return LeadershipAcquisitionResult.Failure();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error acquiring leadership for service {ServiceName}", serviceName);
                return LeadershipAcquisitionResult.Failure();
            }
        }

        /// <inheritdoc/>
        public async Task ReleaseLeadershipAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            var lockKey = GetLockKey(serviceName);

            try
            {
                // Stop renewal first
                await StopLeadershipMaintenanceAsync(serviceName);

                var db = _redis.GetDatabase();

                // Only delete if we own the lock (atomic check and delete)
                var script = @"
                    if redis.call('GET', KEYS[1]) == ARGV[1] then
                        return redis.call('DEL', KEYS[1])
                    else
                        return 0
                    end";

                var result = await db.ScriptEvaluateAsync(
                    script,
                    new RedisKey[] { lockKey },
                    new RedisValue[] { _instanceId });

                if ((long)result == 1)
                {
                    if (_leadershipStates.TryRemove(serviceName, out _))
                    {
                        _logger.LogInformation(
                            "Leadership released for service {ServiceName} by instance {InstanceId}",
                            serviceName, _instanceId);
                    }
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
            finally
            {
                // Always clean up local state
                _leadershipStates.TryRemove(serviceName, out _);
            }
        }

        /// <inheritdoc/>
        public async Task<bool> IsLeaderAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            var lockKey = GetLockKey(serviceName);

            try
            {
                var db = _redis.GetDatabase();
                var currentLeader = await db.StringGetAsync(lockKey);
                var isLeader = currentLeader == _instanceId;

                // Update local state
                if (_leadershipStates.TryGetValue(serviceName, out var state))
                {
                    state.IsLeader = isLeader;
                }

                return isLeader;
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogError(ex, "Redis connection error checking leadership status for service {ServiceName}", serviceName);
                // On connection error, assume we lost leadership for safety
                if (_leadershipStates.TryGetValue(serviceName, out var state))
                {
                    state.IsLeader = false;
                }
                return false;
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

            try
            {
                var db = _redis.GetDatabase();
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
        public Task<long?> GetCurrentFencingTokenAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            if (_leadershipStates.TryGetValue(serviceName, out var state) && state.IsLeader)
            {
                return Task.FromResult<long?>(state.FencingToken);
            }

            return Task.FromResult<long?>(null);
        }

        /// <inheritdoc/>
        public async Task<bool> ValidateFencingTokenAsync(string serviceName, long fencingToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            // First check local state
            if (!_leadershipStates.TryGetValue(serviceName, out var state) || !state.IsLeader)
            {
                return false;
            }

            // Verify our fencing token matches
            if (state.FencingToken != fencingToken)
            {
                _logger.LogWarning(
                    "Fencing token mismatch for service {ServiceName}. Expected: {Expected}, Got: {Actual}",
                    serviceName, state.FencingToken, fencingToken);
                return false;
            }

            // Verify we still hold the lock in Redis
            var isStillLeader = await IsLeaderAsync(serviceName, cancellationToken);
            if (!isStillLeader)
            {
                _logger.LogWarning(
                    "Fencing token validation failed - no longer leader for service {ServiceName}",
                    serviceName);
                state.IsLeader = false;
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public Task StartLeadershipMaintenanceAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            // Stop any existing maintenance first
            StopMaintenanceInternal(serviceName);

            var maintenanceState = new MaintenanceState();

            // Link the provided cancellation token
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                maintenanceState.CancellationTokenSource.Token,
                cancellationToken);

            maintenanceState.MaintenanceTask = RunMaintenanceLoopAsync(serviceName, linkedCts.Token, linkedCts);

            _maintenanceStates[serviceName] = maintenanceState;

            _logger.LogDebug(
                "Started leadership maintenance for service {ServiceName} with renewal interval {RenewalInterval}",
                serviceName, _renewalInterval);

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task StopLeadershipMaintenanceAsync(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            if (_maintenanceStates.TryRemove(serviceName, out var state))
            {
                state.CancellationTokenSource.Cancel();

                // Wait for the maintenance task to complete
                if (state.MaintenanceTask != null)
                {
                    try
                    {
                        await state.MaintenanceTask.WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogWarning("Maintenance task for service {ServiceName} did not complete in time", serviceName);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected
                    }
                }

                state.Dispose();
                _logger.LogDebug("Stopped leadership maintenance for service {ServiceName}", serviceName);
            }
        }

        private void StopMaintenanceInternal(string serviceName)
        {
            if (_maintenanceStates.TryRemove(serviceName, out var state))
            {
                state.Dispose();
            }
        }

        private async Task RunMaintenanceLoopAsync(string serviceName, CancellationToken cancellationToken, CancellationTokenSource linkedCts)
        {
            try
            {
                using var timer = new PeriodicTimer(_renewalInterval);

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (!await timer.WaitForNextTickAsync(cancellationToken))
                        {
                            break;
                        }

                        await RenewLeadershipAsync(serviceName, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in maintenance loop for service {ServiceName}", serviceName);
            }
            finally
            {
                linkedCts.Dispose();
            }
        }

        private async Task RenewLeadershipAsync(string serviceName, CancellationToken cancellationToken)
        {
            var lockKey = GetLockKey(serviceName);

            try
            {
                var db = _redis.GetDatabase();

                // Atomic check and extend TTL
                var script = @"
                    if redis.call('GET', KEYS[1]) == ARGV[1] then
                        return redis.call('EXPIRE', KEYS[1], ARGV[2])
                    else
                        return 0
                    end";

                var result = await db.ScriptEvaluateAsync(
                    script,
                    new RedisKey[] { lockKey },
                    new RedisValue[] { _instanceId, (int)_leadershipTtl.TotalSeconds });

                if ((long)result == 1)
                {
                    if (_leadershipStates.TryGetValue(serviceName, out var state))
                    {
                        state.LastRenewal = DateTime.UtcNow;
                    }

                    _logger.LogTrace(
                        "Leadership renewed for service {ServiceName} by instance {InstanceId}",
                        serviceName, _instanceId);
                }
                else
                {
                    // Lost leadership
                    await HandleLeadershipLostAsync(serviceName);
                }
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogError(ex, "Redis connection error renewing leadership for service {ServiceName}", serviceName);
                // On connection error, we can't confirm we still hold the lock
                // Mark as not leader for safety
                await HandleLeadershipLostAsync(serviceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renewing leadership for service {ServiceName}", serviceName);

                // On error, check if we're still the leader
                try
                {
                    if (!await IsLeaderAsync(serviceName, cancellationToken))
                    {
                        await HandleLeadershipLostAsync(serviceName);
                    }
                }
                catch
                {
                    // If we can't check, assume we lost leadership
                    await HandleLeadershipLostAsync(serviceName);
                }
            }
        }

        private async Task HandleLeadershipLostAsync(string serviceName)
        {
            _logger.LogWarning("Lost leadership for service {ServiceName}. Stopping renewal.", serviceName);

            if (_leadershipStates.TryGetValue(serviceName, out var state))
            {
                state.IsLeader = false;
            }

            // Stop maintenance to prevent further renewal attempts
            StopMaintenanceInternal(serviceName);
        }

        private string GetLockKey(string serviceName)
        {
            return $"leader:{serviceName}";
        }

        private string GetFencingKey(string serviceName)
        {
            return $"leader:{serviceName}:fence";
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            await _disposeLock.WaitAsync();
            try
            {
                if (_disposed) return;
                _disposed = true;

                // Release all active leaderships
                var releaseTasks = new List<Task>();
                foreach (var serviceName in _leadershipStates.Keys.ToList())
                {
                    releaseTasks.Add(ReleaseLeadershipAsync(serviceName));
                }

                try
                {
                    await Task.WhenAll(releaseTasks).WaitAsync(TimeSpan.FromSeconds(10));
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("Timed out waiting for leadership releases during disposal");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error releasing leaderships during disposal");
                }

                // Dispose all maintenance states
                foreach (var state in _maintenanceStates.Values)
                {
                    state.Dispose();
                }

                _maintenanceStates.Clear();
                _leadershipStates.Clear();
            }
            finally
            {
                _disposeLock.Release();
                _disposeLock.Dispose();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            // Synchronously dispose - this is a fallback, prefer DisposeAsync
            _disposeLock.Wait();
            try
            {
                if (_disposed) return;
                _disposed = true;

                // Cancel all maintenance tasks
                foreach (var state in _maintenanceStates.Values)
                {
                    state.Dispose();
                }

                // Best-effort release of leaderships
                var db = _redis.GetDatabase();
                foreach (var serviceName in _leadershipStates.Keys)
                {
                    try
                    {
                        var lockKey = GetLockKey(serviceName);
                        var script = @"
                            if redis.call('GET', KEYS[1]) == ARGV[1] then
                                return redis.call('DEL', KEYS[1])
                            else
                                return 0
                            end";

                        db.ScriptEvaluate(script, new RedisKey[] { lockKey }, new RedisValue[] { _instanceId });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error releasing leadership for service {ServiceName} during disposal", serviceName);
                    }
                }

                _maintenanceStates.Clear();
                _leadershipStates.Clear();
            }
            finally
            {
                _disposeLock.Release();
                _disposeLock.Dispose();
            }
        }
    }
}
