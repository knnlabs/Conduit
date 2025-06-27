using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConduitLLM.WebUI.Models
{
    /// <summary>
    /// Response for async image generation task creation.
    /// </summary>
    public class ImageGenerationTaskResponse
    {
        /// <summary>
        /// Unique identifier for the image generation task.
        /// </summary>
        [JsonPropertyName("task_id")]
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Current status of the task (pending, processing, completed, failed).
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// When the task was created.
        /// </summary>
        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Estimated time when the images will be ready.
        /// </summary>
        [JsonPropertyName("estimated_completion_time")]
        public DateTimeOffset? EstimatedCompletionTime { get; set; }

        /// <summary>
        /// URL to check the status of this task.
        /// </summary>
        [JsonPropertyName("check_status_url")]
        public string CheckStatusUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Status information for an image generation task.
    /// </summary>
    public class ImageGenerationTaskStatus
    {
        /// <summary>
        /// Unique identifier for the task.
        /// </summary>
        [JsonPropertyName("task_id")]
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
        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// When the task was last updated.
        /// </summary>
        [JsonPropertyName("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// When the task completed (if applicable).
        /// </summary>
        [JsonPropertyName("completed_at")]
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
        /// The image generation response if completed.
        /// </summary>
        [JsonPropertyName("image_response")]
        public ConduitLLM.Core.Models.ImageGenerationResponse? ImageResponse { get; set; }
    }
}