using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Strongly typed metadata for async tasks.
    /// Contains all common metadata fields used across different task types.
    /// </summary>
    public class TaskMetadata
    {
        /// <summary>
        /// The ID of the virtual key that owns this task.
        /// Required for authorization checks.
        /// </summary>
        [JsonPropertyName("virtualKeyId")]
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// The model being used for this task (e.g., "dall-e-3", "gpt-4").
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        /// <summary>
        /// The prompt or input text for generation tasks.
        /// </summary>
        [JsonPropertyName("prompt")]
        public string? Prompt { get; set; }

        /// <summary>
        /// Correlation ID for tracking related operations.
        /// </summary>
        [JsonPropertyName("correlationId")]
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Serialized payload of the original request event.
        /// Used for event sourcing and replay.
        /// </summary>
        [JsonPropertyName("payload")]
        public string? Payload { get; set; }

        /// <summary>
        /// For video generation tasks: the video ID being generated.
        /// </summary>
        [JsonPropertyName("videoId")]
        public string? VideoId { get; set; }

        /// <summary>
        /// For webhook tasks: the URL to notify.
        /// </summary>
        [JsonPropertyName("webhookUrl")]
        public string? WebhookUrl { get; set; }

        /// <summary>
        /// For webhook tasks: custom headers to include.
        /// </summary>
        [JsonPropertyName("webhookHeaders")]
        public Dictionary<string, string>? WebhookHeaders { get; set; }

        /// <summary>
        /// Task priority for queue processing.
        /// </summary>
        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Additional custom properties for extensibility.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, object>? ExtensionData { get; set; }

        /// <summary>
        /// Creates a new TaskMetadata instance with the required virtual key ID.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key that owns this task.</param>
        public TaskMetadata(int virtualKeyId)
        {
            VirtualKeyId = virtualKeyId;
        }

        /// <summary>
        /// Parameterless constructor for deserialization.
        /// </summary>
        public TaskMetadata()
        {
        }
    }
}