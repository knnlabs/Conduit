namespace ConduitLLM.Configuration;

/// <summary>
/// Defines a mapping between a user-friendly model alias and the specific provider and model ID to use.
/// </summary>
public class ModelProviderMapping
{
    /// <summary>
    /// Gets or sets the user-defined alias for the model (e.g., "gpt-4-turbo", "my-finetuned-model").
    /// This is the identifier the user will pass in the ChatCompletionRequest.Model property.
    /// </summary>
    public required string ModelAlias { get; set; }

    /// <summary>
    /// Gets or sets the name of the provider configured in ProviderCredentials (e.g., "openai", "anthropic").
    /// This links the mapping to the correct set of credentials and connection details.
    /// </summary>
    public required string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the actual model ID that the target provider expects
    /// (e.g., "gpt-4-turbo-preview", "claude-3-opus-20240229").
    /// </summary>
    public required string ProviderModelId { get; set; }

    /// <summary>
    /// Gets or sets an optional deployment name for providers that support deployments 
    /// (e.g., Azure OpenAI deployments).
    /// </summary>
    public string? DeploymentName { get; set; }

    // Model Capability Properties

    /// <summary>
    /// Indicates whether this model supports vision/image inputs.
    /// </summary>
    public bool SupportsVision { get; set; }

    /// <summary>
    /// Indicates whether this model supports audio transcription (Speech-to-Text).
    /// </summary>
    public bool SupportsAudioTranscription { get; set; }

    /// <summary>
    /// Indicates whether this model supports text-to-speech generation.
    /// </summary>
    public bool SupportsTextToSpeech { get; set; }

    /// <summary>
    /// Indicates whether this model supports real-time audio streaming.
    /// </summary>
    public bool SupportsRealtimeAudio { get; set; }

    /// <summary>
    /// Indicates whether this model supports image generation.
    /// </summary>
    public bool SupportsImageGeneration { get; set; }

    /// <summary>
    /// Indicates whether this model supports embedding operations.
    /// </summary>
    public bool SupportsEmbeddings { get; set; }

    /// <summary>
    /// The tokenizer type used by this model (e.g., "cl100k_base", "p50k_base", "claude").
    /// </summary>
    public string? TokenizerType { get; set; }

    /// <summary>
    /// JSON array of supported voices for TTS models (e.g., ["alloy", "echo", "nova"]).
    /// </summary>
    public string? SupportedVoices { get; set; }

    /// <summary>
    /// JSON array of supported languages (e.g., ["en", "es", "fr", "de"]).
    /// </summary>
    public string? SupportedLanguages { get; set; }

    /// <summary>
    /// JSON array of supported audio formats (e.g., ["mp3", "opus", "aac", "flac"]).
    /// </summary>
    public string? SupportedFormats { get; set; }

    /// <summary>
    /// Indicates whether this is the default model for its provider and capability type.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// The capability type this model is default for (e.g., "chat", "transcription", "tts", "realtime").
    /// Only relevant when IsDefault is true.
    /// </summary>
    public string? DefaultCapabilityType { get; set; }
}
