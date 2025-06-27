using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.SignalR
{
    /// <summary>
    /// Notification when new models are discovered for a provider
    /// </summary>
    public class NewModelsDiscoveredNotification
    {
        /// <summary>
        /// Provider name
        /// </summary>
        public string Provider { get; set; } = string.Empty;
        
        /// <summary>
        /// List of newly discovered models
        /// </summary>
        public List<DiscoveredModelInfo> NewModels { get; set; } = new();
        
        /// <summary>
        /// Total number of models now available
        /// </summary>
        public int TotalModelCount { get; set; }
        
        /// <summary>
        /// When the discovery occurred
        /// </summary>
        public DateTime DiscoveredAt { get; set; }
    }

    /// <summary>
    /// Notification when model capabilities change
    /// </summary>
    public class ModelCapabilitiesChangedNotification
    {
        /// <summary>
        /// Provider name
        /// </summary>
        public string Provider { get; set; } = string.Empty;
        
        /// <summary>
        /// Model ID
        /// </summary>
        public string ModelId { get; set; } = string.Empty;
        
        /// <summary>
        /// Previous capabilities
        /// </summary>
        public ModelCapabilityInfo? PreviousCapabilities { get; set; }
        
        /// <summary>
        /// New capabilities
        /// </summary>
        public ModelCapabilityInfo NewCapabilities { get; set; } = new();
        
        /// <summary>
        /// List of changes
        /// </summary>
        public List<string> Changes { get; set; } = new();
        
        /// <summary>
        /// When the change was detected
        /// </summary>
        public DateTime ChangedAt { get; set; }
    }

    /// <summary>
    /// Notification when model pricing updates
    /// </summary>
    public class ModelPricingUpdatedNotification
    {
        /// <summary>
        /// Provider name
        /// </summary>
        public string Provider { get; set; } = string.Empty;
        
        /// <summary>
        /// Model ID
        /// </summary>
        public string ModelId { get; set; } = string.Empty;
        
        /// <summary>
        /// Previous pricing (if available)
        /// </summary>
        public ModelPricingInfo? PreviousPricing { get; set; }
        
        /// <summary>
        /// New pricing
        /// </summary>
        public ModelPricingInfo NewPricing { get; set; } = new();
        
        /// <summary>
        /// Percentage change in cost (if calculable)
        /// </summary>
        public decimal? PercentageChange { get; set; }
        
        /// <summary>
        /// When the pricing update was detected
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Notification when a model is deprecated
    /// </summary>
    public class ModelDeprecatedNotification
    {
        /// <summary>
        /// Provider name
        /// </summary>
        public string Provider { get; set; } = string.Empty;
        
        /// <summary>
        /// Model ID
        /// </summary>
        public string ModelId { get; set; } = string.Empty;
        
        /// <summary>
        /// Deprecation date
        /// </summary>
        public DateTime DeprecationDate { get; set; }
        
        /// <summary>
        /// Sunset date (when model will be removed)
        /// </summary>
        public DateTime? SunsetDate { get; set; }
        
        /// <summary>
        /// Suggested replacement model
        /// </summary>
        public string? ReplacementModel { get; set; }
        
        /// <summary>
        /// Additional deprecation notes
        /// </summary>
        public string? Notes { get; set; }
        
        /// <summary>
        /// When the deprecation was announced
        /// </summary>
        public DateTime AnnouncedAt { get; set; }
    }

    /// <summary>
    /// Notification about model availability by region
    /// </summary>
    public class ModelRegionalAvailabilityNotification
    {
        /// <summary>
        /// Provider name
        /// </summary>
        public string Provider { get; set; } = string.Empty;
        
        /// <summary>
        /// Model ID
        /// </summary>
        public string ModelId { get; set; } = string.Empty;
        
        /// <summary>
        /// Regions where the model is now available
        /// </summary>
        public List<string> AvailableRegions { get; set; } = new();
        
        /// <summary>
        /// Regions where the model is no longer available
        /// </summary>
        public List<string> UnavailableRegions { get; set; } = new();
        
        /// <summary>
        /// When the availability changed
        /// </summary>
        public DateTime ChangedAt { get; set; }
    }

    /// <summary>
    /// Notification about model performance metrics
    /// </summary>
    public class ModelPerformanceMetricsNotification
    {
        /// <summary>
        /// Provider name
        /// </summary>
        public string Provider { get; set; } = string.Empty;
        
        /// <summary>
        /// Model ID
        /// </summary>
        public string ModelId { get; set; } = string.Empty;
        
        /// <summary>
        /// Average latency in milliseconds
        /// </summary>
        public double AverageLatencyMs { get; set; }
        
        /// <summary>
        /// 95th percentile latency
        /// </summary>
        public double P95LatencyMs { get; set; }
        
        /// <summary>
        /// 99th percentile latency
        /// </summary>
        public double P99LatencyMs { get; set; }
        
        /// <summary>
        /// Tokens per second (for text models)
        /// </summary>
        public double? TokensPerSecond { get; set; }
        
        /// <summary>
        /// Success rate percentage
        /// </summary>
        public double SuccessRate { get; set; }
        
        /// <summary>
        /// Number of samples used for metrics
        /// </summary>
        public int SampleCount { get; set; }
        
        /// <summary>
        /// Time period for metrics
        /// </summary>
        public string TimePeriod { get; set; } = string.Empty;
        
        /// <summary>
        /// When the metrics were calculated
        /// </summary>
        public DateTime CalculatedAt { get; set; }
    }

    /// <summary>
    /// Information about a discovered model
    /// </summary>
    public class DiscoveredModelInfo
    {
        /// <summary>
        /// Model ID
        /// </summary>
        public string ModelId { get; set; } = string.Empty;
        
        /// <summary>
        /// Display name
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// Model capabilities
        /// </summary>
        public ModelCapabilityInfo Capabilities { get; set; } = new();
        
        /// <summary>
        /// Pricing information (if available)
        /// </summary>
        public ModelPricingInfo? Pricing { get; set; }
        
        /// <summary>
        /// Available regions
        /// </summary>
        public List<string>? Regions { get; set; }
        
        /// <summary>
        /// Release date
        /// </summary>
        public DateTime? ReleaseDate { get; set; }
        
        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Model capability information
    /// </summary>
    public class ModelCapabilityInfo
    {
        public bool Chat { get; set; }
        public bool ChatStream { get; set; }
        public bool Vision { get; set; }
        public bool ImageGeneration { get; set; }
        public bool VideoGeneration { get; set; }
        public bool AudioTranscription { get; set; }
        public bool TextToSpeech { get; set; }
        public bool Embeddings { get; set; }
        public bool FunctionCalling { get; set; }
        public bool ToolUse { get; set; }
        public bool JsonMode { get; set; }
        public int? MaxTokens { get; set; }
        public int? MaxOutputTokens { get; set; }
        public List<string>? SupportedImageSizes { get; set; }
        public List<string>? SupportedVideoSizes { get; set; }
        public Dictionary<string, object>? AdditionalCapabilities { get; set; }
    }

    /// <summary>
    /// Model pricing information
    /// </summary>
    public class ModelPricingInfo
    {
        /// <summary>
        /// Cost per 1K input tokens (for text models)
        /// </summary>
        public decimal? InputTokenCost { get; set; }
        
        /// <summary>
        /// Cost per 1K output tokens (for text models)
        /// </summary>
        public decimal? OutputTokenCost { get; set; }
        
        /// <summary>
        /// Cost per image (for image generation)
        /// </summary>
        public decimal? ImageCost { get; set; }
        
        /// <summary>
        /// Cost per second (for video/audio)
        /// </summary>
        public decimal? PerSecondCost { get; set; }
        
        /// <summary>
        /// Cost per minute (for video/audio)
        /// </summary>
        public decimal? PerMinuteCost { get; set; }
        
        /// <summary>
        /// Currency
        /// </summary>
        public string Currency { get; set; } = "USD";
        
        /// <summary>
        /// Pricing tier or plan
        /// </summary>
        public string? PricingTier { get; set; }
        
        /// <summary>
        /// Effective date
        /// </summary>
        public DateTime? EffectiveDate { get; set; }
    }
}