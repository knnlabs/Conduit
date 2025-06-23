using System;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Base webhook payload containing common fields.
    /// </summary>
    public abstract class WebhookPayloadBase
    {
        /// <summary>
        /// The task ID this webhook is for.
        /// </summary>
        [JsonPropertyName("task_id")]
        public required string TaskId { get; set; }

        /// <summary>
        /// The current status of the task.
        /// </summary>
        [JsonPropertyName("status")]
        public required string Status { get; set; }

        /// <summary>
        /// Timestamp when this webhook was sent.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Type of webhook notification.
        /// </summary>
        [JsonPropertyName("webhook_type")]
        public abstract string WebhookType { get; }
    }

    /// <summary>
    /// Webhook payload for video generation completion.
    /// </summary>
    public class VideoCompletionWebhookPayload : WebhookPayloadBase
    {
        /// <inheritdoc/>
        public override string WebhookType => "video_generation_completed";

        /// <summary>
        /// URL of the generated video.
        /// </summary>
        [JsonPropertyName("video_url")]
        public string? VideoUrl { get; set; }

        /// <summary>
        /// Error message if the generation failed.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        /// <summary>
        /// Duration of the video generation process in seconds.
        /// </summary>
        [JsonPropertyName("generation_duration_seconds")]
        public double? GenerationDurationSeconds { get; set; }

        /// <summary>
        /// Model used for generation.
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        /// <summary>
        /// Original prompt used for generation.
        /// </summary>
        [JsonPropertyName("prompt")]
        public string? Prompt { get; set; }
    }

    /// <summary>
    /// Webhook payload for video generation progress updates.
    /// </summary>
    public class VideoProgressWebhookPayload : WebhookPayloadBase
    {
        /// <inheritdoc/>
        public override string WebhookType => "video_generation_progress";

        /// <summary>
        /// Progress percentage (0-100).
        /// </summary>
        [JsonPropertyName("progress_percentage")]
        public int ProgressPercentage { get; set; }

        /// <summary>
        /// Human-readable progress message.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>
        /// Estimated time remaining in seconds.
        /// </summary>
        [JsonPropertyName("estimated_seconds_remaining")]
        public int? EstimatedSecondsRemaining { get; set; }
    }
}