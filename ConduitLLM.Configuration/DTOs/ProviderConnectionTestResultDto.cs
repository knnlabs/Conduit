using System;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Represents the result of a provider connection test.
    /// </summary>
    public class ProviderConnectionTestResultDto
    {
        /// <summary>
        /// Gets or sets a value indicating whether the connection test was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the status message from the connection test.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error details if the connection test failed.
        /// </summary>
        public string? ErrorDetails { get; set; }

        /// <summary>
        /// Gets or sets the provider type that was tested.
        /// </summary>
        public ProviderType ProviderType { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the test was performed.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Gets or sets the response time in milliseconds for the connection test.
        /// </summary>
        public double? ResponseTimeMs { get; set; }
        
    }
}
