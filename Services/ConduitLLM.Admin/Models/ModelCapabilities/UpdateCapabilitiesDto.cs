namespace ConduitLLM.Admin.Models.ModelCapabilities
{
    /// <summary>
    /// Data transfer object for updating an existing model capabilities configuration.
    /// </summary>
    /// <remarks>
    /// Supports partial updates - only non-null properties will be modified.
    /// Changes to capabilities affect all models using this configuration,
    /// so updates should be made carefully.
    /// </remarks>
    public class UpdateCapabilitiesDto
    {
        /// <summary>
        /// Gets or sets the ID of the capabilities configuration to update.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the new chat support status, or null to keep existing.
        /// </summary>
        public bool? SupportsChat { get; set; }

        /// <summary>
        /// Gets or sets the new vision support status, or null to keep existing.
        /// </summary>
        public bool? SupportsVision { get; set; }

        /// <summary>
        /// Gets or sets the new function calling support, or null to keep existing.
        /// </summary>
        public bool? SupportsFunctionCalling { get; set; }

        /// <summary>
        /// Gets or sets the new streaming support, or null to keep existing.
        /// </summary>
        public bool? SupportsStreaming { get; set; }


        /// <summary>
        /// Gets or sets the new image generation support, or null to keep existing.
        /// </summary>
        public bool? SupportsImageGeneration { get; set; }

        /// <summary>
        /// Gets or sets the new video generation support, or null to keep existing.
        /// </summary>
        public bool? SupportsVideoGeneration { get; set; }

        /// <summary>
        /// Gets or sets the new embeddings support, or null to keep existing.
        /// </summary>
        public bool? SupportsEmbeddings { get; set; }

        /// <summary>
        /// Gets or sets the new max input token limit, or null to keep existing.
        /// </summary>
        public int? MaxInputTokens { get; set; }

        /// <summary>
        /// Gets or sets the new max output token limit, or null to keep existing.
        /// </summary>
        public int? MaxOutputTokens { get; set; }

        /// <summary>
        /// Gets or sets the new min token requirement, or null to keep existing.
        /// </summary>
        public int? MinTokens { get; set; }

        /// <summary>
        /// Gets or sets the new tokenizer type, or null to keep existing.
        /// </summary>
        public TokenizerType? TokenizerType { get; set; }

    }
}