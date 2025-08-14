using System;
using System.Collections.Generic;

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

        /// <summary>
        /// Gets or sets whether to automatically configure CORS on bucket initialization.
        /// </summary>
        public bool AutoConfigureCors { get; set; } = true;

        /// <summary>
        /// Gets or sets the allowed origins for CORS configuration.
        /// Default is "*" to allow all origins.
        /// </summary>
        public List<string> CorsAllowedOrigins { get; set; } = new() { "*" };

        /// <summary>
        /// Gets or sets the allowed methods for CORS configuration.
        /// Default is GET and HEAD for media access.
        /// </summary>
        public List<string> CorsAllowedMethods { get; set; } = new() { "GET", "HEAD" };

        /// <summary>
        /// Gets or sets the exposed headers for CORS configuration.
        /// </summary>
        public List<string> CorsExposeHeaders { get; set; } = new() { "ETag", "Content-Length", "Content-Type" };

        /// <summary>
        /// Gets or sets the max age in seconds for CORS preflight cache.
        /// Default is 3600 seconds (1 hour).
        /// </summary>
        public int CorsMaxAgeSeconds { get; set; } = 3600;

        /// <summary>
        /// Gets whether this is Cloudflare R2 service (auto-detected from ServiceUrl).
        /// </summary>
        public bool IsR2 => ServiceUrl?.Contains("r2.cloudflarestorage.com", StringComparison.OrdinalIgnoreCase) ?? false;

        /// <summary>
        /// Gets or sets the size of each part in bytes for multipart uploads.
        /// Default is 10MB which is optimal for R2. AWS S3 minimum is 5MB.
        /// </summary>
        public long MultipartChunkSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB

        /// <summary>
        /// Gets or sets the threshold in bytes for when to use multipart upload.
        /// Default is 100MB. Files larger than this will use multipart upload.
        /// </summary>
        public long MultipartThresholdBytes { get; set; } = 100 * 1024 * 1024; // 100MB
    }
}