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
        /// Gets or sets the maximum number of input tokens the model can process.
        /// </summary>
        /// <remarks>
        /// Specify the maximum context/prompt size the model can handle.
        /// Examples:
        /// - 3000-15000 for GPT-3.5
        /// - 7000-127000 for GPT-4
        /// - 200000 for Claude 3
        /// - 1000000+ for some specialized models
        /// 
        /// This limit is critical for request validation and cost estimation.
        /// </remarks>
        /// <value>The maximum input token limit.</value>
        public int? MaxInputTokens { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of output tokens the model can generate.
        /// </summary>
        /// <remarks>
        /// Specify the maximum response size the model can generate.
        /// Examples:
        /// - 1000-4096 for GPT-3.5
        /// - 1000-4096 for GPT-4
        /// - 4096 for Claude 3
        /// 
        /// Output limits are typically smaller than input limits.
        /// </remarks>
        /// <value>The maximum output token limit.</value>
        public int? MaxOutputTokens { get; set; }

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

    }
}