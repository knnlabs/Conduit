namespace ConduitLLM.Configuration.DTOs.SignalR
{
    /// <summary>
    /// Status update for async tasks sent via SignalR.
    /// </summary>
    public class AsyncTaskStatusUpdate
    {
        /// <summary>
        /// Gets or sets the task ID.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the task status.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the progress percentage (0-100).
        /// </summary>
        public double? Progress { get; set; }

        /// <summary>
        /// Gets or sets the current step or phase.
        /// </summary>
        public string? CurrentStep { get; set; }

        /// <summary>
        /// Gets or sets a message describing the current state.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets the result data if completed.
        /// </summary>
        public object? Result { get; set; }

        /// <summary>
        /// Gets or sets the error information if failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets when the task started.
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// Gets or sets when the task completed.
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Gets or sets the estimated time remaining in seconds.
        /// </summary>
        public double? EstimatedSecondsRemaining { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of this update.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Video generation task response for SignalR updates.
    /// </summary>
    public class VideoGenerationTaskResponse
    {
        /// <summary>
        /// Gets or sets the task ID.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the task status.
        /// </summary>
        public VideoGenerationTaskStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the progress percentage.
        /// </summary>
        public double? Progress { get; set; }

        /// <summary>
        /// Gets or sets the video URL if completed.
        /// </summary>
        public string? VideoUrl { get; set; }

        /// <summary>
        /// Gets or sets the thumbnail URL if available.
        /// </summary>
        public string? ThumbnailUrl { get; set; }

        /// <summary>
        /// Gets or sets the error message if failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets the video metadata.
        /// </summary>
        public VideoMetadata? Metadata { get; set; }

        /// <summary>
        /// Gets or sets when the generation started.
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// Gets or sets when the generation completed.
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Gets or sets the estimated completion time.
        /// </summary>
        public DateTime? EstimatedCompletionTime { get; set; }
    }

    /// <summary>
    /// Video generation task status enumeration.
    /// </summary>
    public enum VideoGenerationTaskStatus
    {
        /// <summary>
        /// Task is pending.
        /// </summary>
        Pending,

        /// <summary>
        /// Task is processing.
        /// </summary>
        Processing,

        /// <summary>
        /// Task completed successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// Task failed.
        /// </summary>
        Failed,

        /// <summary>
        /// Task was cancelled.
        /// </summary>
        Cancelled
    }

    /// <summary>
    /// Video metadata for generated videos.
    /// </summary>
    public class VideoMetadata
    {
        /// <summary>
        /// Gets or sets the video duration in seconds.
        /// </summary>
        public double? DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the video resolution.
        /// </summary>
        public string? Resolution { get; set; }

        /// <summary>
        /// Gets or sets the video format.
        /// </summary>
        public string? Format { get; set; }

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long? FileSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the frames per second.
        /// </summary>
        public double? Fps { get; set; }

        /// <summary>
        /// Gets or sets additional properties.
        /// </summary>
        public Dictionary<string, object> AdditionalProperties { get; set; } = new();
    }

    /// <summary>
    /// Image generation task response for SignalR updates.
    /// </summary>
    public class ImageGenerationTaskResponse
    {
        /// <summary>
        /// Gets or sets the task ID.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the task status.
        /// </summary>
        public ImageGenerationTaskStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the progress percentage.
        /// </summary>
        public double? Progress { get; set; }

        /// <summary>
        /// Gets or sets the generated image URLs.
        /// </summary>
        public List<string> ImageUrls { get; set; } = new();

        /// <summary>
        /// Gets or sets the error message if failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets the image metadata.
        /// </summary>
        public List<ImageMetadata> Metadata { get; set; } = new();

        /// <summary>
        /// Gets or sets when the generation started.
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// Gets or sets when the generation completed.
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Gets or sets the estimated completion time.
        /// </summary>
        public DateTime? EstimatedCompletionTime { get; set; }
    }

    /// <summary>
    /// Image generation task status enumeration.
    /// </summary>
    public enum ImageGenerationTaskStatus
    {
        /// <summary>
        /// Task is pending.
        /// </summary>
        Pending,

        /// <summary>
        /// Task is processing.
        /// </summary>
        Processing,

        /// <summary>
        /// Task completed successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// Task failed.
        /// </summary>
        Failed,

        /// <summary>
        /// Task was cancelled.
        /// </summary>
        Cancelled
    }

    /// <summary>
    /// Image metadata for generated images.
    /// </summary>
    public class ImageMetadata
    {
        /// <summary>
        /// Gets or sets the image width in pixels.
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// Gets or sets the image height in pixels.
        /// </summary>
        public int? Height { get; set; }

        /// <summary>
        /// Gets or sets the image format.
        /// </summary>
        public string? Format { get; set; }

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long? FileSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the color mode.
        /// </summary>
        public string? ColorMode { get; set; }

        /// <summary>
        /// Gets or sets additional properties.
        /// </summary>
        public Dictionary<string, object> AdditionalProperties { get; set; } = new();
    }
}