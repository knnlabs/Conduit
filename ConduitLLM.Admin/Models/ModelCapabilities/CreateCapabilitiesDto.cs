namespace ConduitLLM.Admin.Models.ModelCapabilities
{
    /// <summary>
    /// Data transfer object for creating a new model capabilities configuration.
    /// </summary>
    /// <remarks>
    /// Use this DTO to define a new set of capabilities that can be shared across multiple models.
    /// Capabilities define what an AI model can do - its features, modalities, and constraints.
    /// 
    /// Creating shared capability configurations helps maintain consistency and reduces
    /// duplication when multiple models have identical capabilities. For example:
    /// - All GPT-4 variants might share one capability set
    /// - All image generation models from a provider might share another
    /// - All embedding models might share a minimal capability set
    /// 
    /// After creating a capabilities configuration, you can reference it when creating
    /// or updating models.
    /// </remarks>
    public class CreateCapabilitiesDto
    {
        /// <summary>
        /// Gets or sets whether models with these capabilities support chat/conversation.
        /// </summary>
        /// <remarks>
        /// Set to true for language models that can handle multi-turn conversations
        /// with message history. This is the primary capability for most LLMs.
        /// </remarks>
        /// <value>True if chat is supported; otherwise, false.</value>
        public bool SupportsChat { get; set; }

        /// <summary>
        /// Gets or sets whether models support vision/image understanding.
        /// </summary>
        /// <remarks>
        /// Set to true for multimodal models that can process images alongside text.
        /// Examples: GPT-4V, Claude 3 with vision, Gemini Pro Vision.
        /// </remarks>
        /// <value>True if vision is supported; otherwise, false.</value>
        public bool SupportsVision { get; set; }

        /// <summary>
        /// Gets or sets whether models support function/tool calling.
        /// </summary>
        /// <remarks>
        /// Set to true for models that can determine when to call external functions
        /// and structure the appropriate parameters. Essential for AI agents.
        /// </remarks>
        /// <value>True if function calling is supported; otherwise, false.</value>
        public bool SupportsFunctionCalling { get; set; }

        /// <summary>
        /// Gets or sets whether models support streaming responses.
        /// </summary>
        /// <remarks>
        /// Set to true for models that can return partial responses via Server-Sent Events.
        /// Most modern chat models support this for better user experience.
        /// </remarks>
        /// <value>True if streaming is supported; otherwise, false.</value>
        public bool SupportsStreaming { get; set; }

        /// <summary>
        /// Gets or sets whether models support audio transcription.
        /// </summary>
        /// <remarks>
        /// Set to true for speech-to-text models like Whisper that convert
        /// audio files into text transcripts.
        /// </remarks>
        /// <value>True if audio transcription is supported; otherwise, false.</value>
        public bool SupportsAudioTranscription { get; set; }

        /// <summary>
        /// Gets or sets whether models support text-to-speech synthesis.
        /// </summary>
        /// <remarks>
        /// Set to true for models that can generate natural-sounding speech from text.
        /// Examples: OpenAI TTS, ElevenLabs models.
        /// </remarks>
        /// <value>True if TTS is supported; otherwise, false.</value>
        public bool SupportsTextToSpeech { get; set; }

        /// <summary>
        /// Gets or sets whether models support real-time audio interactions.
        /// </summary>
        /// <remarks>
        /// Set to true for models supporting live, bidirectional audio conversations
        /// with low latency. More advanced than simple TTS/STT.
        /// </remarks>
        /// <value>True if real-time audio is supported; otherwise, false.</value>
        public bool SupportsRealtimeAudio { get; set; }

        /// <summary>
        /// Gets or sets whether models support image generation.
        /// </summary>
        /// <remarks>
        /// Set to true for text-to-image models like DALL-E, Stable Diffusion, Midjourney.
        /// These models create images from text descriptions.
        /// </remarks>
        /// <value>True if image generation is supported; otherwise, false.</value>
        public bool SupportsImageGeneration { get; set; }

        /// <summary>
        /// Gets or sets whether models support video generation.
        /// </summary>
        /// <remarks>
        /// Set to true for text-to-video or image-to-video models like Runway, Pika, Sora.
        /// These are typically resource-intensive with longer processing times.
        /// </remarks>
        /// <value>True if video generation is supported; otherwise, false.</value>
        public bool SupportsVideoGeneration { get; set; }

        /// <summary>
        /// Gets or sets whether models support text embeddings.
        /// </summary>
        /// <remarks>
        /// Set to true for models that generate vector representations of text
        /// for semantic search, similarity, and RAG applications.
        /// </remarks>
        /// <value>True if embeddings are supported; otherwise, false.</value>
        public bool SupportsEmbeddings { get; set; }

        /// <summary>
        /// Gets or sets the maximum token context window.
        /// </summary>
        /// <remarks>
        /// Specify the total number of tokens (input + output) the model can handle.
        /// Examples:
        /// - 4096 for GPT-3.5
        /// - 8192 or 32768 for GPT-4
        /// - 200000 for Claude 3
        /// - 1000000+ for some specialized models
        /// 
        /// This limit is critical for request validation and cost estimation.
        /// </remarks>
        /// <value>The maximum token limit.</value>
        public int MaxTokens { get; set; }

        /// <summary>
        /// Gets or sets the minimum token requirement.
        /// </summary>
        /// <remarks>
        /// Typically set to 1. Only change if the model requires a minimum input length.
        /// </remarks>
        /// <value>The minimum token requirement.</value>
        public int MinTokens { get; set; } = 1;

        /// <summary>
        /// Gets or sets the tokenizer type.
        /// </summary>
        /// <remarks>
        /// Specify the tokenization scheme for accurate token counting.
        /// Common values:
        /// - Cl100KBase for GPT-4/GPT-3.5-turbo
        /// - P50KBase for older GPT-3
        /// - Claude for Anthropic models
        /// - Llama for Meta models
        /// </remarks>
        /// <value>The tokenizer type enum value.</value>
        public TokenizerType TokenizerType { get; set; }

        /// <summary>
        /// Gets or sets the comma-separated list of supported voices for TTS.
        /// </summary>
        /// <remarks>
        /// For TTS-capable models, list available voice options.
        /// Format: "voice1,voice2,voice3"
        /// Example: "alloy,echo,fable,onyx,nova,shimmer"
        /// 
        /// Leave null if not applicable.
        /// </remarks>
        /// <value>Comma-separated voice IDs, or null.</value>
        public string? SupportedVoices { get; set; }

        /// <summary>
        /// Gets or sets the comma-separated list of supported languages.
        /// </summary>
        /// <remarks>
        /// List languages the model can process using ISO 639-1 codes or names.
        /// Format: "en,es,fr,de,zh,ja" or "English,Spanish,French"
        /// 
        /// Leave null if not specified or if the model supports all common languages.
        /// </remarks>
        /// <value>Comma-separated language codes, or null.</value>
        public string? SupportedLanguages { get; set; }

        /// <summary>
        /// Gets or sets the comma-separated list of supported formats.
        /// </summary>
        /// <remarks>
        /// Specify input/output formats the model can handle:
        /// - Audio: "mp3,wav,ogg,flac"
        /// - Image: "png,jpg,webp,gif"
        /// - Video: "mp4,avi,mov,webm"
        /// - Text: "text,json,markdown"
        /// 
        /// Leave null if using default formats for the capability type.
        /// </remarks>
        /// <value>Comma-separated format specifications, or null.</value>
        public string? SupportedFormats { get; set; }
    }
}