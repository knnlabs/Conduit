using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Admin.Models.ModelAuthors
{
    /// <summary>
    /// Simplified model series information for display within an author context.
    /// </summary>
    /// <remarks>
    /// Provides a lightweight view of series belonging to a specific author.
    /// Used when listing an author's model families without full details.
    /// </remarks>
    public class SimpleModelSeriesDto
    {
        /// <summary>
        /// Gets or sets the unique identifier of the series.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the model series.
        /// </summary>
        /// <remarks>
        /// Examples within an author's context:
        /// - OpenAI: "GPT", "DALL-E", "Whisper"
        /// - Anthropic: "Claude", "Claude Instant"
        /// - Meta: "Llama", "Code Llama"
        /// </remarks>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the series.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the tokenizer type used by this series.
        /// </summary>
        /// <remarks>
        /// All models in a series typically share the same tokenization scheme.
        /// </remarks>
        public TokenizerType TokenizerType { get; set; }

        /// <summary>
        /// Gets or sets the number of models in this series.
        /// </summary>
        /// <remarks>
        /// Provides a quick count of how many model versions exist in the series.
        /// Helps understand the series size and development activity.
        /// </remarks>
        public int ModelCount { get; set; }
    }
}