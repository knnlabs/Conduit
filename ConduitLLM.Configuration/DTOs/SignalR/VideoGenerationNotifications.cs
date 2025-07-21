using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.SignalR
{
    /// <summary>
    /// Notification sent when video generation starts
    /// </summary>
    public class VideoGenerationStartedNotification
    {
        /// <summary>
        /// Unique task identifier
        /// </summary>
        public string TaskId { get; set; } = string.Empty;
        
        /// <summary>
        /// Virtual key ID that initiated the generation
        /// </summary>
        public int VirtualKeyId { get; set; }
        
        /// <summary>
        /// The prompt being used for generation
        /// </summary>
        public string Prompt { get; set; } = string.Empty;
        
        /// <summary>
        /// Provider being used for generation
        /// </summary>
        public string Provider { get; set; } = string.Empty;
        
        /// <summary>
        /// Model being used for generation
        /// </summary>
        public string Model { get; set; } = string.Empty;
        
        /// <summary>
        /// Duration of the video in seconds
        /// </summary>
        public int? Duration { get; set; }
        
        /// <summary>
        /// Resolution of the video (e.g., "1920x1080")
        /// </summary>
        public string? Resolution { get; set; }
        
        /// <summary>
        /// Frames per second
        /// </summary>
        public int? Fps { get; set; }
        
        /// <summary>
        /// When the generation started
        /// </summary>
        public DateTime StartedAt { get; set; }
        
        /// <summary>
        /// Estimated time in seconds for completion
        /// </summary>
        public int? EstimatedSeconds { get; set; }
    }

    /// <summary>
    /// Notification sent during video generation progress
    /// </summary>
    public class VideoGenerationProgressNotification
    {
        /// <summary>
        /// Unique task identifier
        /// </summary>
        public string TaskId { get; set; } = string.Empty;
        
        /// <summary>
        /// Virtual key ID
        /// </summary>
        public int VirtualKeyId { get; set; }
        
        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public int ProgressPercentage { get; set; }
        
        /// <summary>
        /// Current status message
        /// </summary>
        public string Status { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional detailed message
        /// </summary>
        public string? Message { get; set; }
        
        /// <summary>
        /// When this progress update was sent
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Notification sent when video generation completes successfully
    /// </summary>
    public class VideoGenerationCompletedNotification
    {
        /// <summary>
        /// Unique task identifier
        /// </summary>
        public string TaskId { get; set; } = string.Empty;
        
        /// <summary>
        /// Virtual key ID
        /// </summary>
        public int VirtualKeyId { get; set; }
        
        /// <summary>
        /// Generated video data
        /// </summary>
        public GeneratedVideoData Video { get; set; } = new();
        
        /// <summary>
        /// Total generation duration
        /// </summary>
        public TimeSpan Duration { get; set; }
        
        /// <summary>
        /// Total cost incurred
        /// </summary>
        public decimal Cost { get; set; }
        
        /// <summary>
        /// Provider used for generation
        /// </summary>
        public string Provider { get; set; } = string.Empty;
        
        /// <summary>
        /// Model used for generation
        /// </summary>
        public string Model { get; set; } = string.Empty;
        
        /// <summary>
        /// When generation completed
        /// </summary>
        public DateTime CompletedAt { get; set; }
    }

    /// <summary>
    /// Notification sent when video generation fails
    /// </summary>
    public class VideoGenerationFailedNotification
    {
        /// <summary>
        /// Unique task identifier
        /// </summary>
        public string TaskId { get; set; } = string.Empty;
        
        /// <summary>
        /// Virtual key ID
        /// </summary>
        public int VirtualKeyId { get; set; }
        
        /// <summary>
        /// Error message
        /// </summary>
        public string Error { get; set; } = string.Empty;
        
        /// <summary>
        /// Error code if available
        /// </summary>
        public string? ErrorCode { get; set; }
        
        /// <summary>
        /// Provider that failed
        /// </summary>
        public string Provider { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether this error is retryable
        /// </summary>
        public bool IsRetryable { get; set; }
        
        /// <summary>
        /// Number of attempts made
        /// </summary>
        public int AttemptCount { get; set; }
        
        /// <summary>
        /// When the failure occurred
        /// </summary>
        public DateTime FailedAt { get; set; }
    }

    /// <summary>
    /// Notification sent when video generation is cancelled
    /// </summary>
    public class VideoGenerationCancelledNotification
    {
        /// <summary>
        /// Unique task identifier
        /// </summary>
        public string TaskId { get; set; } = string.Empty;
        
        /// <summary>
        /// Virtual key ID
        /// </summary>
        public int VirtualKeyId { get; set; }
        
        /// <summary>
        /// Reason for cancellation
        /// </summary>
        public string? Reason { get; set; }
        
        /// <summary>
        /// When the cancellation occurred
        /// </summary>
        public DateTime CancelledAt { get; set; }
    }

    /// <summary>
    /// Data for a generated video
    /// </summary>
    public class GeneratedVideoData
    {
        /// <summary>
        /// Public URL for the video
        /// </summary>
        public string? Url { get; set; }
        
        /// <summary>
        /// Base64 encoded video data (if requested)
        /// </summary>
        public string? B64Json { get; set; }
        
        /// <summary>
        /// Revised prompt if modified by the provider
        /// </summary>
        public string? RevisedPrompt { get; set; }
        
        /// <summary>
        /// Video metadata
        /// </summary>
        public VideoMetadataInfo Metadata { get; set; } = new();
    }

    /// <summary>
    /// Metadata about a generated video
    /// </summary>
    public class VideoMetadataInfo
    {
        /// <summary>
        /// Width of the video in pixels
        /// </summary>
        public int Width { get; set; }
        
        /// <summary>
        /// Height of the video in pixels
        /// </summary>
        public int Height { get; set; }
        
        /// <summary>
        /// Duration of the video in seconds
        /// </summary>
        public double Duration { get; set; }
        
        /// <summary>
        /// Frames per second
        /// </summary>
        public double Fps { get; set; }
        
        /// <summary>
        /// Video codec (e.g., "h264", "vp9")
        /// </summary>
        public string? Codec { get; set; }
        
        /// <summary>
        /// File size in bytes
        /// </summary>
        public long FileSizeBytes { get; set; }
        
        /// <summary>
        /// MIME type of the video file
        /// </summary>
        public string? MimeType { get; set; }
        
        /// <summary>
        /// Container format (e.g., "mp4", "webm")
        /// </summary>
        public string? Format { get; set; }
    }
}