using System;
using System.Collections.Generic;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Events
{
    // ===============================
    // Video Generation Domain Events
    // ===============================

    /// <summary>
    /// Raised when a video generation request is submitted.
    /// Enables async processing across multiple service instances.
    /// </summary>
    public record VideoGenerationRequested : DomainEvent
    {
        /// <summary>
        /// Unique request identifier for tracking
        /// </summary>
        public string RequestId { get; init; } = string.Empty;

        /// <summary>
        /// The model to use for video generation
        /// </summary>
        public string Model { get; init; } = string.Empty;

        /// <summary>
        /// The prompt describing what video to generate
        /// </summary>
        public string Prompt { get; init; } = string.Empty;

        /// <summary>
        /// Virtual Key ID or hash for authorization and spend tracking
        /// </summary>
        public string VirtualKeyId { get; init; } = string.Empty;

        /// <summary>
        /// Whether this is an async generation request
        /// </summary>
        public bool IsAsync { get; init; } = false;

        /// <summary>
        /// When the request was submitted
        /// </summary>
        public DateTime RequestedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Optional video generation parameters
        /// </summary>
        public VideoGenerationParameters? Parameters { get; init; }

        /// <summary>
        /// Optional webhook URL to receive notifications when video generation completes
        /// </summary>
        public string? WebhookUrl { get; init; }

        /// <summary>
        /// Optional headers to include in the webhook request
        /// </summary>
        public Dictionary<string, string>? WebhookHeaders { get; init; }

        /// <summary>
        /// Partition key for ordered processing per virtual key
        /// </summary>
        public string PartitionKey => VirtualKeyId;
    }

    /// <summary>
    /// Raised when video generation starts processing
    /// </summary>
    public record VideoGenerationStarted : DomainEvent
    {
        /// <summary>
        /// Request identifier
        /// </summary>
        public string RequestId { get; init; } = string.Empty;

        /// <summary>
        /// Provider handling the generation
        /// </summary>
        public string Provider { get; init; } = string.Empty;

        /// <summary>
        /// When processing started
        /// </summary>
        public DateTime StartedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Estimated completion time in seconds
        /// </summary>
        public int? EstimatedSeconds { get; init; }
    }

    /// <summary>
    /// Raised when video generation progress updates occur
    /// </summary>
    public record VideoGenerationProgress : DomainEvent
    {
        /// <summary>
        /// Request identifier
        /// </summary>
        public string RequestId { get; init; } = string.Empty;

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public int ProgressPercentage { get; init; }

        /// <summary>
        /// Current status message
        /// </summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>
        /// Optional detailed message
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Frames rendered (if applicable)
        /// </summary>
        public int? FramesCompleted { get; init; }

        /// <summary>
        /// Total frames to render (if applicable)
        /// </summary>
        public int? TotalFrames { get; init; }
    }

    /// <summary>
    /// Raised when video generation completes successfully
    /// </summary>
    public record VideoGenerationCompleted : DomainEvent
    {
        /// <summary>
        /// Request identifier
        /// </summary>
        public string RequestId { get; init; } = string.Empty;

        /// <summary>
        /// URL where the video can be accessed
        /// </summary>
        public string VideoUrl { get; init; } = string.Empty;

        /// <summary>
        /// Optional preview/thumbnail URL
        /// </summary>
        public string? PreviewUrl { get; init; }

        /// <summary>
        /// Video duration in seconds
        /// </summary>
        public double Duration { get; init; }

        /// <summary>
        /// Video resolution (e.g., "1280x720")
        /// </summary>
        public string Resolution { get; init; } = string.Empty;

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long FileSize { get; init; }

        /// <summary>
        /// Total generation duration
        /// </summary>
        public TimeSpan GenerationDuration { get; init; }

        /// <summary>
        /// Total cost incurred
        /// </summary>
        public decimal Cost { get; init; }

        /// <summary>
        /// Provider used for generation
        /// </summary>
        public string Provider { get; init; } = string.Empty;

        /// <summary>
        /// Model used for generation
        /// </summary>
        public string Model { get; init; } = string.Empty;

        /// <summary>
        /// When generation completed
        /// </summary>
        public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Raised when video generation fails
    /// </summary>
    public record VideoGenerationFailed : DomainEvent
    {
        /// <summary>
        /// Request identifier
        /// </summary>
        public string RequestId { get; init; } = string.Empty;

        /// <summary>
        /// Error message
        /// </summary>
        public string Error { get; init; } = string.Empty;

        /// <summary>
        /// Error code (if available)
        /// </summary>
        public string? ErrorCode { get; init; }

        /// <summary>
        /// Provider that failed
        /// </summary>
        public string? Provider { get; init; }

        /// <summary>
        /// Whether the request can be retried
        /// </summary>
        public bool IsRetryable { get; init; } = true;

        /// <summary>
        /// Number of retry attempts made
        /// </summary>
        public int RetryCount { get; init; } = 0;

        /// <summary>
        /// Maximum number of retries allowed
        /// </summary>
        public int MaxRetries { get; init; } = 3;

        /// <summary>
        /// When the failure occurred
        /// </summary>
        public DateTime FailedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// When the task should be retried (if applicable)
        /// </summary>
        public DateTime? NextRetryAt { get; init; }
    }

    /// <summary>
    /// Raised when a video generation is cancelled
    /// </summary>
    public record VideoGenerationCancelled : DomainEvent
    {
        /// <summary>
        /// Request identifier
        /// </summary>
        public string RequestId { get; init; } = string.Empty;

        /// <summary>
        /// Reason for cancellation
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// When the cancellation occurred
        /// </summary>
        public DateTime CancelledAt { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Video generation parameters
    /// </summary>
    public record VideoGenerationParameters
    {
        /// <summary>
        /// Video resolution/size
        /// </summary>
        public string? Size { get; init; }

        /// <summary>
        /// Duration in seconds
        /// </summary>
        public int? Duration { get; init; }

        /// <summary>
        /// Frames per second
        /// </summary>
        public int? Fps { get; init; }

        /// <summary>
        /// Style or aesthetic
        /// </summary>
        public string? Style { get; init; }

        /// <summary>
        /// Response format (url, b64_json)
        /// </summary>
        public string? ResponseFormat { get; init; }

        /// <summary>
        /// Base64-encoded starting image
        /// </summary>
        public string? StartImage { get; init; }

        /// <summary>
        /// Base64-encoded ending image (for interpolation)
        /// </summary>
        public string? EndImage { get; init; }

        /// <summary>
        /// Additional provider-specific options
        /// </summary>
        public Dictionary<string, object>? ProviderOptions { get; init; }
    }
}