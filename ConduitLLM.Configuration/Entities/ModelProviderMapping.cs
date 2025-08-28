using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Maps a generic model alias (e.g., "gpt-4-turbo") to a specific provider's model name 
    /// and associates it with provider credentials. This entity enables routing requests to 
    /// specific provider models regardless of the model name used in the request.
    /// </summary>
    public class ModelProviderMapping
    {
        /// <summary>
        /// Unique identifier for the model-provider mapping.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// User-friendly model alias used in client requests.
        /// This is the name that clients will use in their API calls (e.g., "gpt-4").
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ModelAlias { get; set; } = string.Empty;

        /// <summary>
        /// The actual model identifier expected by the provider.
        /// This is the provider-specific model name (e.g., "gpt-4-turbo-preview", "claude-3-opus-20240229").
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ProviderModelId { get; set; } = string.Empty;

        /// <summary>
        /// Foreign key to the provider entity.
        /// Links this mapping to the provider used to authenticate with the provider.
        /// </summary>
        public int ProviderId { get; set; }

        /// <summary>
        /// Navigation property to the associated provider.
        /// Contains authentication details for connecting to the provider.
        /// </summary>
        [ForeignKey("ProviderId")]
        public virtual Provider Provider { get; set; } = null!;

        /// <summary>
        /// Indicates whether this mapping is currently active.
        /// When false, the router will not use this mapping for routing requests.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Provider-specific override for maximum context tokens.
        /// If null, uses Model.Capabilities.MaxTokens.
        /// Some providers may have different limits than the base model.
        /// </summary>
        public int? MaxContextTokensOverride { get; set; }

        /// <summary>
        /// Gets the effective maximum context tokens for this provider mapping.
        /// </summary>
        [NotMapped]
        public int MaxContextTokens => MaxContextTokensOverride ?? Model?.Capabilities?.MaxTokens ?? 4096;

        /// <summary>
        /// The UTC timestamp when this mapping was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The UTC timestamp when this mapping was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// JSON object containing provider-specific capability overrides.
        /// Use this when a provider has different capabilities than the base model.
        /// Example: {"supportsFunctionCalling": false} if provider disabled this feature.
        /// </summary>
        public string? CapabilityOverrides { get; set; }

        // Helper properties that read from Model.Capabilities with optional overrides

        /// <summary>
        /// Gets whether this model supports vision, checking overrides first.
        /// </summary>
        [NotMapped]
        public bool SupportsVision => GetCapability(nameof(SupportsVision), 
            () => Model?.Capabilities?.SupportsVision ?? false);

        /// <summary>
        /// Gets whether this model supports chat, checking overrides first.
        /// </summary>
        [NotMapped]
        public bool SupportsChat => GetCapability(nameof(SupportsChat), 
            () => Model?.Capabilities?.SupportsChat ?? false);

        /// <summary>
        /// Gets whether this model supports function calling, checking overrides first.
        /// </summary>
        [NotMapped]
        public bool SupportsFunctionCalling => GetCapability(nameof(SupportsFunctionCalling), 
            () => Model?.Capabilities?.SupportsFunctionCalling ?? false);

        /// <summary>
        /// Gets whether this model supports streaming, checking overrides first.
        /// </summary>
        [NotMapped]
        public bool SupportsStreaming => GetCapability(nameof(SupportsStreaming), 
            () => Model?.Capabilities?.SupportsStreaming ?? false);

        /// <summary>
        /// Gets whether this model supports audio transcription, checking overrides first.
        /// </summary>
        [NotMapped]
        public bool SupportsAudioTranscription => GetCapability(nameof(SupportsAudioTranscription),
            () => Model?.Capabilities?.SupportsAudioTranscription ?? false);

        /// <summary>
        /// Gets whether this model supports text-to-speech, checking overrides first.
        /// </summary>
        [NotMapped]
        public bool SupportsTextToSpeech => GetCapability(nameof(SupportsTextToSpeech),
            () => Model?.Capabilities?.SupportsTextToSpeech ?? false);

        /// <summary>
        /// Gets whether this model supports realtime audio, checking overrides first.
        /// </summary>
        [NotMapped]
        public bool SupportsRealtimeAudio => GetCapability(nameof(SupportsRealtimeAudio),
            () => Model?.Capabilities?.SupportsRealtimeAudio ?? false);

        /// <summary>
        /// Gets whether this model supports image generation, checking overrides first.
        /// </summary>
        [NotMapped]
        public bool SupportsImageGeneration => GetCapability(nameof(SupportsImageGeneration),
            () => Model?.Capabilities?.SupportsImageGeneration ?? false);

        /// <summary>
        /// Gets whether this model supports video generation, checking overrides first.
        /// </summary>
        [NotMapped]
        public bool SupportsVideoGeneration => GetCapability(nameof(SupportsVideoGeneration),
            () => Model?.Capabilities?.SupportsVideoGeneration ?? false);

        /// <summary>
        /// Gets whether this model supports embeddings, checking overrides first.
        /// </summary>
        [NotMapped]
        public bool SupportsEmbeddings => GetCapability(nameof(SupportsEmbeddings),
            () => Model?.Capabilities?.SupportsEmbeddings ?? false);

        /// <summary>
        /// Gets the tokenizer type from Model.Capabilities.
        /// </summary>
        [NotMapped]
        public TokenizerType? TokenizerType => Model?.Capabilities?.TokenizerType;

        /// <summary>
        /// Gets supported voices from Model.Capabilities.
        /// </summary>
        [NotMapped]
        public string? SupportedVoices => Model?.Capabilities?.SupportedVoices;

        /// <summary>
        /// Gets supported languages from Model.Capabilities.
        /// </summary>
        [NotMapped]
        public string? SupportedLanguages => Model?.Capabilities?.SupportedLanguages;

        /// <summary>
        /// Gets supported formats from Model.Capabilities.
        /// </summary>
        [NotMapped]
        public string? SupportedFormats => Model?.Capabilities?.SupportedFormats;

        /// <summary>
        /// Indicates whether this is the default model for its provider and capability type.
        /// </summary>
        public bool IsDefault { get; set; } = false;

        /// <summary>
        /// The capability type this model is default for (e.g., "chat", "transcription", "tts", "realtime").
        /// Only relevant when IsDefault is true.
        /// </summary>
        [MaxLength(50)]
        public string? DefaultCapabilityType { get; set; }

        /// <summary>
        /// Required foreign key to the associated Model entity.
        /// Every provider mapping must reference a canonical model.
        /// </summary>
        [Required]
        public int ModelId { get; set; }

        /// <summary>
        /// Navigation property to the associated Model.
        /// Contains metadata, capabilities, and configuration for the model.
        /// </summary>
        [ForeignKey("ModelId")]
        public virtual Model Model { get; set; } = null!;


        /// <summary>
        /// Represents the variation of the model provided by the provider.
        /// Examples: "Q4_K_M", "GGUF", "4bit-128g", "fine-tuned-medical", "instruct"
        /// </summary>
        public string? ProviderVariation { get; set; } // // "4-bit-quantized", "fine-tuned-v2"

        /// <summary>
        /// Represents the quality score of the model provided by the provider.
        /// 1.0 = identical to original
        /// 0.95 = 5% quality loss (typical for good quantization)
        /// 0.8 = 20% quality loss (aggressive quantization)
        /// </summary>

        public decimal? QualityScore { get; set; } // Provider's quality vs original

        /// <summary>
        /// Gets or sets the collection of cost configurations applied to this model mapping.
        /// </summary>
        /// <remarks>
        /// This navigation property represents the many-to-many relationship between ModelProviderMapping and ModelCost.
        /// A model can have multiple cost configurations (e.g., different costs for different time periods or regions).
        /// </remarks>
        public virtual ICollection<ModelCostMapping> ModelCostMappings { get; set; } = new List<ModelCostMapping>();

        /// <summary>
        /// Helper method to get capability value, checking overrides first.
        /// </summary>
        /// <param name="capabilityName">The name of the capability to check.</param>
        /// <param name="defaultValue">Function to get the default value from Model.Capabilities.</param>
        /// <returns>The capability value, considering overrides.</returns>
        private bool GetCapability(string capabilityName, Func<bool> defaultValue)
        {
            if (string.IsNullOrEmpty(CapabilityOverrides))
                return defaultValue();

            try
            {
                var overrides = JsonDocument.Parse(CapabilityOverrides);
                var propertyName = char.ToLower(capabilityName[0]) + capabilityName.Substring(1);
                
                if (overrides.RootElement.TryGetProperty(propertyName, out var element) &&
                    (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
                {
                    return element.GetBoolean();
                }
            }
            catch
            {
                // Invalid JSON or parsing error, fall back to default
            }

            return defaultValue();
        }
    }
}
