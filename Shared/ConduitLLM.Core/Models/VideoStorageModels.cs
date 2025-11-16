namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Extended metadata specific to video content.
    /// </summary>
    public class VideoMediaMetadata : MediaMetadata
    {
        /// <summary>
        /// Gets or sets the video duration in seconds.
        /// </summary>
        public double Duration { get; set; }

        /// <summary>
        /// Gets or sets the video resolution (e.g., "1920x1080").
        /// </summary>
        public string Resolution { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the video width in pixels.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the video height in pixels.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets the frame rate (frames per second).
        /// </summary>
        public double FrameRate { get; set; }

        /// <summary>
        /// Gets or sets the video codec (e.g., "h264", "h265").
        /// </summary>
        public string? Codec { get; set; }

        /// <summary>
        /// Gets or sets the video bitrate in bits per second.
        /// </summary>
        public long? Bitrate { get; set; }

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the generation time in seconds.
        /// </summary>
        public double? GenerationTimeSeconds { get; set; }

        /// <summary>
        /// Gets or sets the model used to generate the video.
        /// </summary>
        public string? GeneratedByModel { get; set; }

        /// <summary>
        /// Gets or sets the prompt used to generate the video.
        /// </summary>
        public string? GenerationPrompt { get; set; }

        /// <summary>
        /// Initializes a new instance of VideoMediaMetadata.
        /// </summary>
        public VideoMediaMetadata()
        {
            MediaType = MediaType.Video;
        }
    }

    /// <summary>
    /// Represents a multipart upload session for large video files.
    /// </summary>
    public class MultipartUploadSession
    {
        /// <summary>
        /// Gets or sets the unique session identifier.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the storage key that will be assigned to the completed upload.
        /// </summary>
        public string StorageKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the S3 upload ID (for S3 storage).
        /// </summary>
        public string? S3UploadId { get; set; }

        /// <summary>
        /// Gets or sets when the session was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the session expires.
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the minimum part size in bytes (except last part).
        /// </summary>
        public long MinimumPartSize { get; set; } = 5 * 1024 * 1024; // 5MB default

        /// <summary>
        /// Gets or sets the maximum number of parts allowed.
        /// </summary>
        public int MaxParts { get; set; } = 10000;
    }

    /// <summary>
    /// Represents the result of uploading a single part in a multipart upload.
    /// </summary>
    public class PartUploadResult
    {
        /// <summary>
        /// Gets or sets the part number (1-based).
        /// </summary>
        public int PartNumber { get; set; }

        /// <summary>
        /// Gets or sets the ETag returned by the storage service.
        /// </summary>
        public string ETag { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the size of this part in bytes.
        /// </summary>
        public long SizeBytes { get; set; }
    }

    /// <summary>
    /// Represents a stream with range information for partial content delivery.
    /// </summary>
    public class RangedStream
    {
        /// <summary>
        /// Gets or sets the content stream.
        /// </summary>
        public Stream Stream { get; set; } = Stream.Null;

        /// <summary>
        /// Gets or sets the starting byte position of the range.
        /// </summary>
        public long RangeStart { get; set; }

        /// <summary>
        /// Gets or sets the ending byte position of the range (inclusive).
        /// </summary>
        public long RangeEnd { get; set; }

        /// <summary>
        /// Gets or sets the total size of the complete content.
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// Gets or sets the content type.
        /// </summary>
        public string ContentType { get; set; } = "video/mp4";

        /// <summary>
        /// Gets the length of the ranged content.
        /// </summary>
        public long ContentLength => RangeEnd - RangeStart + 1;

        /// <summary>
        /// Gets whether this represents a partial range.
        /// </summary>
        public bool IsPartial => RangeStart > 0 || RangeEnd < TotalSize - 1;
    }

    /// <summary>
    /// Represents a presigned URL for direct upload to storage.
    /// </summary>
    public class PresignedUploadUrl
    {
        /// <summary>
        /// Gets or sets the presigned upload URL.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the HTTP method to use (usually PUT or POST).
        /// </summary>
        public string HttpMethod { get; set; } = "PUT";

        /// <summary>
        /// Gets or sets additional headers that must be included in the upload request.
        /// </summary>
        public Dictionary<string, string> RequiredHeaders { get; set; } = new();

        /// <summary>
        /// Gets or sets form fields for POST uploads (if using POST method).
        /// </summary>
        public Dictionary<string, string>? FormFields { get; set; }

        /// <summary>
        /// Gets or sets when the presigned URL expires.
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the storage key that will be assigned to the uploaded file.
        /// </summary>
        public string StorageKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the maximum file size allowed for this upload.
        /// </summary>
        public long? MaxFileSizeBytes { get; set; }
    }
}