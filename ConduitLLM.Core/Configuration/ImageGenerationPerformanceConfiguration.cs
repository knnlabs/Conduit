using System.Collections.Generic;

namespace ConduitLLM.Core.Configuration
{
    /// <summary>
    /// Configuration for image generation performance optimization.
    /// </summary>
    public class ImageGenerationPerformanceConfiguration
    {
        /// <summary>
        /// Maximum concurrent image downloads.
        /// </summary>
        public int MaxConcurrentDownloads { get; set; } = 10;
        
        /// <summary>
        /// Maximum concurrent image generations per provider.
        /// </summary>
        public int MaxConcurrentGenerations { get; set; } = 5;
        
        /// <summary>
        /// Provider-specific concurrency limits.
        /// </summary>
        public Dictionary<string, int> ProviderConcurrencyLimits { get; set; } = new()
        {
            { "openai", 5 },
            { "minimax", 3 },
            { "replicate", 2 }
        };
        
        /// <summary>
        /// Provider-specific timeout settings in seconds.
        /// </summary>
        public Dictionary<string, int> ProviderTimeouts { get; set; } = new()
        {
            { "openai", 120 },      // 2 minutes
            { "minimax", 180 },     // 3 minutes
            { "replicate", 300 }    // 5 minutes
        };
        
        /// <summary>
        /// Provider-specific download timeouts in seconds.
        /// </summary>
        public Dictionary<string, int> ProviderDownloadTimeouts { get; set; } = new()
        {
            { "openai", 30 },
            { "minimax", 45 },
            { "replicate", 60 }
        };
        
        /// <summary>
        /// Configuration for smart provider selection.
        /// </summary>
        public ProviderSelectionConfiguration ProviderSelection { get; set; } = new();
        
        /// <summary>
        /// Enable performance metrics collection.
        /// </summary>
        public bool EnableMetricsCollection { get; set; } = true;
        
        /// <summary>
        /// Metrics retention period in days.
        /// </summary>
        public int MetricsRetentionDays { get; set; } = 7;
        
        /// <summary>
        /// Metrics cleanup interval in hours.
        /// </summary>
        public int MetricsCleanupIntervalHours { get; set; } = 24;
    }
    
    /// <summary>
    /// Configuration for smart provider selection.
    /// </summary>
    public class ProviderSelectionConfiguration
    {
        /// <summary>
        /// Enable smart provider selection when no model is specified.
        /// </summary>
        public bool EnableSmartSelection { get; set; } = true;
        
        /// <summary>
        /// Performance window in minutes for metrics evaluation.
        /// </summary>
        public int PerformanceWindowMinutes { get; set; } = 60;
        
        /// <summary>
        /// Minimum success rate threshold for provider selection.
        /// </summary>
        public double MinSuccessRateThreshold { get; set; } = 0.85;
        
        /// <summary>
        /// Maximum acceptable wait time in seconds for provider selection.
        /// </summary>
        public double? MaxAcceptableWaitTimeSeconds { get; set; }
        
        /// <summary>
        /// Weight for latency in provider scoring (0.0 to 1.0).
        /// </summary>
        public double LatencyWeight { get; set; } = 0.6;
        
        /// <summary>
        /// Weight for success rate in provider scoring (0.0 to 1.0).
        /// </summary>
        public double SuccessRateWeight { get; set; } = 0.3;
        
        /// <summary>
        /// Weight for queue depth in provider scoring (0.0 to 1.0).
        /// </summary>
        public double QueueDepthWeight { get; set; } = 0.1;
        
        /// <summary>
        /// Default model to use when smart selection fails.
        /// </summary>
        public string DefaultFallbackModel { get; set; } = "dall-e-3";
    }
}