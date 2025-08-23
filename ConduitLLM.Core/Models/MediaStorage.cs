namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Represents metadata for media content being stored.
    /// </summary>
    public class MediaMetadata
    {
        /// <summary>
        /// Gets or sets the content type (MIME type) of the media.
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the original filename.
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// Gets or sets the type of media (Image, Video, Audio).
        /// </summary>
        public MediaType MediaType { get; set; }

        /// <summary>
        /// Gets or sets additional metadata key-value pairs.
        /// </summary>
        public Dictionary<string, string> CustomMetadata { get; set; } = new();

        /// <summary>
        /// Gets or sets the virtual key ID that created this media.
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Gets or sets the expiration time for the media.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>
    /// Represents the result of storing media content.
    /// </summary>
    public class MediaStorageResult
    {
        /// <summary>
        /// Gets or sets the unique storage key for the media.
        /// </summary>
        public string StorageKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the public URL for accessing the media.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the size of the media in bytes.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the content hash (SHA256).
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the media was stored.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Represents information about stored media.
    /// </summary>
    public class MediaInfo
    {
        /// <summary>
        /// Gets or sets the unique storage key.
        /// </summary>
        public string StorageKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the content type (MIME type).
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the size in bytes.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the original filename.
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// Gets or sets the media type.
        /// </summary>
        public MediaType MediaType { get; set; }

        /// <summary>
        /// Gets or sets when the media was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the media expires.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets custom metadata.
        /// </summary>
        public Dictionary<string, string> CustomMetadata { get; set; } = new();
    }

    /// <summary>
    /// Represents the type of media content.
    /// </summary>
    public enum MediaType
    {
        /// <summary>
        /// Image content (JPEG, PNG, GIF, etc.).
        /// </summary>
        Image,

        /// <summary>
        /// Video content (MP4, WebM, etc.).
        /// </summary>
        Video,

        /// <summary>
        /// Audio content (MP3, WAV, etc.).
        /// </summary>
        Audio,

        /// <summary>
        /// Other or unknown media type.
        /// </summary>
        Other
    }
}