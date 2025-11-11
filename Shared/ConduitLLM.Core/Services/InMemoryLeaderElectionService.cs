using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// In-memory implementation of leader election service for development/single-instance scenarios
    /// </summary>
    public class InMemoryLeaderElectionService : ILeaderElectionService
    {
        private readonly ILogger<InMemoryLeaderElectionService> _logger;
        private readonly ConcurrentDictionary<string, string> _leaders;
        private readonly string _instanceId;

        public InMemoryLeaderElectionService(ILogger<InMemoryLeaderElectionService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _leaders = new ConcurrentDictionary<string, string>();
            _instanceId = $"InMemory:{Guid.NewGuid():N}";
            
            _logger.LogWarning(
                "Using in-memory leader election service. This should only be used in development or single-instance deployments.");
        }

        public Task<bool> TryAcquireLeadershipAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            var acquired = _leaders.TryAdd(serviceName, _instanceId);
            
            if (acquired)
            {
                _logger.LogInformation("Leadership acquired for service {ServiceName}", serviceName);
            }
            
            return Task.FromResult(acquired);
        }

        public Task ReleaseLeadershipAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            if (_leaders.TryRemove(serviceName, out _))
            {
                _logger.LogInformation("Leadership released for service {ServiceName}", serviceName);
            }
            
            return Task.CompletedTask;
        }

        public Task<bool> IsLeaderAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            var isLeader = _leaders.TryGetValue(serviceName, out var leader) && leader == _instanceId;
            return Task.FromResult(isLeader);
        }

        public Task<string?> GetCurrentLeaderAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            _leaders.TryGetValue(serviceName, out var leader);
            return Task.FromResult(leader);
        }

        public Task StartLeadershipMaintenanceAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            // No-op for in-memory implementation
            return Task.CompletedTask;
        }

        public Task StopLeadershipMaintenanceAsync(string serviceName)
        {
            // No-op for in-memory implementation
            return Task.CompletedTask;
        }
    }
}