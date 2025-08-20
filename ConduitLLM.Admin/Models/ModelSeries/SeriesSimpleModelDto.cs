namespace ConduitLLM.Admin.Models.ModelSeries
{
    /// <summary>
    /// Simplified model information for display within a series context.
    /// </summary>
    /// <remarks>
    /// This DTO provides a lightweight view of models when listing them as part of a series.
    /// It includes only the essential information needed for series management and overview,
    /// without the full details like capabilities or provider mappings.
    /// 
    /// Used primarily in the "Get models in series" endpoint to show which models belong
    /// to a particular series without overwhelming the response with detailed information.
    /// This helps in:
    /// - Quick overview of series composition
    /// - Understanding version progression within a series
    /// - Identifying active vs deprecated models in the family
    /// </remarks>
    public class SeriesSimpleModelDto
    {
        /// <summary>
        /// Gets or sets the unique identifier of the model.
        /// </summary>
        /// <value>The model's database ID.</value>
        public int Id { get; set; }
        
        /// <summary>
        /// Gets or sets the canonical name of the model.
        /// </summary>
        /// <remarks>
        /// The standardized model name as used throughout the system.
        /// Within a series context, these names often follow patterns like:
        /// - "gpt-3.5-turbo", "gpt-4", "gpt-4-turbo" (GPT series)
        /// - "claude-2", "claude-3-haiku", "claude-3-sonnet" (Claude series)
        /// - "llama-2-7b", "llama-2-13b", "llama-2-70b" (Llama series)
        /// </remarks>
        /// <value>The model name.</value>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the version identifier of the model.
        /// </summary>
        /// <remarks>
        /// Optional version string providing additional granularity beyond the name.
        /// This might include:
        /// - Release dates: "2024-01-25"
        /// - Version numbers: "v2.1", "3.5"
        /// - Build identifiers: "preview", "stable", "latest"
        /// 
        /// The version helps distinguish between iterations of the same model
        /// and track improvements over time within the series.
        /// </remarks>
        /// <value>The version string, or null if not versioned.</value>
        public string? Version { get; set; }
        
        /// <summary>
        /// Gets or sets whether the model is currently active.
        /// </summary>
        /// <remarks>
        /// Indicates if the model is available for use in the system.
        /// Within a series, you might have:
        /// - Active models: Currently supported and recommended
        /// - Inactive models: Deprecated, discontinued, or temporarily unavailable
        /// 
        /// This helps identify which models in the series are current versus
        /// historical or deprecated versions that are kept for reference.
        /// </remarks>
        /// <value>True if the model is active; otherwise, false.</value>
        public bool IsActive { get; set; }
    }
}