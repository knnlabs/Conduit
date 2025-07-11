using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ConduitLLM.CoreClient.Models
{
    /// <summary>
    /// Represents a request to generate a video based on a text prompt.
    /// </summary>
    public class VideoGenerationRequest
    {
        /// <summary>
        /// Gets or sets the text prompt that describes what video to generate.
        /// </summary>
        [Required]
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the model to use for video generation (e.g., "minimax-video-01").
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        /// <summary>
        /// Gets or sets the duration of the video in seconds. Defaults to 5 seconds.
        /// </summary>
        [Range(1, 60)]
        [JsonPropertyName("duration")]
        public int? Duration { get; set; }

        /// <summary>
        /// Gets or sets the size/resolution of the video (e.g., "1920x1080", "1280x720").
        /// </summary>
        [JsonPropertyName("size")]
        public string? Size { get; set; }

        /// <summary>
        /// Gets or sets frames per second for the video. Common values: 24, 30, 60.
        /// </summary>
        [Range(1, 120)]
        [JsonPropertyName("fps")]
        public int? Fps { get; set; }

        /// <summary>
        /// Gets or sets the style or aesthetic of the video generation.
        /// </summary>
        [JsonPropertyName("style")]
        public string? Style { get; set; }

        /// <summary>
        /// Gets or sets the format in which the generated video is returned.
        /// Options: "url" (default) or "b64_json" (base64 encoded).
        /// </summary>
        [JsonPropertyName("response_format")]
        public string? ResponseFormat { get; set; }

        /// <summary>
        /// Gets or sets a unique identifier representing your end-user.
        /// </summary>
        [JsonPropertyName("user")]
        public string? User { get; set; }

        /// <summary>
        /// Gets or sets optional seed for deterministic generation.
        /// </summary>
        [JsonPropertyName("seed")]
        public int? Seed { get; set; }

        /// <summary>
        /// Gets or sets the number of videos to generate. Defaults to 1.
        /// </summary>
        [Range(1, 10)]
        [JsonPropertyName("n")]
        public int N { get; set; } = 1;
    }

    /// <summary>
    /// Represents the response from a video generation request.
    /// </summary>
    public class VideoGenerationResponse
    {
        /// <summary>
        /// Gets or sets the Unix timestamp of when the response was created.
        /// </summary>
        [JsonPropertyName("created")]
        public long Created { get; set; }

        /// <summary>
        /// Gets or sets the list of generated video data.
        /// </summary>
        [JsonPropertyName("data")]
        public List<VideoData> Data { get; set; } = new();

        /// <summary>
        /// Gets or sets the model used for generation.
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        /// <summary>
        /// Gets or sets usage information if available.
        /// </summary>
        [JsonPropertyName("usage")]
        public VideoUsage? Usage { get; set; }
    }

    /// <summary>
    /// Represents a single generated video.
    /// </summary>
    public class VideoData
    {
        /// <summary>
        /// Gets or sets the URL of the generated video, if response_format is "url".
        /// </summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets the base64-encoded video data, if response_format is "b64_json".
        /// </summary>
        [JsonPropertyName("b64_json")]
        public string? B64Json { get; set; }

        /// <summary>
        /// Gets or sets the revised prompt that was used for generation.
        /// </summary>
        [JsonPropertyName("revised_prompt")]
        public string? RevisedPrompt { get; set; }

        /// <summary>
        /// Gets or sets additional metadata about the generated video.
        /// </summary>
        [JsonPropertyName("metadata")]
        public VideoMetadata? Metadata { get; set; }
    }

    /// <summary>
    /// Represents usage statistics for video generation.
    /// </summary>
    public class VideoUsage
    {
        /// <summary>
        /// Gets or sets the number of prompt tokens used.
        /// </summary>
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        /// <summary>
        /// Gets or sets the total number of tokens used.
        /// </summary>
        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }

        /// <summary>
        /// Gets or sets the duration processed in seconds.
        /// </summary>
        [JsonPropertyName("duration_seconds")]
        public double? DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the total processing time in seconds.
        /// </summary>
        [JsonPropertyName("processing_time_seconds")]
        public double? ProcessingTimeSeconds { get; set; }
    }

    /// <summary>
    /// Represents metadata about a generated video.
    /// </summary>
    public class VideoMetadata
    {
        /// <summary>
        /// Gets or sets the actual duration of the generated video in seconds.
        /// </summary>
        [JsonPropertyName("duration")]
        public double? Duration { get; set; }

        /// <summary>
        /// Gets or sets the resolution of the generated video.
        /// </summary>
        [JsonPropertyName("resolution")]
        public string? Resolution { get; set; }

        /// <summary>
        /// Gets or sets the frames per second of the generated video.
        /// </summary>
        [JsonPropertyName("fps")]
        public int? Fps { get; set; }

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        [JsonPropertyName("file_size_bytes")]
        public long? FileSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the video format/codec.
        /// </summary>
        [JsonPropertyName("format")]
        public string? Format { get; set; }

        /// <summary>
        /// Gets or sets the video codec used for encoding.
        /// </summary>
        [JsonPropertyName("codec")]
        public string? Codec { get; set; }

        /// <summary>
        /// Gets or sets the audio codec used for encoding.
        /// </summary>
        [JsonPropertyName("audio_codec")]
        public string? AudioCodec { get; set; }

        /// <summary>
        /// Gets or sets the bitrate of the video.
        /// </summary>
        [JsonPropertyName("bitrate")]
        public int? Bitrate { get; set; }

        /// <summary>
        /// Gets or sets the MIME type of the video file.
        /// </summary>
        [JsonPropertyName("mime_type")]
        public string? MimeType { get; set; }

        /// <summary>
        /// Gets or sets the seed used for generation, if any.
        /// </summary>
        [JsonPropertyName("seed")]
        public int? Seed { get; set; }
    }

    /// <summary>
    /// Represents an async video generation request.
    /// </summary>
    public class AsyncVideoGenerationRequest : VideoGenerationRequest
    {
        /// <summary>
        /// Gets or sets the webhook URL to receive the result when generation is complete.
        /// </summary>
        [JsonPropertyName("webhook_url")]
        public string? WebhookUrl { get; set; }

        /// <summary>
        /// Gets or sets additional metadata to include with the webhook callback.
        /// </summary>
        [JsonPropertyName("webhook_metadata")]
        public Dictionary<string, object>? WebhookMetadata { get; set; }

        /// <summary>
        /// Gets or sets additional headers to include with the webhook callback.
        /// </summary>
        [JsonPropertyName("webhook_headers")]
        public Dictionary<string, string>? WebhookHeaders { get; set; }

        /// <summary>
        /// Gets or sets the timeout for the generation task in seconds.
        /// </summary>
        [Range(1, 3600)]
        [JsonPropertyName("timeout_seconds")]
        public int? TimeoutSeconds { get; set; }
    }

    /// <summary>
    /// Represents the response from an async video generation request.
    /// </summary>
    public class AsyncVideoGenerationResponse
    {
        /// <summary>
        /// Gets or sets the unique task identifier.
        /// </summary>
        [JsonPropertyName("task_id")]
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current status of the task.
        /// </summary>
        [JsonPropertyName("status")]
        public VideoTaskStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the progress percentage (0-100).
        /// </summary>
        [JsonPropertyName("progress")]
        public int Progress { get; set; }

        /// <summary>
        /// Gets or sets an optional progress message.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets the estimated time to completion in seconds.
        /// </summary>
        [JsonPropertyName("estimated_time_to_completion")]
        public int? EstimatedTimeToCompletion { get; set; }

        /// <summary>
        /// Gets or sets when the task was created.
        /// </summary>
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the task was last updated.
        /// </summary>
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the generation result, available when status is Completed.
        /// </summary>
        [JsonPropertyName("result")]
        public VideoGenerationResponse? Result { get; set; }

        /// <summary>
        /// Gets or sets error information if the task failed.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    /// <summary>
    /// Represents the status of an async video generation task.
    /// </summary>
    public enum VideoTaskStatus
    {
        /// <summary>
        /// Task is waiting to be processed.
        /// </summary>
        Pending,

        /// <summary>
        /// Task is currently being processed.
        /// </summary>
        Running,

        /// <summary>
        /// Task completed successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// Task failed with an error.
        /// </summary>
        Failed,

        /// <summary>
        /// Task was cancelled.
        /// </summary>
        Cancelled,

        /// <summary>
        /// Task timed out.
        /// </summary>
        TimedOut
    }

    /// <summary>
    /// Represents options for polling video task status.
    /// </summary>
    public class VideoTaskPollingOptions
    {
        /// <summary>
        /// Gets or sets the polling interval in milliseconds.
        /// </summary>
        public int IntervalMs { get; set; } = 2000;

        /// <summary>
        /// Gets or sets the maximum polling timeout in milliseconds.
        /// </summary>
        public int TimeoutMs { get; set; } = 600000; // 10 minutes

        /// <summary>
        /// Gets or sets whether to use exponential backoff for polling intervals.
        /// </summary>
        public bool UseExponentialBackoff { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum interval between polls in milliseconds when using exponential backoff.
        /// </summary>
        public int MaxIntervalMs { get; set; } = 30000; // 30 seconds
    }

    /// <summary>
    /// Common video models supported by Conduit.
    /// </summary>
    public static class VideoModels
    {
        /// <summary>
        /// MiniMax video generation model.
        /// </summary>
        public const string MiniMaxVideo = "minimax-video";

        /// <summary>
        /// Default video model.
        /// </summary>
        public const string Default = MiniMaxVideo;
    }

    /// <summary>
    /// Common video resolutions.
    /// </summary>
    public static class VideoResolutions
    {
        /// <summary>
        /// 720p resolution (1280x720).
        /// </summary>
        public const string HD = "1280x720";

        /// <summary>
        /// 1080p resolution (1920x1080).
        /// </summary>
        public const string FullHD = "1920x1080";

        /// <summary>
        /// Vertical 720p (720x1280).
        /// </summary>
        public const string VerticalHD = "720x1280";

        /// <summary>
        /// Vertical 1080p (1080x1920).
        /// </summary>
        public const string VerticalFullHD = "1080x1920";

        /// <summary>
        /// Square format (720x720).
        /// </summary>
        public const string Square = "720x720";
    }

    /// <summary>
    /// Video response formats.
    /// </summary>
    public static class VideoResponseFormats
    {
        /// <summary>
        /// Return video as URL (default).
        /// </summary>
        public const string Url = "url";

        /// <summary>
        /// Return video as base64-encoded JSON.
        /// </summary>
        public const string Base64Json = "b64_json";
    }

    /// <summary>
    /// Default values for video generation.
    /// </summary>
    public static class VideoDefaults
    {
        /// <summary>
        /// Default duration in seconds.
        /// </summary>
        public const int Duration = 5;

        /// <summary>
        /// Default resolution.
        /// </summary>
        public const string Resolution = VideoResolutions.HD;

        /// <summary>
        /// Default frames per second.
        /// </summary>
        public const int Fps = 30;

        /// <summary>
        /// Default response format.
        /// </summary>
        public const string ResponseFormat = VideoResponseFormats.Url;
    }

    /// <summary>
    /// Base class for webhook payloads sent by Conduit.
    /// </summary>
    public abstract class WebhookPayloadBase
    {
        /// <summary>
        /// Gets or sets the unique identifier for this webhook event.
        /// </summary>
        [JsonPropertyName("event_id")]
        public string EventId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of webhook event.
        /// </summary>
        [JsonPropertyName("event_type")]
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp when the event occurred.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the task ID associated with this event.
        /// </summary>
        [JsonPropertyName("task_id")]
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets optional metadata provided in the original request.
        /// </summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Webhook payload sent when video generation is completed.
    /// </summary>
    public class VideoCompletionWebhookPayload : WebhookPayloadBase
    {
        /// <summary>
        /// Gets or sets the final status of the video generation task.
        /// </summary>
        [JsonPropertyName("status")]
        public VideoTaskStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the generated video result, if successful.
        /// </summary>
        [JsonPropertyName("result")]
        public VideoGenerationResponse? Result { get; set; }

        /// <summary>
        /// Gets or sets error information if the task failed.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets the total processing time in seconds.
        /// </summary>
        [JsonPropertyName("processing_time_seconds")]
        public double? ProcessingTimeSeconds { get; set; }
    }

    /// <summary>
    /// Webhook payload sent to provide progress updates during video generation.
    /// </summary>
    public class VideoProgressWebhookPayload : WebhookPayloadBase
    {
        /// <summary>
        /// Gets or sets the current status of the video generation task.
        /// </summary>
        [JsonPropertyName("status")]
        public VideoTaskStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the progress percentage (0-100).
        /// </summary>
        [JsonPropertyName("progress")]
        public int Progress { get; set; }

        /// <summary>
        /// Gets or sets an optional progress message.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets the estimated time to completion in seconds.
        /// </summary>
        [JsonPropertyName("estimated_time_to_completion")]
        public int? EstimatedTimeToCompletion { get; set; }
    }
}