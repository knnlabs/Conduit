namespace ConduitLLM.Configuration;

/// <summary>
/// Defines default model configurations for different providers and operation types.
/// This replaces hardcoded model defaults throughout the codebase.
/// </summary>
public class ProviderDefaultModels
{
    /// <summary>
    /// Gets or sets the default models for audio operations.
    /// </summary>
    public AudioDefaultModels Audio { get; set; } = new();

    /// <summary>
    /// Gets or sets the default models for realtime operations.
    /// </summary>
    public RealtimeDefaultModels Realtime { get; set; } = new();

    /// <summary>
    /// Gets or sets provider-specific default models.
    /// </summary>
    public Dictionary<string, ProviderSpecificDefaults> ProviderDefaults { get; set; } = new();
}

/// <summary>
/// Default models for audio operations across providers.
/// </summary>
public class AudioDefaultModels
{
    /// <summary>
    /// Gets or sets the default model for speech-to-text transcription.
    /// </summary>
    public string? DefaultTranscriptionModel { get; set; }

    /// <summary>
    /// Gets or sets the default model for text-to-speech generation.
    /// </summary>
    public string? DefaultTextToSpeechModel { get; set; }

    /// <summary>
    /// Gets or sets provider-specific audio model defaults.
    /// </summary>
    public Dictionary<string, AudioProviderDefaults> ProviderOverrides { get; set; } = new();
}

/// <summary>
/// Provider-specific audio model defaults.
/// </summary>
public class AudioProviderDefaults
{
    /// <summary>
    /// Gets or sets the transcription model for this provider.
    /// </summary>
    public string? TranscriptionModel { get; set; }

    /// <summary>
    /// Gets or sets the text-to-speech model for this provider.
    /// </summary>
    public string? TextToSpeechModel { get; set; }
}

/// <summary>
/// Default models for realtime operations.
/// </summary>
public class RealtimeDefaultModels
{
    /// <summary>
    /// Gets or sets the default model for realtime conversations.
    /// </summary>
    public string? DefaultRealtimeModel { get; set; }

    /// <summary>
    /// Gets or sets provider-specific realtime model defaults.
    /// </summary>
    public Dictionary<string, string> ProviderOverrides { get; set; } = new();
}

/// <summary>
/// Provider-specific default model configurations.
/// </summary>
public class ProviderSpecificDefaults
{
    /// <summary>
    /// Gets or sets the default model for chat completions.
    /// </summary>
    public string? DefaultChatModel { get; set; }

    /// <summary>
    /// Gets or sets the default model for embeddings.
    /// </summary>
    public string? DefaultEmbeddingModel { get; set; }

    /// <summary>
    /// Gets or sets the default model for image generation.
    /// </summary>
    public string? DefaultImageModel { get; set; }

    /// <summary>
    /// Gets or sets model aliases for this provider.
    /// Maps user-friendly names to provider-specific model IDs.
    /// </summary>
    public Dictionary<string, string> ModelAliases { get; set; } = new();
}
