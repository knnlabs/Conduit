using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Admin.Models.ModelCapabilities
{
    /// <summary>
    /// Data transfer object representing the capabilities and characteristics of AI models.
    /// </summary>
    /// <remarks>
    /// This DTO defines what an AI model can do - its supported features, modalities, and constraints.
    /// Capabilities are shared across multiple models to avoid duplication. For example, all GPT-4
    /// variants might share the same capabilities configuration.
    /// 
    /// The capabilities system is designed to be flexible and extensible as new AI capabilities
    /// emerge. Each boolean flag indicates support for a specific feature, while additional
    /// properties provide configuration details like token limits and supported formats.
    /// 
    /// Capabilities are used for:
    /// - Routing decisions (finding models that support required features)
    /// - Validation (ensuring requests don't exceed model limits)
    /// - UI presentation (showing available features to users)
    /// - Cost calculation (different capabilities may have different pricing)
    /// </remarks>
    public class CapabilitiesDto
    {
        /// <summary>
        /// Gets or sets the unique identifier for this capabilities configuration.
        /// </summary>
        /// <value>The database-generated ID for this capabilities set.</value>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports chat/conversation interactions.
        /// </summary>
        /// <remarks>
        /// Models with chat support can handle multi-turn conversations with message history.
        /// This is the most common capability for LLMs like GPT, Claude, and Llama models.
        /// </remarks>
        /// <value>True if chat is supported; otherwise, false.</value>
        public bool SupportsChat { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports vision/image understanding.
        /// </summary>
        /// <remarks>
        /// Vision-capable models can process and understand images in addition to text.
        /// Examples include GPT-4V, Claude 3 with vision, and Gemini Pro Vision.
        /// These models can answer questions about images, describe visual content, etc.
        /// </remarks>
        /// <value>True if vision/image understanding is supported; otherwise, false.</value>
        public bool SupportsVision { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports function/tool calling.
        /// </summary>
        /// <remarks>
        /// Function calling allows models to invoke external tools and APIs as part of their response.
        /// The model can determine when to call functions, what parameters to pass, and how to use
        /// the results. This is essential for building AI agents and integrating with external systems.
        /// </remarks>
        /// <value>True if function calling is supported; otherwise, false.</value>
        public bool SupportsFunctionCalling { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports streaming responses.
        /// </summary>
        /// <remarks>
        /// Streaming allows the model to return partial responses as they're generated,
        /// providing a better user experience for long responses. Most modern chat models
        /// support streaming via Server-Sent Events (SSE).
        /// </remarks>
        /// <value>True if streaming is supported; otherwise, false.</value>
        public bool SupportsStreaming { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports audio transcription (speech-to-text).
        /// </summary>
        /// <remarks>
        /// Audio transcription models can convert spoken audio into text.
        /// Examples include Whisper models and other STT (speech-to-text) services.
        /// These models typically accept audio files in various formats (mp3, wav, etc.).
        /// </remarks>
        /// <value>True if audio transcription is supported; otherwise, false.</value>
        public bool SupportsAudioTranscription { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports text-to-speech synthesis.
        /// </summary>
        /// <remarks>
        /// TTS models can convert text into natural-sounding speech.
        /// Examples include OpenAI TTS, ElevenLabs, and other voice synthesis services.
        /// These models typically support multiple voices and languages.
        /// </remarks>
        /// <value>True if text-to-speech is supported; otherwise, false.</value>
        public bool SupportsTextToSpeech { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports real-time audio interactions.
        /// </summary>
        /// <remarks>
        /// Real-time audio support enables live, bidirectional audio conversations.
        /// This is more advanced than simple TTS/STT, supporting interruptions,
        /// natural conversation flow, and low-latency responses. Example: OpenAI Realtime API.
        /// </remarks>
        /// <value>True if real-time audio is supported; otherwise, false.</value>
        public bool SupportsRealtimeAudio { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports image generation.
        /// </summary>
        /// <remarks>
        /// Image generation models can create images from text descriptions.
        /// Examples include DALL-E, Stable Diffusion, Midjourney, and Flux.
        /// These models typically support various resolutions, styles, and quality settings.
        /// </remarks>
        /// <value>True if image generation is supported; otherwise, false.</value>
        public bool SupportsImageGeneration { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports video generation.
        /// </summary>
        /// <remarks>
        /// Video generation models can create video content from text descriptions or images.
        /// Examples include Runway, Pika, Stable Video Diffusion, and Sora.
        /// These are typically resource-intensive and may have longer processing times.
        /// </remarks>
        /// <value>True if video generation is supported; otherwise, false.</value>
        public bool SupportsVideoGeneration { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports text embeddings generation.
        /// </summary>
        /// <remarks>
        /// Embedding models convert text into dense vector representations for semantic search,
        /// similarity comparison, and retrieval-augmented generation (RAG) applications.
        /// Examples include text-embedding-ada-002, voyage-ai embeddings, and cohere-embed.
        /// </remarks>
        /// <value>True if embeddings generation is supported; otherwise, false.</value>
        public bool SupportsEmbeddings { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of tokens the model can process.
        /// </summary>
        /// <remarks>
        /// This represents the model's context window - the total number of tokens it can handle
        /// in a single request (input + output). Different models have different limits:
        /// - GPT-3.5: 4,096 or 16,385 tokens
        /// - GPT-4: 8,192 or 32,768 tokens  
        /// - Claude 3: 200,000 tokens
        /// - Some models: 1,000,000+ tokens
        /// 
        /// This limit is crucial for determining how much context can be provided and
        /// how long the responses can be.
        /// </remarks>
        /// <value>The maximum token limit for the model.</value>
        public int MaxTokens { get; set; }

        /// <summary>
        /// Gets or sets the minimum number of tokens for the model.
        /// </summary>
        /// <remarks>
        /// This represents the minimum tokens required for a valid request.
        /// Typically set to 1 as most models can handle single-token inputs.
        /// </remarks>
        /// <value>The minimum token requirement.</value>
        public int MinTokens { get; set; } = 1;

        /// <summary>
        /// Gets or sets the tokenizer type used by this model.
        /// </summary>
        /// <remarks>
        /// Different models use different tokenization schemes which affect how text is
        /// counted and processed. Common tokenizers include:
        /// - Cl100KBase (GPT-4, GPT-3.5-turbo)
        /// - P50KBase (older GPT-3 models)
        /// - Claude (Anthropic models)
        /// - Llama (Meta Llama models)
        /// 
        /// The tokenizer type is important for accurate token counting and cost calculation.
        /// </remarks>
        /// <value>The tokenizer type enum value.</value>
        public TokenizerType TokenizerType { get; set; }

        /// <summary>
        /// Gets or sets the comma-separated list of supported voice IDs for TTS models.
        /// </summary>
        /// <remarks>
        /// For text-to-speech capable models, this lists the available voice options.
        /// Format: "voice1,voice2,voice3" or JSON array as string.
        /// Examples: "alloy,echo,fable,onyx,nova,shimmer" for OpenAI TTS.
        /// </remarks>
        /// <value>Comma-separated voice IDs or JSON array string, or null if not applicable.</value>
        public string? SupportedVoices { get; set; }

        /// <summary>
        /// Gets or sets the comma-separated list of supported languages.
        /// </summary>
        /// <remarks>
        /// Lists the languages the model can process or generate.
        /// Format: ISO 639-1 codes like "en,es,fr,de,zh,ja" or full names.
        /// Some models support 100+ languages while others are English-only.
        /// Applies to chat, TTS, STT, and other language-processing capabilities.
        /// </remarks>
        /// <value>Comma-separated language codes or names, or null if not specified.</value>
        public string? SupportedLanguages { get; set; }

        /// <summary>
        /// Gets or sets the comma-separated list of supported input/output formats.
        /// </summary>
        /// <remarks>
        /// Specifies the file formats or data formats the model can handle.
        /// For audio models: "mp3,wav,ogg,flac"
        /// For image models: "png,jpg,webp,gif"
        /// For video models: "mp4,avi,mov,webm"
        /// For chat models: "text,json,markdown"
        /// </remarks>
        /// <value>Comma-separated format specifications, or null if not specified.</value>
        public string? SupportedFormats { get; set; }
    }
}