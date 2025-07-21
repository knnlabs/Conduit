using System;

namespace ConduitLLM.Core.Configuration
{
    /// <summary>
    /// Provides operation-specific timeout configurations to prevent conflicting timeout policies.
    /// </summary>
    public interface IOperationTimeoutProvider
    {
        /// <summary>
        /// Gets the timeout duration for a specific operation type.
        /// </summary>
        /// <param name="operationType">The type of operation (e.g., "chat", "completion", "image-generation", "video-generation", "polling").</param>
        /// <returns>The timeout duration for the operation.</returns>
        TimeSpan GetTimeout(string operationType);

        /// <summary>
        /// Determines whether a timeout should be applied for the specified operation type.
        /// </summary>
        /// <param name="operationType">The type of operation.</param>
        /// <returns>True if a timeout should be applied; otherwise, false.</returns>
        bool ShouldApplyTimeout(string operationType);

        /// <summary>
        /// Gets the timeout duration for a specific operation type with a fallback default.
        /// </summary>
        /// <param name="operationType">The type of operation.</param>
        /// <param name="defaultTimeout">The default timeout to use if no specific configuration exists.</param>
        /// <returns>The configured timeout or the default timeout.</returns>
        TimeSpan GetTimeoutOrDefault(string operationType, TimeSpan defaultTimeout);
    }
}