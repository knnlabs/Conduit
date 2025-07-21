using System;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Response for async task creation endpoints.
    /// </summary>
    public class AsyncTaskResponse
    {
        /// <summary>
        /// Unique identifier for the async task.
        /// </summary>
        [JsonPropertyName("task_id")]
        public required string TaskId { get; set; }

        /// <summary>
        /// Current status of the task (e.g., "queued", "processing", "completed", "failed").
        /// </summary>
        [JsonPropertyName("status")]
        public required string Status { get; set; }

        /// <summary>
        /// URL to check the status of this task.
        /// </summary>
        [JsonPropertyName("check_status_url")]
        public string? CheckStatusUrl { get; set; }

        /// <summary>
        /// When the task was created.
        /// </summary>
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Estimated time when the task will complete.
        /// </summary>
        [JsonPropertyName("estimated_completion_time")]
        public DateTime? EstimatedCompletionTime { get; set; }
    }

    /// <summary>
    /// Response for async task status endpoints.
    /// </summary>
    public class AsyncTaskStatusResponse
    {
        /// <summary>
        /// Unique identifier for the task.
        /// </summary>
        [JsonPropertyName("task_id")]
        public required string TaskId { get; set; }

        /// <summary>
        /// Current status (e.g., "queued", "running", "completed", "failed", "cancelled").
        /// </summary>
        [JsonPropertyName("status")]
        public required string Status { get; set; }

        /// <summary>
        /// Progress percentage (0-100).
        /// </summary>
        [JsonPropertyName("progress")]
        public int? Progress { get; set; }

        /// <summary>
        /// When the task was created.
        /// </summary>
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the task was last updated.
        /// </summary>
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Result data if the task is completed.
        /// </summary>
        [JsonPropertyName("result")]
        public object? Result { get; set; }

        /// <summary>
        /// Error message if the task failed.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}