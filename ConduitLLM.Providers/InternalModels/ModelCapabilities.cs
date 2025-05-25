using System;
using System.Collections.Generic;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Providers.InternalModels
{
    /// <summary>
    /// Represents the capabilities of a specific model.
    /// </summary>
    public class ModelCapabilities
    {
        /// <summary>
        /// Gets or sets a value indicating whether the model supports chat completions.
        /// </summary>
        public bool Chat { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the model supports text generation.
        /// </summary>
        public bool TextGeneration { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the model supports embeddings.
        /// </summary>
        public bool Embeddings { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the model supports image generation.
        /// </summary>
        public bool ImageGeneration { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the model supports vision/multimodal inputs.
        /// </summary>
        public bool Vision { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the model supports function calling.
        /// </summary>
        public bool FunctionCalling { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the model supports tool usage.
        /// </summary>
        public bool ToolUsage { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the model supports JSON mode.
        /// </summary>
        public bool JsonMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the model supports audio transcription.
        /// </summary>
        public bool AudioTranscription { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the model supports text-to-speech.
        /// </summary>
        public bool TextToSpeech { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the model supports real-time audio.
        /// </summary>
        public bool RealtimeAudio { get; set; }

        /// <summary>
        /// Gets or sets the list of supported audio operations.
        /// </summary>
        public List<AudioOperation>? SupportedAudioOperations { get; set; }
    }
}