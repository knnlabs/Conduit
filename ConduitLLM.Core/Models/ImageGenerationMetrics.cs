using System;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Performance metrics specifically for image generation operations.
    /// </summary>
    public class ImageGenerationMetrics
    {
        /// <summary>
        /// Unique identifier for the metric entry.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Provider name that handled the request.
        /// </summary>
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;
        
        /// <summary>
        /// Model name used for the request.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;
        
        /// <summary>
        /// Total generation time in milliseconds (from request to all images ready).
        /// </summary>
        [JsonPropertyName("total_generation_time_ms")]
        public long TotalGenerationTimeMs { get; set; }
        
        /// <summary>
        /// Average generation time per image in milliseconds.
        /// </summary>
        [JsonPropertyName("avg_generation_time_per_image_ms")]
        public double AvgGenerationTimePerImageMs { get; set; }
        
        /// <summary>
        /// Time spent downloading images in milliseconds.
        /// </summary>
        [JsonPropertyName("download_time_ms")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? DownloadTimeMs { get; set; }
        
        /// <summary>
        /// Time spent storing images in milliseconds.
        /// </summary>
        [JsonPropertyName("storage_time_ms")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? StorageTimeMs { get; set; }
        
        /// <summary>
        /// Number of images generated.
        /// </summary>
        [JsonPropertyName("image_count")]
        public int ImageCount { get; set; }
        
        /// <summary>
        /// Size of the request (e.g., "1024x1024").
        /// </summary>
        [JsonPropertyName("image_size")]
        public string ImageSize { get; set; } = string.Empty;
        
        /// <summary>
        /// Quality setting used (if applicable).
        /// </summary>
        [JsonPropertyName("quality")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Quality { get; set; }
        
        /// <summary>
        /// Whether the generation was successful.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        
        /// <summary>
        /// Error code if the generation failed.
        /// </summary>
        [JsonPropertyName("error_code")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ErrorCode { get; set; }
        
        /// <summary>
        /// Whether this was a retry.
        /// </summary>
        [JsonPropertyName("is_retry")]
        public bool IsRetry { get; set; }
        
        /// <summary>
        /// Number of retry attempts.
        /// </summary>
        [JsonPropertyName("retry_count")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int RetryCount { get; set; }
        
        /// <summary>
        /// Timestamp when the generation started.
        /// </summary>
        [JsonPropertyName("started_at")]
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Timestamp when the generation completed.
        /// </summary>
        [JsonPropertyName("completed_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? CompletedAt { get; set; }
        
        /// <summary>
        /// Virtual Key ID that made the request.
        /// </summary>
        [JsonPropertyName("virtual_key_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int VirtualKeyId { get; set; }
        
        /// <summary>
        /// Estimated cost of the generation.
        /// </summary>
        [JsonPropertyName("estimated_cost")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? EstimatedCost { get; set; }
        
        /// <summary>
        /// Current queue depth when request was made (for load estimation).
        /// </summary>
        [JsonPropertyName("queue_depth")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int QueueDepth { get; set; }
        
        /// <summary>
        /// Concurrency level used for this generation.
        /// </summary>
        [JsonPropertyName("concurrency_level")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int ConcurrencyLevel { get; set; }
    }
    
    /// <summary>
    /// Aggregated performance statistics for a provider/model combination.
    /// </summary>
    public class ImageGenerationProviderStats
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;
        
        /// <summary>
        /// Model name.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;
        
        /// <summary>
        /// Average generation time in milliseconds.
        /// </summary>
        [JsonPropertyName("avg_generation_time_ms")]
        public double AvgGenerationTimeMs { get; set; }
        
        /// <summary>
        /// 95th percentile generation time in milliseconds.
        /// </summary>
        [JsonPropertyName("p95_generation_time_ms")]
        public double P95GenerationTimeMs { get; set; }
        
        /// <summary>
        /// Success rate (0.0 to 1.0).
        /// </summary>
        [JsonPropertyName("success_rate")]
        public double SuccessRate { get; set; }
        
        /// <summary>
        /// Total number of requests in the measurement window.
        /// </summary>
        [JsonPropertyName("request_count")]
        public int RequestCount { get; set; }
        
        /// <summary>
        /// Current queue depth.
        /// </summary>
        [JsonPropertyName("current_queue_depth")]
        public int CurrentQueueDepth { get; set; }
        
        /// <summary>
        /// Estimated wait time in seconds based on current performance.
        /// </summary>
        [JsonPropertyName("estimated_wait_time_seconds")]
        public double EstimatedWaitTimeSeconds { get; set; }
        
        /// <summary>
        /// Provider health score (0.0 to 1.0).
        /// </summary>
        [JsonPropertyName("health_score")]
        public double HealthScore { get; set; }
        
        /// <summary>
        /// Whether the provider is currently healthy.
        /// </summary>
        [JsonPropertyName("is_healthy")]
        public bool IsHealthy { get; set; }
        
        /// <summary>
        /// Last updated timestamp.
        /// </summary>
        [JsonPropertyName("last_updated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Time window in minutes for the statistics.
        /// </summary>
        [JsonPropertyName("window_minutes")]
        public int WindowMinutes { get; set; } = 60;
    }
}