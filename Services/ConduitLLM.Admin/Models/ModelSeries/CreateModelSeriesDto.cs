namespace ConduitLLM.Admin.Models.ModelSeries
{
    /// <summary>
    /// Data transfer object for creating a new model series in the system.
    /// </summary>
    /// <remarks>
    /// Use this DTO to register a new model series/family before adding individual models.
    /// A series groups related models from the same author/organization and sharing
    /// common characteristics like tokenization and parameter configurations.
    /// 
    /// Before creating a series:
    /// 1. Ensure the ModelAuthor exists (or create it first)
    /// 2. Determine the appropriate tokenizer type for the model family
    /// 3. Prepare any UI parameter configurations if needed
    /// 
    /// After creating a series, you can add individual models that belong to it.
    /// </remarks>
    public class CreateModelSeriesDto
    {
        /// <summary>
        /// Gets or sets the ID of the author/organization for this series.
        /// </summary>
        /// <remarks>
        /// References an existing ModelAuthor entity. The author represents the
        /// organization or team that created this model family. Common authors include:
        /// - OpenAI (for GPT series)
        /// - Anthropic (for Claude series)
        /// - Meta (for Llama series)
        /// - Google (for Gemini/PaLM series)
        /// - Mistral AI (for Mistral series)
        /// 
        /// The author must exist before creating the series.
        /// </remarks>
        /// <value>The foreign key reference to an existing ModelAuthor.</value>
        public int AuthorId { get; set; }
        
        /// <summary>
        /// Gets or sets the name of the model series.
        /// </summary>
        /// <remarks>
        /// Choose a clear, recognizable name for the model family. This should match
        /// how the series is commonly referred to in documentation and discussions.
        /// 
        /// Good examples:
        /// - "GPT" (not "GPT Models" or "OpenAI GPT")
        /// - "Claude" (not "Claude AI" or "Anthropic Claude")
        /// - "Llama" (not "LLaMA" or "Meta Llama")
        /// - "Gemini" (not "Google Gemini")
        /// 
        /// The name should be unique within the author's series collection.
        /// </remarks>
        /// <value>The series name.</value>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets an optional description of the model series.
        /// </summary>
        /// <remarks>
        /// Provide context about what makes this series unique, its primary use cases,
        /// or notable capabilities. This helps users understand when to choose models
        /// from this series.
        /// 
        /// Examples:
        /// - "State-of-the-art language models with advanced reasoning and coding capabilities"
        /// - "Open-source language models optimized for efficiency and broad deployment"
        /// - "Multimodal models supporting text, image, and code understanding"
        /// </remarks>
        /// <value>The series description, or null if not provided.</value>
        public string? Description { get; set; }
        
        /// <summary>
        /// Gets or sets the tokenizer type for models in this series.
        /// </summary>
        /// <remarks>
        /// Select the tokenization scheme used by this model family. This is typically
        /// consistent across all models in a series and determines how text is processed.
        /// 
        /// Common selections:
        /// - TokenizerType.Cl100KBase - For modern OpenAI models (GPT-4, GPT-3.5-turbo)
        /// - TokenizerType.Claude - For Anthropic Claude models
        /// - TokenizerType.Llama - For Meta Llama models
        /// - TokenizerType.Custom - For proprietary tokenization schemes
        /// 
        /// The tokenizer affects token counting, cost calculation, and context limits.
        /// </remarks>
        /// <value>The tokenizer type enum value.</value>
        public TokenizerType TokenizerType { get; set; }
        
        /// <summary>
        /// Gets or sets optional UI parameter configuration for the series.
        /// </summary>
        /// <remarks>
        /// Provide a JSON string defining UI controls and parameter ranges for models
        /// in this series. This enables custom UI experiences tailored to specific
        /// model characteristics.
        /// 
        /// Example JSON structure:
        /// {
        ///   "temperature": {
        ///     "type": "slider",
        ///     "min": 0,
        ///     "max": 2,
        ///     "default": 0.7,
        ///     "step": 0.1,
        ///     "label": "Creativity",
        ///     "description": "Higher values make output more random"
        ///   },
        ///   "max_tokens": {
        ///     "type": "number",
        ///     "min": 1,
        ///     "max": 4096,
        ///     "default": 1000
        ///   }
        /// }
        /// 
        /// If not provided, defaults to "{}" (empty configuration).
        /// </remarks>
        /// <value>JSON string with UI parameters, or null for default.</value>
        public string? Parameters { get; set; }
    }
}