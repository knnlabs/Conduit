using System;
using System.Threading.Tasks;

using ConduitLLM.WebUI.Models;
using ConduitLLM.WebUI.Services;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Interface for monitoring the health status of the Admin API
    /// </summary>
    public interface IAdminApiHealthService
    {
        /// <summary>
        /// Gets whether the Admin API is healthy
        /// </summary>
        bool IsHealthy { get; }

        /// <summary>
        /// Gets the last error message, if any
        /// </summary>
        string LastErrorMessage { get; }

        /// <summary>
        /// Gets the time when the health was last checked
        /// </summary>
        DateTime LastChecked { get; }

        /// <summary>
        /// Gets the last detailed health status
        /// </summary>
        DetailedHealthStatus? LastDetailedStatus { get; }

        /// <summary>
        /// Checks the health of the Admin API
        /// </summary>
        /// <param name="force">Whether to force a check regardless of the interval</param>
        /// <returns>True if the Admin API is healthy, false otherwise</returns>
        Task<bool> CheckHealthAsync(bool force = false);

        /// <summary>
        /// Gets detailed health status from the Admin API
        /// </summary>
        /// <param name="force">Whether to force a check regardless of the interval</param>
        /// <returns>Detailed health status</returns>
        Task<DetailedHealthStatus?> GetDetailedHealthAsync(bool force = false);

        /// <summary>
        /// Sets the interval between health checks
        /// </summary>
        /// <param name="interval">The interval</param>
        void SetCheckInterval(TimeSpan interval);

        /// <summary>
        /// Gets the connection details for the Admin API
        /// </summary>
        /// <returns>Connection details</returns>
        AdminApiConnectionDetails GetConnectionDetails();
    }
}
