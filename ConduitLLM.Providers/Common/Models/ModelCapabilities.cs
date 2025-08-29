using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Providers.Common.Models
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
        /// Gets or sets a value indicating whether the model supports video generation.
        /// </summary>
        public bool VideoGeneration { get; set; }

        /// <summary>
        /// Converts the capabilities to a dictionary for serialization.
        /// </summary>
        /// <returns>A dictionary representation of the capabilities.</returns>
        public Dictionary<string, object?> ToDictionary()
        {
            return new Dictionary<string, object?>
            {
                [nameof(Chat)] = Chat,
                [nameof(TextGeneration)] = TextGeneration,
                [nameof(Embeddings)] = Embeddings,
                [nameof(ImageGeneration)] = ImageGeneration,
                [nameof(Vision)] = Vision,
                [nameof(FunctionCalling)] = FunctionCalling,
                [nameof(ToolUsage)] = ToolUsage,
                [nameof(JsonMode)] = JsonMode,
                [nameof(VideoGeneration)] = VideoGeneration
            };
        }
    }
}
