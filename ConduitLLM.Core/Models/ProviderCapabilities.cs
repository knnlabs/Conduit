namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Defines the capabilities and supported parameters for a specific provider or model.
    /// </summary>
    public class ProviderCapabilities
    {
        /// <summary>
        /// The provider name (e.g., "openai", "anthropic").
        /// </summary>
        public string Provider { get; set; } = "";
        
        /// <summary>
        /// Optional specific model ID for model-specific capabilities.
        /// </summary>
        public string? ModelId { get; set; }
        
        /// <summary>
        /// Supported chat completion parameters.
        /// </summary>
        public ChatParameterSupport ChatParameters { get; set; } = new();
        
        /// <summary>
        /// Supported features beyond basic chat completion.
        /// </summary>
        public FeatureSupport Features { get; set; } = new();
    }
    
    /// <summary>
    /// Defines which chat completion parameters are supported.
    /// </summary>
    public class ChatParameterSupport
    {
        public bool Temperature { get; set; } = true;
        public bool MaxTokens { get; set; } = true;
        public bool TopP { get; set; }
        public bool TopK { get; set; }
        public bool Stop { get; set; }
        public bool PresencePenalty { get; set; }
        public bool FrequencyPenalty { get; set; }
        public bool LogitBias { get; set; }
        public bool N { get; set; }
        public bool User { get; set; }
        public bool Seed { get; set; }
        public bool ResponseFormat { get; set; }
        public bool Tools { get; set; }
        
        /// <summary>
        /// Parameter-specific constraints or ranges.
        /// </summary>
        public ParameterConstraints? Constraints { get; set; }
    }
    
    /// <summary>
    /// Defines constraints for parameter values.
    /// </summary>
    public class ParameterConstraints
    {
        public Range<double>? TemperatureRange { get; set; }
        public Range<double>? TopPRange { get; set; }
        public Range<int>? TopKRange { get; set; }
        public int? MaxStopSequences { get; set; }
        public int? MaxTokenLimit { get; set; }
    }
    
    /// <summary>
    /// Defines additional features beyond basic chat.
    /// </summary>
    public class FeatureSupport
    {
        public bool Streaming { get; set; } = true;
        public bool Embeddings { get; set; }
        public bool ImageGeneration { get; set; }
        public bool VisionInput { get; set; }
        public bool FunctionCalling { get; set; }
    }
    
    /// <summary>
    /// Represents a numeric range with min and max values.
    /// </summary>
    public class Range<T> where T : struct
    {
        public T Min { get; set; }
        public T Max { get; set; }
        
        public Range(T min, T max)
        {
            Min = min;
            Max = max;
        }
    }
}