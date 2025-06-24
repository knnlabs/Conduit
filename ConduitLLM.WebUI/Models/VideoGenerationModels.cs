using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.WebUI.Models
{
    /// <summary>
    /// Response for async video generation task creation.
    /// </summary>
    public class VideoGenerationTaskResponse
    {
        /// <summary>
        /// Unique identifier for the video generation task.
        /// </summary>
        [JsonPropertyName("taskId")]
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Current status of the task (pending, processing, completed, failed).
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// When the task was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Estimated time when the video will be ready.
        /// </summary>
        [JsonPropertyName("estimatedCompletionTime")]
        public DateTimeOffset? EstimatedCompletionTime { get; set; }

        /// <summary>
        /// URL to check the status of this task.
        /// </summary>
        [JsonPropertyName("checkStatusUrl")]
        public string CheckStatusUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Status information for a video generation task.
    /// </summary>
    public class VideoGenerationTaskStatus
    {
        /// <summary>
        /// Unique identifier for the task.
        /// </summary>
        [JsonPropertyName("taskId")]
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Current status (pending, running, completed, failed, cancelled).
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Progress percentage (0-100).
        /// </summary>
        [JsonPropertyName("progress")]
        public int? Progress { get; set; }

        /// <summary>
        /// When the task was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// When the task was last updated.
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// When the task completed (if applicable).
        /// </summary>
        [JsonPropertyName("completedAt")]
        public DateTimeOffset? CompletedAt { get; set; }

        /// <summary>
        /// Error message if the task failed.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        /// <summary>
        /// Result data (internal use).
        /// </summary>
        [JsonPropertyName("result")]
        public string? Result { get; set; }

        /// <summary>
        /// The video generation response if completed.
        /// </summary>
        [JsonPropertyName("videoResponse")]
        public ConduitLLM.Core.Models.VideoGenerationResponse? VideoResponse { get; set; }
    }
}