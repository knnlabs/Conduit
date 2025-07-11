using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.AdminClient.Models;

/// <summary>
/// Represents a model available from a specific provider.
/// </summary>
public class ProviderModelDto
{
    /// <summary>
    /// Gets or sets the model ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the model capabilities.
    /// </summary>
    public IEnumerable<string> Capabilities { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the maximum context length.
    /// </summary>
    public int? MaxContextLength { get; set; }

    /// <summary>
    /// Gets or sets the model version.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets whether the model is available.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Gets or sets when the model was last verified.
    /// </summary>
    public DateTime? LastVerified { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the model.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets when the model information was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the model information was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Represents filter criteria for querying provider models.
/// </summary>
public class ProviderModelsFilters : FilterOptions
{
    /// <summary>
    /// Gets or sets the provider name filter.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Gets or sets the model name filter.
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Gets or sets the capability filter.
    /// </summary>
    public string? Capability { get; set; }

    /// <summary>
    /// Gets or sets the availability filter.
    /// </summary>
    public bool? IsAvailable { get; set; }

    /// <summary>
    /// Gets or sets the minimum context length filter.
    /// </summary>
    public int? MinContextLength { get; set; }

    /// <summary>
    /// Gets or sets the maximum context length filter.
    /// </summary>
    public int? MaxContextLength { get; set; }
}

/// <summary>
/// Represents a discovered model with capability information.
/// </summary>
public class DiscoveredModelDto
{
    /// <summary>
    /// Gets or sets the model ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the object type (typically "model").
    /// </summary>
    public string Object { get; set; } = "model";

    /// <summary>
    /// Gets or sets when the model was created.
    /// </summary>
    public long Created { get; set; }

    /// <summary>
    /// Gets or sets the model owner/provider.
    /// </summary>
    public string OwnedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the detected capabilities.
    /// </summary>
    public ModelCapabilities Capabilities { get; set; } = new();

    /// <summary>
    /// Gets or sets the maximum context length.
    /// </summary>
    public int? MaxContextLength { get; set; }

    /// <summary>
    /// Gets or sets additional model metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Represents the capabilities of a model.
/// </summary>
public class ModelCapabilities
{
    /// <summary>
    /// Gets or sets whether the model supports chat completions.
    /// </summary>
    public bool SupportsChat { get; set; }

    /// <summary>
    /// Gets or sets whether the model supports vision/image understanding.
    /// </summary>
    public bool SupportsVision { get; set; }

    /// <summary>
    /// Gets or sets whether the model supports function calling.
    /// </summary>
    public bool SupportsFunctionCalling { get; set; }

    /// <summary>
    /// Gets or sets whether the model supports embeddings.
    /// </summary>
    public bool SupportsEmbeddings { get; set; }

    /// <summary>
    /// Gets or sets whether the model supports image generation.
    /// </summary>
    public bool SupportsImageGeneration { get; set; }

    /// <summary>
    /// Gets or sets whether the model supports audio transcription.
    /// </summary>
    public bool SupportsAudioTranscription { get; set; }

    /// <summary>
    /// Gets or sets whether the model supports text-to-speech.
    /// </summary>
    public bool SupportsTextToSpeech { get; set; }

    /// <summary>
    /// Gets or sets whether the model supports real-time audio.
    /// </summary>
    public bool SupportsRealtimeAudio { get; set; }

    /// <summary>
    /// Gets or sets additional capability flags.
    /// </summary>
    public Dictionary<string, bool>? AdditionalCapabilities { get; set; }
}

/// <summary>
/// Represents a response containing discovered models.
/// </summary>
public class DiscoveredModelsResponse
{
    /// <summary>
    /// Gets or sets the object type.
    /// </summary>
    public string Object { get; set; } = "list";

    /// <summary>
    /// Gets or sets the list of discovered models.
    /// </summary>
    public IEnumerable<DiscoveredModelDto> Data { get; set; } = new List<DiscoveredModelDto>();

    /// <summary>
    /// Gets or sets the total count of models.
    /// </summary>
    public int? TotalCount { get; set; }

    /// <summary>
    /// Gets or sets when the discovery was performed.
    /// </summary>
    public DateTime Timestamp { get; set; }
}


/// <summary>
/// Represents the result of bulk capability testing.
/// </summary>
public class BulkCapabilityTestResult
{
    /// <summary>
    /// Gets or sets the results for each model.
    /// </summary>
    public Dictionary<string, ModelCapabilityTestResult> Results { get; set; } = new();

    /// <summary>
    /// Gets or sets when the testing was performed.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the total duration of all tests.
    /// </summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Represents the capability test result for a specific model.
/// </summary>
public class ModelCapabilityTestResult
{
    /// <summary>
    /// Gets or sets the model ID.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the model is available.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Gets or sets the detected capabilities.
    /// </summary>
    public ModelCapabilities Capabilities { get; set; } = new();

    /// <summary>
    /// Gets or sets any error that occurred during testing.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the duration of the capability test.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets when the test was performed.
    /// </summary>
    public DateTime TestedAt { get; set; }
}

/// <summary>
/// Represents provider model statistics.
/// </summary>
public class ProviderModelStatsDto
{
    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of models.
    /// </summary>
    public int TotalModels { get; set; }

    /// <summary>
    /// Gets or sets the number of available models.
    /// </summary>
    public int AvailableModels { get; set; }

    /// <summary>
    /// Gets or sets the number of models with chat capability.
    /// </summary>
    public int ChatModels { get; set; }

    /// <summary>
    /// Gets or sets the number of models with vision capability.
    /// </summary>
    public int VisionModels { get; set; }

    /// <summary>
    /// Gets or sets the number of models with embedding capability.
    /// </summary>
    public int EmbeddingModels { get; set; }

    /// <summary>
    /// Gets or sets the number of models with image generation capability.
    /// </summary>
    public int ImageGenerationModels { get; set; }

    /// <summary>
    /// Gets or sets when the statistics were last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; }
}