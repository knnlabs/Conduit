using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Defines health check capabilities for providers.
    /// </summary>
    public interface IHealthCheckable
    {
        /// <summary>
        /// Performs a health check on the provider.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Health check result.</returns>
        Task<ProviderHealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of a provider health check operation.
    /// </summary>
    public class ProviderHealthCheckResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the service is healthy.
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets or sets the health status message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the response time in milliseconds.
        /// </summary>
        public double? ResponseTimeMs { get; set; }

        /// <summary>
        /// Gets or sets additional details about the health check.
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the health check.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets provider-specific health metrics.
        /// </summary>
        public Dictionary<string, object>? Metrics { get; set; }

        /// <summary>
        /// Creates a healthy result.
        /// </summary>
        public static ProviderHealthCheckResult Healthy(string message, double? responseTimeMs = null)
        {
            return new ProviderHealthCheckResult
            {
                IsHealthy = true,
                Message = message,
                ResponseTimeMs = responseTimeMs
            };
        }

        /// <summary>
        /// Creates an unhealthy result.
        /// </summary>
        public static ProviderHealthCheckResult Unhealthy(string message, string? details = null)
        {
            return new ProviderHealthCheckResult
            {
                IsHealthy = false,
                Message = message,
                Details = details
            };
        }
    }
}