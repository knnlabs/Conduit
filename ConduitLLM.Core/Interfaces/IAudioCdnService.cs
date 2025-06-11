using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for CDN integration for audio content delivery.
    /// </summary>
    public interface IAudioCdnService
    {
        /// <summary>
        /// Uploads audio content to CDN.
        /// </summary>
        /// <param name="audioData">The audio data to upload.</param>
        /// <param name="contentType">The content type (e.g., "audio/mp3").</param>
        /// <param name="metadata">Optional metadata.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>CDN URL for the uploaded content.</returns>
        Task<CdnUploadResult> UploadAudioAsync(
            byte[] audioData,
            string contentType,
            CdnMetadata? metadata = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams audio content to CDN with chunked upload.
        /// </summary>
        /// <param name="audioStream">The audio stream.</param>
        /// <param name="contentType">The content type.</param>
        /// <param name="metadata">Optional metadata.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>CDN URL for the uploaded content.</returns>
        Task<CdnUploadResult> StreamUploadAsync(
            Stream audioStream,
            string contentType,
            CdnMetadata? metadata = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a CDN URL for cached content.
        /// </summary>
        /// <param name="contentKey">The content key.</param>
        /// <param name="expiresIn">URL expiration time.</param>
        /// <returns>CDN URL or null if not found.</returns>
        Task<string?> GetCdnUrlAsync(
            string contentKey,
            TimeSpan? expiresIn = null);

        /// <summary>
        /// Invalidates CDN cache for specific content.
        /// </summary>
        /// <param name="contentKey">The content key to invalidate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task InvalidateCacheAsync(
            string contentKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets CDN usage statistics.
        /// </summary>
        /// <param name="startDate">Start date for statistics.</param>
        /// <param name="endDate">End date for statistics.</param>
        /// <returns>CDN usage statistics.</returns>
        Task<CdnUsageStatistics> GetUsageStatisticsAsync(
            DateTime? startDate = null,
            DateTime? endDate = null);

        /// <summary>
        /// Configures CDN edge locations for optimal delivery.
        /// </summary>
        /// <param name="config">Edge configuration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ConfigureEdgeLocationsAsync(
            CdnEdgeConfiguration config,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of CDN upload operation.
    /// </summary>
    public class CdnUploadResult
    {
        /// <summary>
        /// Gets or sets the CDN URL.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the content key.
        /// </summary>
        public string ContentKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the content hash.
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the upload timestamp.
        /// </summary>
        public DateTime UploadedAt { get; set; }

        /// <summary>
        /// Gets or sets the content size in bytes.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Gets or sets edge locations where content is cached.
        /// </summary>
        public List<string> EdgeLocations { get; set; } = new();
    }

    /// <summary>
    /// Metadata for CDN content.
    /// </summary>
    public class CdnMetadata
    {
        /// <summary>
        /// Gets or sets the content duration in seconds.
        /// </summary>
        public double? DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the audio format.
        /// </summary>
        public string? AudioFormat { get; set; }

        /// <summary>
        /// Gets or sets the bit rate.
        /// </summary>
        public int? BitRate { get; set; }

        /// <summary>
        /// Gets or sets the language.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Gets or sets cache control headers.
        /// </summary>
        public string? CacheControl { get; set; }

        /// <summary>
        /// Gets or sets custom metadata.
        /// </summary>
        public Dictionary<string, string> CustomMetadata { get; set; } = new();
    }

    /// <summary>
    /// CDN usage statistics.
    /// </summary>
    public class CdnUsageStatistics
    {
        /// <summary>
        /// Gets or sets total bandwidth used in bytes.
        /// </summary>
        public long TotalBandwidthBytes { get; set; }

        /// <summary>
        /// Gets or sets total number of requests.
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Gets or sets cache hit rate.
        /// </summary>
        public double CacheHitRate { get; set; }

        /// <summary>
        /// Gets or sets average response time in milliseconds.
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// Gets or sets bandwidth by region.
        /// </summary>
        public Dictionary<string, long> BandwidthByRegion { get; set; } = new();

        /// <summary>
        /// Gets or sets requests by content type.
        /// </summary>
        public Dictionary<string, long> RequestsByContentType { get; set; } = new();

        /// <summary>
        /// Gets or sets top content by requests.
        /// </summary>
        public List<TopContent> TopContent { get; set; } = new();
    }

    /// <summary>
    /// Top content information.
    /// </summary>
    public class TopContent
    {
        /// <summary>
        /// Gets or sets the content key.
        /// </summary>
        public string ContentKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of requests.
        /// </summary>
        public long Requests { get; set; }

        /// <summary>
        /// Gets or sets the bandwidth used.
        /// </summary>
        public long BandwidthBytes { get; set; }
    }

    /// <summary>
    /// CDN edge location configuration.
    /// </summary>
    public class CdnEdgeConfiguration
    {
        /// <summary>
        /// Gets or sets priority regions for content distribution.
        /// </summary>
        public List<string> PriorityRegions { get; set; } = new();

        /// <summary>
        /// Gets or sets whether to enable auto-scaling.
        /// </summary>
        public bool EnableAutoScaling { get; set; }

        /// <summary>
        /// Gets or sets custom routing rules.
        /// </summary>
        public List<CdnRoutingRule> RoutingRules { get; set; } = new();
    }

    /// <summary>
    /// CDN routing rule.
    /// </summary>
    public class CdnRoutingRule
    {
        /// <summary>
        /// Gets or sets the source region.
        /// </summary>
        public string SourceRegion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target edge location.
        /// </summary>
        public string TargetEdgeLocation { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the routing weight (0-100).
        /// </summary>
        public int Weight { get; set; }
    }
}