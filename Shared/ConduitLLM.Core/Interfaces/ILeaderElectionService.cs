using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Represents the result of a leadership acquisition attempt, including a fencing token
    /// </summary>
    public readonly struct LeadershipAcquisitionResult
    {
        /// <summary>
        /// Whether leadership was successfully acquired
        /// </summary>
        public bool Acquired { get; }

        /// <summary>
        /// Fencing token that must be validated before performing leader work.
        /// This token changes each time leadership is acquired and helps prevent
        /// split-brain scenarios where a stale leader continues processing.
        /// </summary>
        public long FencingToken { get; }

        /// <summary>
        /// Creates a successful acquisition result
        /// </summary>
        public static LeadershipAcquisitionResult Success(long fencingToken) => new(true, fencingToken);

        /// <summary>
        /// Creates a failed acquisition result
        /// </summary>
        public static LeadershipAcquisitionResult Failure() => new(false, 0);

        private LeadershipAcquisitionResult(bool acquired, long fencingToken)
        {
            Acquired = acquired;
            FencingToken = fencingToken;
        }
    }

    /// <summary>
    /// Service for managing distributed leader election for background services.
    /// Implements distributed locking with fencing tokens to prevent split-brain scenarios.
    /// </summary>
    public interface ILeaderElectionService
    {
        /// <summary>
        /// Attempts to acquire leadership for a specific service
        /// </summary>
        /// <param name="serviceName">Unique name of the service requiring leadership</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result containing acquisition status and fencing token</returns>
        Task<LeadershipAcquisitionResult> TryAcquireLeadershipAsync(string serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases leadership for a specific service
        /// </summary>
        /// <param name="serviceName">Unique name of the service</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task ReleaseLeadershipAsync(string serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the current instance is the leader for a specific service
        /// </summary>
        /// <param name="serviceName">Unique name of the service</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if this instance is the leader, false otherwise</returns>
        Task<bool> IsLeaderAsync(string serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current leader instance ID for a specific service
        /// </summary>
        /// <param name="serviceName">Unique name of the service</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Instance ID of the current leader, or null if no leader</returns>
        Task<string?> GetCurrentLeaderAsync(string serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current fencing token for a service.
        /// Used to validate that this instance still holds valid leadership before performing work.
        /// </summary>
        /// <param name="serviceName">Unique name of the service</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Current fencing token, or null if not the leader</returns>
        Task<long?> GetCurrentFencingTokenAsync(string serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates that the provided fencing token is still valid for the service.
        /// This should be called before performing any leader-only work to prevent stale leaders.
        /// </summary>
        /// <param name="serviceName">Unique name of the service</param>
        /// <param name="fencingToken">The fencing token to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the fencing token is valid, false if leadership was lost</returns>
        Task<bool> ValidateFencingTokenAsync(string serviceName, long fencingToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts automatic leadership renewal for a service
        /// </summary>
        /// <param name="serviceName">Unique name of the service</param>
        /// <param name="cancellationToken">Cancellation token that will stop renewal when triggered</param>
        Task StartLeadershipMaintenanceAsync(string serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops automatic leadership renewal for a service
        /// </summary>
        /// <param name="serviceName">Unique name of the service</param>
        /// <returns>Task that completes when maintenance is fully stopped</returns>
        Task StopLeadershipMaintenanceAsync(string serviceName);
    }
}