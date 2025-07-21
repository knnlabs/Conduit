using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Request DTO for bulk model mapping creation
    /// </summary>
    public class BulkModelMappingRequest
    {
        /// <summary>
        /// Collection of model mappings to create
        /// </summary>
        [Required]
        public List<CreateModelProviderMappingDto> Mappings { get; set; } = new();

        /// <summary>
        /// Whether to replace existing mappings with the same model ID
        /// </summary>
        public bool ReplaceExisting { get; set; } = false;

        /// <summary>
        /// Whether to validate provider model existence before creation
        /// </summary>
        public bool ValidateProviderModels { get; set; } = true;
    }

    /// <summary>
    /// DTO for creating a single model provider mapping
    /// </summary>
    public class CreateModelProviderMappingDto
    {
        /// <summary>
        /// The model identifier used in client requests
        /// </summary>
        [Required(ErrorMessage = "Model ID is required")]
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// The provider-specific model identifier
        /// </summary>
        [Required(ErrorMessage = "Provider Model ID is required")]
        public string ProviderModelId { get; set; } = string.Empty;

        /// <summary>
        /// The provider identifier
        /// </summary>
        [Required(ErrorMessage = "Provider ID is required")]
        public string ProviderId { get; set; } = string.Empty;

        /// <summary>
        /// The priority of this mapping (lower values have higher priority)
        /// </summary>
        public int Priority { get; set; } = 1;

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
        /// Optional notes or description for this mapping
        /// </summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Response DTO for bulk model mapping creation
    /// </summary>
    public class BulkModelMappingResponse
    {
        /// <summary>
        /// Successfully created mappings
        /// </summary>
        public List<ModelProviderMappingDto> Created { get; set; } = new();

        /// <summary>
        /// Updated mappings (when ReplaceExisting is true)
        /// </summary>
        public List<ModelProviderMappingDto> Updated { get; set; } = new();

        /// <summary>
        /// Failed mapping attempts
        /// </summary>
        public List<BulkMappingError> Failed { get; set; } = new();

        /// <summary>
        /// Total number of mappings processed
        /// </summary>
        public int TotalProcessed { get; set; }

        /// <summary>
        /// Number of successful operations
        /// </summary>
        public int SuccessCount => Created.Count + Updated.Count;

        /// <summary>
        /// Number of failed operations
        /// </summary>
        public int FailureCount => Failed.Count;

        /// <summary>
        /// Whether the bulk operation was completely successful
        /// </summary>
        public bool IsSuccess => Failed.Count == 0;
    }

    /// <summary>
    /// Details about a failed mapping creation
    /// </summary>
    public class BulkMappingError
    {
        /// <summary>
        /// Index of the failed mapping in the original request
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// The mapping that failed to be created
        /// </summary>
        public CreateModelProviderMappingDto Mapping { get; set; } = new();

        /// <summary>
        /// Error message describing the failure
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Detailed error information
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// Error category for UI handling
        /// </summary>
        public BulkMappingErrorType ErrorType { get; set; }
    }

    /// <summary>
    /// Categories of bulk mapping errors
    /// </summary>
    public enum BulkMappingErrorType
    {
        /// <summary>
        /// Validation error in the input data
        /// </summary>
        Validation,

        /// <summary>
        /// Duplicate model ID conflict
        /// </summary>
        Duplicate,

        /// <summary>
        /// Provider model does not exist
        /// </summary>
        ProviderModelNotFound,

        /// <summary>
        /// Database or system error
        /// </summary>
        SystemError,

        /// <summary>
        /// Provider not found or unavailable
        /// </summary>
        ProviderNotFound
    }
}