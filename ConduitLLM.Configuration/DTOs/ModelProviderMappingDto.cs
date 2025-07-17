using System;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for model-provider mappings
    /// </summary>
    public class ModelProviderMappingDto
    {
        /// <summary>
        /// Unique identifier for the mapping
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The model identifier used in client requests
        /// </summary>
        [Required(ErrorMessage = "Model Alias is required")]
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// The provider-specific model identifier
        /// </summary>
        [Required(ErrorMessage = "Provider Model ID is required")]
        public string ProviderModelId { get; set; } = string.Empty;

        /// <summary>
        /// The provider identifier
        /// </summary>
        [Required(ErrorMessage = "Provider is required")]
        public string ProviderId { get; set; } = string.Empty;

        /// <summary>
        /// The human-readable provider name (e.g., "OpenAI", "Anthropic")
        /// </summary>
        public string? ProviderName { get; set; }

        /// <summary>
        /// The priority of this mapping (lower values have higher priority)
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Whether this mapping is currently enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Optional model capabilities (e.g., vision, function-calling)
        /// </summary>
        public string? Capabilities { get; set; }

        /// <summary>
        /// Optional maximum context length
        /// </summary>
        public int? MaxContextLength { get; set; }

        /// <summary>
        /// Whether this model supports vision/image input capabilities
        /// </summary>
        public bool SupportsVision { get; set; } = false;

        /// <summary>
        /// Whether this model supports audio transcription capabilities
        /// </summary>
        public bool SupportsAudioTranscription { get; set; } = false;

        /// <summary>
        /// Whether this model supports text-to-speech capabilities
        /// </summary>
        public bool SupportsTextToSpeech { get; set; } = false;

        /// <summary>
        /// Whether this model supports real-time audio streaming capabilities
        /// </summary>
        public bool SupportsRealtimeAudio { get; set; } = false;

        /// <summary>
        /// Whether this model supports image generation capabilities
        /// </summary>
        public bool SupportsImageGeneration { get; set; } = false;

        /// <summary>
        /// Whether this model supports video generation capabilities
        /// </summary>
        public bool SupportsVideoGeneration { get; set; } = false;

        /// <summary>
        /// Whether this model supports embedding generation capabilities
        /// </summary>
        public bool SupportsEmbeddings { get; set; } = false;

        /// <summary>
        /// Whether this model supports function calling
        /// </summary>
        public bool SupportsFunctionCalling { get; set; } = false;

        /// <summary>
        /// Whether this model supports streaming responses
        /// </summary>
        public bool SupportsStreaming { get; set; } = false;

        /// <summary>
        /// The tokenizer type used by this model
        /// </summary>
        public string? TokenizerType { get; set; }

        /// <summary>
        /// JSON array of supported voices for TTS models
        /// </summary>
        public string? SupportedVoices { get; set; }

        /// <summary>
        /// JSON array of supported languages for this model
        /// </summary>
        public string? SupportedLanguages { get; set; }

        /// <summary>
        /// JSON array of supported audio formats for this model
        /// </summary>
        public string? SupportedFormats { get; set; }

        /// <summary>
        /// Whether this model is the default for its capability type
        /// </summary>
        public bool IsDefault { get; set; } = false;

        /// <summary>
        /// The capability type this model is default for (if IsDefault is true)
        /// </summary>
        public string? DefaultCapabilityType { get; set; }

        /// <summary>
        /// Date when the mapping was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Date when the mapping was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional notes or description for this mapping
        /// </summary>
        public string? Notes { get; set; }
    }
}
