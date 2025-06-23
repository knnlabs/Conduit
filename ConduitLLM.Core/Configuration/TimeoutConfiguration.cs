using System;
using System.Collections.Generic;

namespace ConduitLLM.Core.Configuration
{
    /// <summary>
    /// Configuration class for timeout settings across different operation types.
    /// </summary>
    public class TimeoutConfiguration
    {
        /// <summary>
        /// Gets or sets the timeout configurations for different operation types.
        /// </summary>
        public Dictionary<string, OperationTimeout> Timeouts { get; set; } = new();

        /// <summary>
        /// Gets or sets the default timeout for operations not explicitly configured.
        /// </summary>
        public int DefaultTimeoutSeconds { get; set; } = 100;

        /// <summary>
        /// Gets or sets whether timeout diagnostics should be logged.
        /// </summary>
        public bool EnableDiagnostics { get; set; } = true;
    }

    /// <summary>
    /// Represents timeout configuration for a specific operation type.
    /// </summary>
    public class OperationTimeout
    {
        /// <summary>
        /// Gets or sets the timeout in seconds for this operation.
        /// </summary>
        public int Seconds { get; set; }

        /// <summary>
        /// Gets or sets whether timeout should be applied for this operation.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a description of this timeout configuration.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets whether this is a hard timeout (no retries) or soft timeout (with retries).
        /// </summary>
        public bool IsHardTimeout { get; set; } = false;
    }

    /// <summary>
    /// Constants for common operation types.
    /// </summary>
    public static class OperationTypes
    {
        public const string Chat = "chat";
        public const string Completion = "completion";
        public const string ImageGeneration = "image-generation";
        public const string VideoGeneration = "video-generation";
        public const string VideoPolling = "video-polling";
        public const string Polling = "polling";
        public const string HealthCheck = "health-check";
        public const string ModelDiscovery = "model-discovery";
        public const string Streaming = "streaming";
        public const string WebSocket = "websocket";
    }
}