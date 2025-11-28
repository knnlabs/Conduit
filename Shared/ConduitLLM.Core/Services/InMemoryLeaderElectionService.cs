using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// In-memory implementation of leader election service for development/single-instance scenarios.
    /// Implements fencing tokens for API compatibility with the Redis implementation.
    /// </summary>
    public class InMemoryLeaderElectionService : ILeaderElectionService
    {
        private readonly ILogger<InMemoryLeaderElectionService> _logger;
        private readonly ConcurrentDictionary<string, LeadershipInfo> _leaders;
        private readonly string _instanceId;
        private long _globalFencingCounter;

        /// <summary>
        /// Stores leadership information including fencing token
        /// </summary>
        private sealed class LeadershipInfo
        {
            public string InstanceId { get; init; } = string.Empty;
            public long FencingToken { get; init; }
        }

        public InMemoryLeaderElectionService(ILogger<InMemoryLeaderElectionService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _leaders = new ConcurrentDictionary<string, LeadershipInfo>();
            _instanceId = $"InMemory:{Guid.NewGuid():N}";
            _globalFencingCounter = 0;

            _logger.LogWarning(
                "Using in-memory leader election service. This should only be used in development or single-instance deployments.");
        }

        public Task<LeadershipAcquisitionResult> TryAcquireLeadershipAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            var fencingToken = Interlocked.Increment(ref _globalFencingCounter);
            var leaderInfo = new LeadershipInfo
            {
                InstanceId = _instanceId,
                FencingToken = fencingToken
            };

            var acquired = _leaders.TryAdd(serviceName, leaderInfo);

            if (acquired)
            {
                _logger.LogInformation(
                    "Leadership acquired for service {ServiceName} with fencing token {FencingToken}",
                    serviceName, fencingToken);
                return Task.FromResult(LeadershipAcquisitionResult.Success(fencingToken));
            }

            return Task.FromResult(LeadershipAcquisitionResult.Failure());
        }

        public Task ReleaseLeadershipAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            if (_leaders.TryRemove(serviceName, out var info) && info.InstanceId == _instanceId)
            {
                _logger.LogInformation("Leadership released for service {ServiceName}", serviceName);
            }

            return Task.CompletedTask;
        }

        public Task<bool> IsLeaderAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            var isLeader = _leaders.TryGetValue(serviceName, out var info) && info.InstanceId == _instanceId;
            return Task.FromResult(isLeader);
        }

        public Task<string?> GetCurrentLeaderAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            _leaders.TryGetValue(serviceName, out var info);
            return Task.FromResult(info?.InstanceId);
        }

        public Task<long?> GetCurrentFencingTokenAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            if (_leaders.TryGetValue(serviceName, out var info) && info.InstanceId == _instanceId)
            {
                return Task.FromResult<long?>(info.FencingToken);
            }

            return Task.FromResult<long?>(null);
        }

        public Task<bool> ValidateFencingTokenAsync(string serviceName, long fencingToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            if (_leaders.TryGetValue(serviceName, out var info))
            {
                return Task.FromResult(info.InstanceId == _instanceId && info.FencingToken == fencingToken);
            }

            return Task.FromResult(false);
        }

        public Task StartLeadershipMaintenanceAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            // No-op for in-memory implementation - leadership doesn't expire
            return Task.CompletedTask;
        }

        public Task StopLeadershipMaintenanceAsync(string serviceName)
        {
            // No-op for in-memory implementation
            return Task.CompletedTask;
        }
    }
}
