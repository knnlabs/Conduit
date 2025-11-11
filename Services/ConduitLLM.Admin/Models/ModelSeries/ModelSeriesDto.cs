namespace ConduitLLM.Admin.Models.ModelSeries
{
    /// <summary>
    /// Data transfer object representing a series or family of related AI models.
    /// </summary>
    /// <remarks>
    /// A ModelSeries groups related models together, typically representing different versions
    /// or sizes of the same model family. For example:
    /// - The "GPT" series includes GPT-3.5, GPT-4, GPT-4-Turbo
    /// - The "Claude" series includes Claude-2, Claude-3-Haiku, Claude-3-Opus, Claude-3-Sonnet
    /// - The "Llama" series includes Llama-2-7B, Llama-2-70B, Llama-3-8B, Llama-3-70B
    /// 
    /// Series help organize models and provide shared characteristics like:
    /// - Common tokenizer type
    /// - Shared UI parameters and configuration
    /// - Common author/organization
    /// - Similar capabilities and behavior patterns
    /// 
    /// This grouping is essential for UI organization, billing aggregation, and understanding
    /// model relationships and evolution over time.
    /// </remarks>
    public class ModelSeriesDto
    {
        /// <summary>
        /// Gets or sets the unique identifier for this model series.
        /// </summary>
        /// <value>The database-generated ID that uniquely identifies this series.</value>
        public int Id { get; set; }
        
        /// <summary>
        /// Gets or sets the ID of the author/organization that created this model series.
        /// </summary>
        /// <remarks>
        /// Links to the ModelAuthor entity representing the organization or team that
        /// developed this model series. For example: OpenAI, Anthropic, Meta, Google, etc.
        /// </remarks>
        /// <value>The foreign key reference to the ModelAuthor entity.</value>
        public int AuthorId { get; set; }
        
        /// <summary>
        /// Gets or sets the name of the author/organization.
        /// </summary>
        /// <remarks>
        /// This is a denormalized field populated from the related ModelAuthor entity
        /// for convenience in display. Examples: "OpenAI", "Anthropic", "Meta", "Mistral AI".
        /// </remarks>
        /// <value>The author's display name, or null if not loaded.</value>
        public string? AuthorName { get; set; }
        
        /// <summary>
        /// Gets or sets the name of this model series.
        /// </summary>
        /// <remarks>
        /// The series name identifies the model family. Examples:
        /// - "GPT" for GPT models
        /// - "Claude" for Anthropic's Claude models
        /// - "Llama" for Meta's Llama models
        /// - "Gemini" for Google's Gemini models
        /// 
        /// This name is used for grouping and display purposes in the UI.
        /// </remarks>
        /// <value>The series name.</value>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets a description of this model series.
        /// </summary>
        /// <remarks>
        /// Provides additional context about the model series, its purpose, capabilities,
        /// or notable characteristics. This helps users understand what makes this series
        /// unique or when to choose models from this series.
        /// 
        /// Example: "Advanced language models with strong reasoning capabilities and
        /// extensive training on code and mathematics."
        /// </remarks>
        /// <value>The series description, or null if not provided.</value>
        public string? Description { get; set; }
        
        /// <summary>
        /// Gets or sets the tokenizer type used by models in this series.
        /// </summary>
        /// <remarks>
        /// All models in a series typically use the same tokenization scheme.
        /// This determines how text is split into tokens for processing and affects:
        /// - Token counting for cost calculation
        /// - Context window measurements
        /// - Prompt optimization strategies
        /// 
        /// Common tokenizers:
        /// - Cl100KBase: Used by GPT-4 and GPT-3.5-turbo
        /// - P50KBase: Used by older GPT-3 models
        /// - Claude: Used by Anthropic's Claude models
        /// - Llama: Used by Meta's Llama models
        /// </remarks>
        /// <value>The tokenizer type enum value.</value>
        public TokenizerType TokenizerType { get; set; }
        
        /// <summary>
        /// Gets or sets the UI parameters configuration for this series.
        /// </summary>
        /// <remarks>
        /// A JSON string containing UI-specific configuration for models in this series.
        /// This can include custom parameter ranges, default values, and UI hints for
        /// model-specific settings. The structure is flexible to accommodate different
        /// model requirements.
        /// 
        /// Example structure:
        /// {
        ///   "temperature": {"min": 0, "max": 2, "default": 0.7, "step": 0.1},
        ///   "max_tokens": {"min": 1, "max": 4096, "default": 1000},
        ///   "top_p": {"min": 0, "max": 1, "default": 1, "step": 0.01}
        /// }
        /// 
        /// Defaults to empty object "{}" if not configured.
        /// </remarks>
        /// <value>JSON string containing UI parameter configuration.</value>
        public string Parameters { get; set; } = "{}";
    }
}