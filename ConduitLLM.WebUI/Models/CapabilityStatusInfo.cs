namespace ConduitLLM.WebUI.Models;

/// <summary>
/// Detailed information about the status of model capabilities in the system.
/// </summary>
public class CapabilityStatusInfo
{
    /// <summary>
    /// Total number of configured and enabled models.
    /// </summary>
    public int TotalConfiguredModels { get; set; }

    /// <summary>
    /// Number of models configured for image generation.
    /// </summary>
    public int ImageGenerationModels { get; set; }

    /// <summary>
    /// Number of models configured for vision/image input.
    /// </summary>
    public int VisionModels { get; set; }

    /// <summary>
    /// Number of models configured for audio transcription.
    /// </summary>
    public int AudioTranscriptionModels { get; set; }

    /// <summary>
    /// Number of models configured for text-to-speech.
    /// </summary>
    public int TextToSpeechModels { get; set; }

    /// <summary>
    /// Number of models configured for real-time audio.
    /// </summary>
    public int RealtimeAudioModels { get; set; }

    /// <summary>
    /// Number of models configured for video generation.
    /// </summary>
    public int VideoGenerationModels { get; set; }

    /// <summary>
    /// Detailed information about each configured model.
    /// </summary>
    public ICollection<ModelCapabilityInfo> ConfiguredModels { get; set; } = new List<ModelCapabilityInfo>();

    /// <summary>
    /// Whether an error occurred while gathering status information.
    /// </summary>
    public bool HasError { get; set; }

    /// <summary>
    /// Error message if an error occurred.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets a summary of the capability status.
    /// </summary>
    public string Summary => HasError 
        ? $"Error: {ErrorMessage}" 
        : $"Total Models: {TotalConfiguredModels}, Image Gen: {ImageGenerationModels}, Video Gen: {VideoGenerationModels}, Vision: {VisionModels}, Audio: {AudioTranscriptionModels + TextToSpeechModels + RealtimeAudioModels}";
}

/// <summary>
/// Information about a specific model's capabilities.
/// </summary>
public class ModelCapabilityInfo
{
    /// <summary>
    /// The model identifier.
    /// </summary>
    public string ModelId { get; set; } = "";

    /// <summary>
    /// The provider identifier.
    /// </summary>
    public string ProviderId { get; set; } = "";

    /// <summary>
    /// Whether the model supports image generation.
    /// </summary>
    public bool SupportsImageGeneration { get; set; }

    /// <summary>
    /// Whether the model supports vision/image input.
    /// </summary>
    public bool SupportsVision { get; set; }

    /// <summary>
    /// Whether the model supports audio transcription.
    /// </summary>
    public bool SupportsAudioTranscription { get; set; }

    /// <summary>
    /// Whether the model supports text-to-speech.
    /// </summary>
    public bool SupportsTextToSpeech { get; set; }

    /// <summary>
    /// Whether the model supports real-time audio.
    /// </summary>
    public bool SupportsRealtimeAudio { get; set; }

    /// <summary>
    /// Whether the model supports video generation.
    /// </summary>
    public bool SupportsVideoGeneration { get; set; }

    /// <summary>
    /// Gets a list of supported capabilities as strings.
    /// </summary>
    public IEnumerable<string> SupportedCapabilities
    {
        get
        {
            var capabilities = new List<string>();
            if (SupportsImageGeneration) capabilities.Add("Image Generation");
            if (SupportsVideoGeneration) capabilities.Add("Video Generation");
            if (SupportsVision) capabilities.Add("Vision");
            if (SupportsAudioTranscription) capabilities.Add("Audio Transcription");
            if (SupportsTextToSpeech) capabilities.Add("Text-to-Speech");
            if (SupportsRealtimeAudio) capabilities.Add("Realtime Audio");
            return capabilities;
        }
    }
}