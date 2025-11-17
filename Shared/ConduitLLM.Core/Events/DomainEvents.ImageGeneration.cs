namespace ConduitLLM.Core.Events
{
    // ===============================
    // Image Generation Domain Events
    // ===============================

    /// <summary>
    /// Raised when an image generation request is submitted
    /// Enables async processing across multiple service instances
    /// </summary>
    public record ImageGenerationRequested : DomainEvent
    {
        /// <summary>
        /// Unique task identifier for tracking
        /// </summary>
        public string TaskId { get; init; } = string.Empty;
        
        /// <summary>
        /// Virtual Key ID for authorization and spend tracking
        /// </summary>
        public int VirtualKeyId { get; init; }
        
        /// <summary>
        /// Virtual Key hash for quick lookups
        /// </summary>
        public string VirtualKeyHash { get; init; } = string.Empty;
        
        /// <summary>
        /// The image generation request details
        /// </summary>
        public ImageGenerationRequest Request { get; init; } = new();
        
        /// <summary>
        /// User identifier for tracking and logging
        /// </summary>
        public string UserId { get; init; } = string.Empty;
        
        /// <summary>
        /// Priority level for queue processing (0 = normal, higher = more priority)
        /// </summary>
        public int Priority { get; init; } = 0;
        
        /// <summary>
        /// When the request was submitted
        /// </summary>
        public DateTime RequestedAt { get; init; } = DateTime.UtcNow;
        
        /// <summary>
        /// Optional webhook URL for status notifications
        /// </summary>
        public string? WebhookUrl { get; init; }
        
        /// <summary>
        /// Optional custom headers for webhook requests
        /// </summary>
        public Dictionary<string, string>? WebhookHeaders { get; init; }
        
        /// <summary>
        /// Partition key for ordered processing per virtual key
        /// </summary>
        public string PartitionKey => VirtualKeyId.ToString();
    }

    /// <summary>
    /// Raised when image generation progress updates occur
    /// Enables real-time progress tracking
    /// </summary>
    public record ImageGenerationProgress : DomainEvent
    {
        /// <summary>
        /// Task identifier
        /// </summary>
        public string TaskId { get; init; } = string.Empty;
        
        /// <summary>
        /// Number of images completed
        /// </summary>
        public int ImagesCompleted { get; init; }
        
        /// <summary>
        /// Total number of images requested
        /// </summary>
        public int TotalImages { get; init; }
        
        /// <summary>
        /// Current status (queued, processing, downloading, storing)
        /// </summary>
        public string Status { get; init; } = string.Empty;
        
        /// <summary>
        /// Optional status message
        /// </summary>
        public string? Message { get; init; }
        
        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public int ProgressPercentage => TotalImages > 0 ? (ImagesCompleted * 100 / TotalImages) : 0;
    }

    /// <summary>
    /// Raised when image generation completes successfully
    /// Critical for spend tracking and result delivery
    /// </summary>
    public record ImageGenerationCompleted : DomainEvent
    {
        /// <summary>
        /// Task identifier
        /// </summary>
        public string TaskId { get; init; } = string.Empty;
        
        /// <summary>
        /// Virtual Key ID for spend tracking
        /// </summary>
        public int VirtualKeyId { get; init; }
        
        /// <summary>
        /// Generated image data
        /// </summary>
        public List<ImageData> Images { get; init; } = new();
        
        /// <summary>
        /// Total generation duration
        /// </summary>
        public TimeSpan Duration { get; init; }
        
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
        /// Partition key for ordered processing per virtual key
        /// </summary>
        public string PartitionKey => VirtualKeyId.ToString();
    }

    /// <summary>
    /// Raised when image generation fails
    /// Enables error tracking and retry logic
    /// </summary>
    public record ImageGenerationFailed : DomainEvent
    {
        /// <summary>
        /// Task identifier
        /// </summary>
        public string TaskId { get; init; } = string.Empty;
        
        /// <summary>
        /// Virtual Key ID for tracking
        /// </summary>
        public int VirtualKeyId { get; init; }
        
        /// <summary>
        /// Error message
        /// </summary>
        public string Error { get; init; } = string.Empty;
        
        /// <summary>
        /// Error code if available
        /// </summary>
        public string? ErrorCode { get; init; }
        
        /// <summary>
        /// Provider that failed
        /// </summary>
        public string Provider { get; init; } = string.Empty;
        
        /// <summary>
        /// Whether this error is retryable
        /// </summary>
        public bool IsRetryable { get; init; }
        
        /// <summary>
        /// Number of attempts made
        /// </summary>
        public int AttemptCount { get; init; } = 1;
        
        /// <summary>
        /// Partition key for ordered processing per virtual key
        /// </summary>
        public string PartitionKey => VirtualKeyId.ToString();
    }

    /// <summary>
    /// Raised when an image generation is cancelled
    /// </summary>
    public record ImageGenerationCancelled : DomainEvent
    {
        /// <summary>
        /// Task identifier
        /// </summary>
        public string TaskId { get; init; } = string.Empty;
        
        /// <summary>
        /// Virtual Key ID for tracking
        /// </summary>
        public int VirtualKeyId { get; init; }
        
        /// <summary>
        /// Reason for cancellation
        /// </summary>
        public string? Reason { get; init; }
        
        /// <summary>
        /// When the cancellation occurred
        /// </summary>
        public DateTime CancelledAt { get; init; } = DateTime.UtcNow;
        
        /// <summary>
        /// Partition key for ordered processing per virtual key
        /// </summary>
        public string PartitionKey => VirtualKeyId.ToString();
    }

    /// <summary>
    /// Image data structure for generation results
    /// </summary>
    public record ImageData
    {
        /// <summary>
        /// Public URL for the image (if stored)
        /// </summary>
        public string? Url { get; init; }
        
        /// <summary>
        /// Base64 encoded image data (if requested)
        /// </summary>
        public string? B64Json { get; init; }
        
        /// <summary>
        /// Revised prompt if modified by the provider
        /// </summary>
        public string? RevisedPrompt { get; init; }
        
        /// <summary>
        /// Image metadata (dimensions, format, etc.)
        /// </summary>
        public Dictionary<string, object> Metadata { get; init; } = new();
    }

    /// <summary>
    /// Image generation request structure
    /// </summary>
    public record ImageGenerationRequest
    {
        /// <summary>
        /// The prompt to generate images from
        /// </summary>
        public string Prompt { get; init; } = string.Empty;
        
        /// <summary>
        /// Model to use for generation
        /// </summary>
        public string? Model { get; init; }
        
        /// <summary>
        /// Number of images to generate (1-10)
        /// </summary>
        public int N { get; init; } = 1;
        
        /// <summary>
        /// Image size (e.g., "1024x1024", "1792x1024")
        /// </summary>
        public string? Size { get; init; }
        
        /// <summary>
        /// Quality setting (standard, hd)
        /// </summary>
        public string? Quality { get; init; }
        
        /// <summary>
        /// Style setting (vivid, natural)
        /// </summary>
        public string? Style { get; init; }
        
        /// <summary>
        /// Response format (url, b64_json)
        /// </summary>
        public string? ResponseFormat { get; init; }
        
        /// <summary>
        /// User identifier for tracking
        /// </summary>
        public string? User { get; init; }
        
        /// <summary>
        /// Base64-encoded image to use as input for image-to-image generation.
        /// When provided, the prompt will be used to modify or enhance this image.
        /// </summary>
        public string? Image { get; init; }
        
        /// <summary>
        /// Base64-encoded mask image for image editing (PNG with transparency).
        /// Only the transparent areas will be edited when both image and mask are provided.
        /// </summary>
        public string? Mask { get; init; }
        
        /// <summary>
        /// The operation type for image generation.
        /// - "generate": Standard text-to-image generation (default)
        /// - "edit": Edit existing image using prompt and optional mask
        /// - "variation": Create variations of existing image
        /// </summary>
        public string Operation { get; init; } = "generate";
        
        /// <summary>
        /// Additional model-specific parameters that are passed through to the provider API.
        /// </summary>
        public Dictionary<string, System.Text.Json.JsonElement>? ExtensionData { get; init; }
    }
}