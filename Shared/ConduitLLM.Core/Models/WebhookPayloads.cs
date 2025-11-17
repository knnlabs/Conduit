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
    
    /// <summary>
    /// Webhook payload for image generation completion, failure, or cancellation.
    /// </summary>
    public class ImageCompletionWebhookPayload : WebhookPayloadBase
    {
        /// <inheritdoc/>
        public override string WebhookType => "image_generation_completed";
        
        /// <summary>
        /// List of generated image URLs (for successful completion).
        /// </summary>
        [JsonPropertyName("image_urls")]
        public List<string>? ImageUrls { get; set; }
        
        /// <summary>
        /// Number of images successfully generated.
        /// </summary>
        [JsonPropertyName("images_generated")]
        public int ImagesGenerated { get; set; }
        
        /// <summary>
        /// Total images requested.
        /// </summary>
        [JsonPropertyName("images_requested")]
        public int ImagesRequested { get; set; }
        
        /// <summary>
        /// Time taken to generate images in seconds.
        /// </summary>
        [JsonPropertyName("generation_duration_seconds")]
        public double? GenerationDurationSeconds { get; set; }
        
        /// <summary>
        /// Model used for generation.
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; }
        
        /// <summary>
        /// Prompt used for generation.
        /// </summary>
        [JsonPropertyName("prompt")]
        public string? Prompt { get; set; }
        
        /// <summary>
        /// Image size (e.g., "1024x1024", "512x512").
        /// </summary>
        [JsonPropertyName("size")]
        public string? Size { get; set; }
        
        /// <summary>
        /// Response format ("url" or "b64_json").
        /// </summary>
        [JsonPropertyName("response_format")]
        public string? ResponseFormat { get; set; }
        
        /// <summary>
        /// Error message if the generation failed.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
    
    /// <summary>
    /// Webhook payload for image generation progress updates.
    /// </summary>
    public class ImageProgressWebhookPayload : WebhookPayloadBase
    {
        /// <inheritdoc/>
        public override string WebhookType => "image_generation_progress";
        
        /// <summary>
        /// Number of images completed.
        /// </summary>
        [JsonPropertyName("images_completed")]
        public int ImagesCompleted { get; set; }
        
        /// <summary>
        /// Total images being generated.
        /// </summary>
        [JsonPropertyName("total_images")]
        public int TotalImages { get; set; }
        
        /// <summary>
        /// Progress percentage (0-100).
        /// </summary>
        [JsonPropertyName("progress_percentage")]
        public int ProgressPercentage => TotalImages > 0 ? (ImagesCompleted * 100) / TotalImages : 0;
        
        /// <summary>
        /// Human-readable progress message.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}