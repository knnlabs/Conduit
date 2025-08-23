namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Tracks generated media files (images/videos) for lifecycle management
    /// Enables cleanup of CDN/S3 storage when virtual keys are deleted
    /// </summary>
    public class MediaLifecycleRecord
    {
        /// <summary>
        /// Primary key
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Virtual Key ID that owns this media
        /// </summary>
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// Type of media (Image, Video)
        /// </summary>
        public string MediaType { get; set; } = string.Empty;

        /// <summary>
        /// Public URL where the media is stored
        /// </summary>
        public string MediaUrl { get; set; } = string.Empty;

        /// <summary>
        /// Storage key/path for the media file (for deletion)
        /// </summary>
        public string StorageKey { get; set; } = string.Empty;

        /// <summary>
        /// Size of the media file in bytes
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// MIME type of the media file
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// Model used to generate this media
        /// </summary>
        public string GeneratedByModel { get; set; } = string.Empty;

        /// <summary>
        /// Prompt used to generate this media
        /// </summary>
        public string GenerationPrompt { get; set; } = string.Empty;

        /// <summary>
        /// When the media was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// When the media should expire (if applicable)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Additional metadata as JSON
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// When this record was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When this record was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Whether the media has been deleted from storage
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// When the media was deleted (if applicable)
        /// </summary>
        public DateTime? DeletedAt { get; set; }

        /// <summary>
        /// Navigation property to the virtual key
        /// </summary>
        public virtual VirtualKey VirtualKey { get; set; } = null!;
    }
}