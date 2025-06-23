using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.AdminClient.Models;

/// <summary>
/// Represents a model provider mapping.
/// </summary>
public class ModelProviderMappingDto
{
    /// <summary>
    /// Gets or sets the mapping ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the model ID.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider ID.
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider-specific model ID.
    /// </summary>
    public string ProviderModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this mapping is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the priority of this mapping (0-100, higher is preferred).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets optional metadata as a JSON string.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports vision/image input capabilities.
    /// </summary>
    public bool SupportsVision { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports image generation capabilities.
    /// </summary>
    public bool SupportsImageGeneration { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports audio transcription capabilities.
    /// </summary>
    public bool SupportsAudioTranscription { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports text-to-speech capabilities.
    /// </summary>
    public bool SupportsTextToSpeech { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports real-time audio streaming capabilities.
    /// </summary>
    public bool SupportsRealtimeAudio { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports function calling.
    /// </summary>
    public bool SupportsFunctionCalling { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports streaming responses.
    /// </summary>
    public bool SupportsStreaming { get; set; }

    /// <summary>
    /// Gets or sets optional model capabilities (e.g., vision, function-calling).
    /// </summary>
    public string? Capabilities { get; set; }

    /// <summary>
    /// Gets or sets the optional maximum context length.
    /// </summary>
    public int? MaxContextLength { get; set; }

    /// <summary>
    /// Gets or sets the maximum output tokens for this model.
    /// </summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>
    /// Gets or sets supported languages for transcription/TTS (comma-separated).
    /// </summary>
    public string? SupportedLanguages { get; set; }

    /// <summary>
    /// Gets or sets supported voices for TTS (comma-separated).
    /// </summary>
    public string? SupportedVoices { get; set; }

    /// <summary>
    /// Gets or sets supported input formats (comma-separated).
    /// </summary>
    public string? SupportedFormats { get; set; }

    /// <summary>
    /// Gets or sets the tokenizer type used by this model.
    /// </summary>
    public string? TokenizerType { get; set; }

    /// <summary>
    /// Gets or sets whether this mapping is the default for its capability type.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the capability type this mapping is default for (e.g., 'chat', 'image-generation').
    /// </summary>
    public string? DefaultCapabilityType { get; set; }
}

/// <summary>
/// Represents a request to create a new model provider mapping.
/// </summary>
public class CreateModelProviderMappingDto
{
    /// <summary>
    /// Gets or sets the model ID.
    /// </summary>
    [Required]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider ID.
    /// </summary>
    [Required]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider-specific model ID.
    /// </summary>
    [Required]
    public string ProviderModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this mapping is enabled (default: true).
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the priority of this mapping (0-100, higher is preferred).
    /// </summary>
    [Range(0, 100)]
    public int? Priority { get; set; }

    /// <summary>
    /// Gets or sets optional metadata as a JSON string.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports vision/image input capabilities.
    /// </summary>
    public bool? SupportsVision { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports image generation capabilities.
    /// </summary>
    public bool? SupportsImageGeneration { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports audio transcription capabilities.
    /// </summary>
    public bool? SupportsAudioTranscription { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports text-to-speech capabilities.
    /// </summary>
    public bool? SupportsTextToSpeech { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports real-time audio streaming capabilities.
    /// </summary>
    public bool? SupportsRealtimeAudio { get; set; }

    /// <summary>
    /// Gets or sets optional model capabilities (e.g., vision, function-calling).
    /// </summary>
    public string? Capabilities { get; set; }

    /// <summary>
    /// Gets or sets the optional maximum context length.
    /// </summary>
    public int? MaxContextLength { get; set; }

    /// <summary>
    /// Gets or sets supported languages for transcription/TTS (comma-separated).
    /// </summary>
    public string? SupportedLanguages { get; set; }

    /// <summary>
    /// Gets or sets supported voices for TTS (comma-separated).
    /// </summary>
    public string? SupportedVoices { get; set; }

    /// <summary>
    /// Gets or sets whether this mapping is the default for its capability type.
    /// </summary>
    public bool? IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the capability type this mapping is default for (e.g., 'chat', 'image-generation').
    /// </summary>
    public string? DefaultCapabilityType { get; set; }

    /// <summary>
    /// Gets or sets supported input formats (comma-separated).
    /// </summary>
    public string? SupportedFormats { get; set; }

    /// <summary>
    /// Gets or sets the tokenizer type used by this model.
    /// </summary>
    public string? TokenizerType { get; set; }

    /// <summary>
    /// Gets or sets the maximum output tokens for this model.
    /// </summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports function calling.
    /// </summary>
    public bool? SupportsFunctionCalling { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports streaming responses.
    /// </summary>
    public bool? SupportsStreaming { get; set; }
}

/// <summary>
/// Represents a request to update an existing model provider mapping.
/// </summary>
public class UpdateModelProviderMappingDto
{
    /// <summary>
    /// Gets or sets the provider ID.
    /// </summary>
    public string? ProviderId { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific model ID.
    /// </summary>
    public string? ProviderModelId { get; set; }

    /// <summary>
    /// Gets or sets whether this mapping is enabled.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the priority of this mapping (0-100, higher is preferred).
    /// </summary>
    [Range(0, 100)]
    public int? Priority { get; set; }

    /// <summary>
    /// Gets or sets optional metadata as a JSON string.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports vision/image input capabilities.
    /// </summary>
    public bool? SupportsVision { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports image generation capabilities.
    /// </summary>
    public bool? SupportsImageGeneration { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports audio transcription capabilities.
    /// </summary>
    public bool? SupportsAudioTranscription { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports text-to-speech capabilities.
    /// </summary>
    public bool? SupportsTextToSpeech { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports real-time audio streaming capabilities.
    /// </summary>
    public bool? SupportsRealtimeAudio { get; set; }

    /// <summary>
    /// Gets or sets optional model capabilities (e.g., vision, function-calling).
    /// </summary>
    public string? Capabilities { get; set; }

    /// <summary>
    /// Gets or sets the optional maximum context length.
    /// </summary>
    public int? MaxContextLength { get; set; }

    /// <summary>
    /// Gets or sets supported languages for transcription/TTS (comma-separated).
    /// </summary>
    public string? SupportedLanguages { get; set; }

    /// <summary>
    /// Gets or sets supported voices for TTS (comma-separated).
    /// </summary>
    public string? SupportedVoices { get; set; }

    /// <summary>
    /// Gets or sets whether this mapping is the default for its capability type.
    /// </summary>
    public bool? IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the capability type this mapping is default for (e.g., 'chat', 'image-generation').
    /// </summary>
    public string? DefaultCapabilityType { get; set; }

    /// <summary>
    /// Gets or sets supported input formats (comma-separated).
    /// </summary>
    public string? SupportedFormats { get; set; }

    /// <summary>
    /// Gets or sets the tokenizer type used by this model.
    /// </summary>
    public string? TokenizerType { get; set; }

    /// <summary>
    /// Gets or sets the maximum output tokens for this model.
    /// </summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports function calling.
    /// </summary>
    public bool? SupportsFunctionCalling { get; set; }

    /// <summary>
    /// Gets or sets whether this model supports streaming responses.
    /// </summary>
    public bool? SupportsStreaming { get; set; }
}

/// <summary>
/// Represents filter criteria for querying model provider mappings.
/// </summary>
public class ModelMappingFilters : FilterOptions
{
    /// <summary>
    /// Gets or sets the model ID filter.
    /// </summary>
    public string? ModelId { get; set; }

    /// <summary>
    /// Gets or sets the provider ID filter.
    /// </summary>
    public string? ProviderId { get; set; }

    /// <summary>
    /// Gets or sets the enabled status filter.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the minimum priority filter.
    /// </summary>
    public int? MinPriority { get; set; }

    /// <summary>
    /// Gets or sets the maximum priority filter.
    /// </summary>
    public int? MaxPriority { get; set; }

    /// <summary>
    /// Gets or sets the vision support filter.
    /// </summary>
    public bool? SupportsVision { get; set; }

    /// <summary>
    /// Gets or sets the image generation support filter.
    /// </summary>
    public bool? SupportsImageGeneration { get; set; }

    /// <summary>
    /// Gets or sets the audio transcription support filter.
    /// </summary>
    public bool? SupportsAudioTranscription { get; set; }

    /// <summary>
    /// Gets or sets the text-to-speech support filter.
    /// </summary>
    public bool? SupportsTextToSpeech { get; set; }

    /// <summary>
    /// Gets or sets the real-time audio support filter.
    /// </summary>
    public bool? SupportsRealtimeAudio { get; set; }

    /// <summary>
    /// Gets or sets the default mapping filter.
    /// </summary>
    public bool? IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the default capability type filter.
    /// </summary>
    public string? DefaultCapabilityType { get; set; }

    /// <summary>
    /// Gets or sets the function calling support filter.
    /// </summary>
    public bool? SupportsFunctionCalling { get; set; }

    /// <summary>
    /// Gets or sets the streaming support filter.
    /// </summary>
    public bool? SupportsStreaming { get; set; }
}

/// <summary>
/// Represents provider information for a model.
/// </summary>
public class ModelProviderInfo
{
    /// <summary>
    /// Gets or sets the provider ID.
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider-specific model ID.
    /// </summary>
    public string ProviderModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the provider is available.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Gets or sets whether the provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the provider priority.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the estimated cost information.
    /// </summary>
    public EstimatedCost? EstimatedCost { get; set; }
}

/// <summary>
/// Represents estimated cost information for a provider.
/// </summary>
public class EstimatedCost
{
    /// <summary>
    /// Gets or sets the input token cost.
    /// </summary>
    public decimal InputTokenCost { get; set; }

    /// <summary>
    /// Gets or sets the output token cost.
    /// </summary>
    public decimal OutputTokenCost { get; set; }

    /// <summary>
    /// Gets or sets the currency.
    /// </summary>
    public string Currency { get; set; } = "USD";
}

/// <summary>
/// Represents routing information for a model.
/// </summary>
public class ModelRoutingInfo
{
    /// <summary>
    /// Gets or sets the model ID.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primary provider information.
    /// </summary>
    public ModelProviderInfo? PrimaryProvider { get; set; }

    /// <summary>
    /// Gets or sets the fallback providers.
    /// </summary>
    public IEnumerable<ModelProviderInfo> FallbackProviders { get; set; } = new List<ModelProviderInfo>();

    /// <summary>
    /// Gets or sets whether load balancing is enabled.
    /// </summary>
    public bool LoadBalancingEnabled { get; set; }

    /// <summary>
    /// Gets or sets the routing strategy.
    /// </summary>
    public RoutingStrategy RoutingStrategy { get; set; }
}

/// <summary>
/// Represents a bulk mapping request.
/// </summary>
public class BulkMappingRequest
{
    /// <summary>
    /// Gets or sets the mappings to create.
    /// </summary>
    [Required]
    public IEnumerable<CreateModelProviderMappingDto> Mappings { get; set; } = new List<CreateModelProviderMappingDto>();

    /// <summary>
    /// Gets or sets whether to replace existing mappings.
    /// </summary>
    public bool? ReplaceExisting { get; set; }
}

/// <summary>
/// Represents the response from a bulk mapping operation.
/// </summary>
public class BulkMappingResponse
{
    /// <summary>
    /// Gets or sets the successfully created mappings.
    /// </summary>
    public IEnumerable<ModelProviderMappingDto> Created { get; set; } = new List<ModelProviderMappingDto>();

    /// <summary>
    /// Gets or sets the successfully updated mappings.
    /// </summary>
    public IEnumerable<ModelProviderMappingDto> Updated { get; set; } = new List<ModelProviderMappingDto>();

    /// <summary>
    /// Gets or sets the failed mapping operations.
    /// </summary>
    public IEnumerable<BulkMappingFailure> Failed { get; set; } = new List<BulkMappingFailure>();
}

/// <summary>
/// Represents a failed bulk mapping operation.
/// </summary>
public class BulkMappingFailure
{
    /// <summary>
    /// Gets or sets the index of the failed mapping in the original request.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the mapping that failed.
    /// </summary>
    public CreateModelProviderMappingDto? Mapping { get; set; }
}

/// <summary>
/// Represents a model mapping suggestion.
/// </summary>
public class ModelMappingSuggestion
{
    /// <summary>
    /// Gets or sets the model ID.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the suggested providers.
    /// </summary>
    public IEnumerable<SuggestedProvider> SuggestedProviders { get; set; } = new List<SuggestedProvider>();
}

/// <summary>
/// Represents a suggested provider for a model.
/// </summary>
public class SuggestedProvider
{
    /// <summary>
    /// Gets or sets the provider ID.
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider-specific model ID.
    /// </summary>
    public string ProviderModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the confidence score (0-1).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets the reasoning for this suggestion.
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the estimated performance metrics.
    /// </summary>
    public EstimatedPerformance? EstimatedPerformance { get; set; }
}

/// <summary>
/// Represents estimated performance metrics for a provider.
/// </summary>
public class EstimatedPerformance
{
    /// <summary>
    /// Gets or sets the estimated latency in milliseconds.
    /// </summary>
    public double Latency { get; set; }

    /// <summary>
    /// Gets or sets the estimated reliability (0-1).
    /// </summary>
    public double Reliability { get; set; }

    /// <summary>
    /// Gets or sets the estimated cost efficiency (0-1).
    /// </summary>
    public double CostEfficiency { get; set; }
}

/// <summary>
/// Represents a discovered model from a provider.
/// </summary>
public class DiscoveredModel
{
    /// <summary>
    /// Gets or sets the model ID.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider-specific model ID.
    /// </summary>
    public string ProviderModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the discovered capabilities.
    /// </summary>
    public ModelCapabilities? Capabilities { get; set; }

    /// <summary>
    /// Gets or sets the confidence score for this discovery (0-1).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets whether this model is recommended for mapping.
    /// </summary>
    public bool IsRecommended { get; set; }
}

