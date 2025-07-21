using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.SignalR
{
    /// <summary>
    /// Notification sent when image generation starts
    /// </summary>
    public class ImageGenerationStartedNotification
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
        /// Number of images requested
        /// </summary>
        public int ImageCount { get; set; }
        
        /// <summary>
        /// Provider being used for generation
        /// </summary>
        public string Provider { get; set; } = string.Empty;
        
        /// <summary>
        /// Model being used for generation
        /// </summary>
        public string Model { get; set; } = string.Empty;
        
        /// <summary>
        /// When the generation started
        /// </summary>
        public DateTime StartedAt { get; set; }
    }

    /// <summary>
    /// Notification sent during image generation progress
    /// </summary>
    public class ImageGenerationProgressNotification
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
        /// Number of images completed
        /// </summary>
        public int ImagesCompleted { get; set; }
        
        /// <summary>
        /// Total number of images being generated
        /// </summary>
        public int TotalImages { get; set; }
        
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
    /// Notification sent when image generation completes successfully
    /// </summary>
    public class ImageGenerationCompletedNotification
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
        /// Generated images data
        /// </summary>
        public List<GeneratedImageData> Images { get; set; } = new();
        
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
    /// Notification sent when image generation fails
    /// </summary>
    public class ImageGenerationFailedNotification
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
    /// Notification sent when image generation is cancelled
    /// </summary>
    public class ImageGenerationCancelledNotification
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
    /// Data for a generated image
    /// </summary>
    public class GeneratedImageData
    {
        /// <summary>
        /// Public URL for the image
        /// </summary>
        public string? Url { get; set; }
        
        /// <summary>
        /// Base64 encoded image data (if requested)
        /// </summary>
        public string? B64Json { get; set; }
        
        /// <summary>
        /// Revised prompt if modified by the provider
        /// </summary>
        public string? RevisedPrompt { get; set; }
        
        /// <summary>
        /// Image metadata (dimensions, format, etc.)
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}