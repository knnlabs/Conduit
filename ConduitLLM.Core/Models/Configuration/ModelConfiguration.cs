using System;
using System.Collections.Generic;

namespace ConduitLLM.Core.Models.Configuration
{
    /// <summary>
    /// Configuration for model capabilities that can be loaded from configuration files or database.
    /// </summary>
    public class ModelConfiguration
    {
        /// <summary>
        /// Gets or sets the unique model identifier.
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the provider name (e.g., "openai", "anthropic").
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name for the model.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the model capabilities.
        /// </summary>
        public ModelCapabilities Capabilities { get; set; } = new();

        /// <summary>
        /// Gets or sets whether the model is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets when this configuration was last updated.
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Defines the capabilities of a model.
    /// </summary>
    public class ModelCapabilities
    {
        /// <summary>
        /// Gets or sets whether the model supports chat completion.
        /// </summary>
        public bool SupportsChat { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports vision/image inputs.
        /// </summary>
        public bool SupportsVision { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports audio transcription.
        /// </summary>
        public bool SupportsTranscription { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports text-to-speech.
        /// </summary>
        public bool SupportsTextToSpeech { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports real-time audio streaming.
        /// </summary>
        public bool SupportsRealtimeAudio { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports function calling.
        /// </summary>
        public bool SupportsFunctionCalling { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports embeddings generation.
        /// </summary>
        public bool SupportsEmbeddings { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports image generation.
        /// </summary>
        public bool SupportsImageGeneration { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports video generation.
        /// </summary>
        public bool SupportsVideoGeneration { get; set; }

        /// <summary>
        /// Gets or sets the tokenizer type for the model.
        /// </summary>
        public string? TokenizerType { get; set; }

        /// <summary>
        /// Gets or sets the maximum token limit for the model.
        /// </summary>
        public int? MaxTokens { get; set; }

        /// <summary>
        /// Gets or sets the supported voices for TTS models.
        /// </summary>
        public List<string> SupportedVoices { get; set; } = new();

        /// <summary>
        /// Gets or sets the supported languages.
        /// </summary>
        public List<string> SupportedLanguages { get; set; } = new();

        /// <summary>
        /// Gets or sets the supported audio formats.
        /// </summary>
        public List<string> SupportedFormats { get; set; } = new();

        /// <summary>
        /// Gets or sets additional capability metadata.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Configuration for provider defaults.
    /// </summary>
    public class ProviderDefaults
    {
        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the default models for different capabilities.
        /// </summary>
        public Dictionary<string, string> DefaultModels { get; set; } = new();
    }

    /// <summary>
    /// Root configuration containing all model configurations.
    /// </summary>
    public class ModelConfigurationRoot
    {
        /// <summary>
        /// Gets or sets the list of model configurations.
        /// </summary>
        public List<ModelConfiguration> Models { get; set; } = new();

        /// <summary>
        /// Gets or sets the provider defaults.
        /// </summary>
        public List<ProviderDefaults> ProviderDefaults { get; set; } = new();

        /// <summary>
        /// Gets or sets when this configuration was last updated.
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}