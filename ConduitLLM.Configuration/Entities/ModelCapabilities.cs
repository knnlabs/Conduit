using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.Entities
{
    public class ModelCapabilities
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The maximum tokens that can be generated for this capability.
        /// </summary>
        public int MaxTokens { get; set; } = 4096;

        /// <summary>
        /// The minimum tokens that can be generated for this capability.
        /// </summary>
        public int MinTokens { get; set; } = 1;

    /// <summary>
    /// Indicates whether this model supports vision/image inputs.
    /// </summary>
    public bool SupportsVision { get; set; } = false;

    /// <summary>
    /// Indicates whether this model supports image generation.
    /// </summary>
    public bool SupportsImageGeneration { get; set; } = false;

    /// <summary>
    /// Indicates whether this model supports video generation.
    /// </summary>
    public bool SupportsVideoGeneration { get; set; } = false;

    /// <summary>
    /// Indicates whether this model supports embedding generation.
    /// </summary>
    public bool SupportsEmbeddings { get; set; } = false;

    /// <summary>
    /// Indicates whether this model supports chat completions.
    /// </summary>
    public bool SupportsChat { get; set; } = false;

    /// <summary>
    /// Indicates whether this model supports function calling.
    /// </summary>
    public bool SupportsFunctionCalling { get; set; } = false;

    /// <summary>
    /// Indicates whether this model supports streaming responses.
    /// </summary>
    public bool SupportsStreaming { get; set; } = false;

    /// <summary>
    /// The tokenizer type used by this model (e.g., "cl100k_base", "p50k_base", "claude").
    /// </summary>
    public TokenizerType TokenizerType { get; set; }


    }
}