namespace ConduitLLM.Admin.Models.ModelCapabilities
{
    /// <summary>
    /// Simplified model information for display within a capabilities context.
    /// </summary>
    /// <remarks>
    /// Provides a lightweight view of models that share a specific capabilities configuration.
    /// Used when listing models that have the same capabilities to understand which models
    /// will be affected by capability changes.
    /// </remarks>
    public class CapabilitiesSimpleModelDto
    {
        /// <summary>
        /// Gets or sets the unique identifier of the model.
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Gets or sets the canonical name of the model.
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the version identifier of the model.
        /// </summary>
        public string? Version { get; set; }
        
        /// <summary>
        /// Gets or sets whether the model is currently active.
        /// </summary>
        public bool IsActive { get; set; }
    }
}