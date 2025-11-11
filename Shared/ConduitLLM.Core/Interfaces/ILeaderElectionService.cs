using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for managing distributed leader election for background services
    /// </summary>
    public interface ILeaderElectionService
    {
        /// <summary>
        /// Attempts to acquire leadership for a specific service
        /// </summary>
        /// <param name="serviceName">Unique name of the service requiring leadership</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if leadership was acquired, false otherwise</returns>
        Task<bool> TryAcquireLeadershipAsync(string serviceName, CancellationToken cancellationToken = default);

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
        /// Starts automatic leadership renewal for a service
        /// </summary>
        /// <param name="serviceName">Unique name of the service</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task StartLeadershipMaintenanceAsync(string serviceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops automatic leadership renewal for a service
        /// </summary>
        /// <param name="serviceName">Unique name of the service</param>
        Task StopLeadershipMaintenanceAsync(string serviceName);
    }
}