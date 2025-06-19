using System;

namespace ConduitLLM.Core.Options
{
    /// <summary>
    /// Configuration options for S3-compatible storage services.
    /// </summary>
    public class S3StorageOptions
    {
        /// <summary>
        /// Configuration section name.
        /// </summary>
        public const string SectionName = "ConduitLLM:Storage:S3";

        /// <summary>
        /// Gets or sets the S3 endpoint URL (for S3-compatible services like R2, MinIO).
        /// </summary>
        public string? ServiceUrl { get; set; }

        /// <summary>
        /// Gets or sets the access key ID.
        /// </summary>
        public string AccessKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the secret access key.
        /// </summary>
        public string SecretKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the bucket name for storing media.
        /// </summary>
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the region (optional for some S3-compatible services).
        /// </summary>
        public string Region { get; set; } = "auto";

        /// <summary>
        /// Gets or sets whether to use path-style addressing (required for some S3-compatible services).
        /// </summary>
        public bool ForcePathStyle { get; set; } = true;

        /// <summary>
        /// Gets or sets the public base URL for accessing stored files (if different from service URL).
        /// </summary>
        public string? PublicBaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the default URL expiration time.
        /// </summary>
        public TimeSpan DefaultUrlExpiration { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// Gets or sets the maximum file size allowed for upload.
        /// </summary>
        public long MaxFileSizeBytes { get; set; } = 500 * 1024 * 1024; // 500MB

        /// <summary>
        /// Gets or sets whether to automatically create the bucket if it doesn't exist.
        /// </summary>
        public bool AutoCreateBucket { get; set; } = true;
    }
}