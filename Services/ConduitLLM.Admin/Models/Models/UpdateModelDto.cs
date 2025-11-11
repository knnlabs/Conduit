namespace ConduitLLM.Admin.Models.Models
{
    /// <summary>
    /// Data transfer object for updating an existing AI model in the system.
    /// </summary>
    /// <remarks>
    /// This DTO supports partial updates to model properties. Only properties that are
    /// provided (non-null) will be updated. This allows for surgical updates without
    /// needing to provide all model properties.
    /// 
    /// Common update scenarios include:
    /// - Activating/deactivating a model (IsActive)
    /// - Changing the model's capabilities configuration (ModelCapabilitiesId)
    /// - Reassigning to a different series (ModelSeriesId)
    /// - Renaming a model (Name) - use with caution as it may break existing references
    /// 
    /// Note that changing fundamental properties like the model name should be done
    /// carefully as it may impact existing virtual keys, provider mappings, and
    /// active requests using the model.
    /// </remarks>
    public class UpdateModelDto
    {
        /// <summary>
        /// Gets or sets the ID of the model to update.
        /// </summary>
        /// <remarks>
        /// This must match the ID in the request URL for validation.
        /// The ID identifies which model record will be updated.
        /// </remarks>
        /// <value>The unique identifier of the model to update.</value>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the new canonical name for the model.
        /// </summary>
        /// <remarks>
        /// Changing the model name should be done with extreme caution as it may break:
        /// - Existing virtual key configurations that reference this model
        /// - Provider mappings that use the model name for routing
        /// - Active client applications using the old name
        /// 
        /// Only provide this if you intend to rename the model. Leave null to keep
        /// the existing name. The new name must be unique within the system.
        /// </remarks>
        /// <value>The new model name, or null to keep existing.</value>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the new model series ID.
        /// </summary>
        /// <remarks>
        /// Use this to reassign a model to a different series. This might be done
        /// if the model was initially miscategorized or if series are being reorganized.
        /// The new series must exist. Leave null to keep the current series assignment.
        /// </remarks>
        /// <value>The new series ID, or null to keep existing.</value>
        public int? ModelSeriesId { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports chat/conversation interactions.
        /// </summary>
        /// <value>True to enable chat support, false to disable, or null to keep existing.</value>
        public bool? SupportsChat { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports vision/image understanding.
        /// </summary>
        /// <value>True to enable vision support, false to disable, or null to keep existing.</value>
        public bool? SupportsVision { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports function/tool calling.
        /// </summary>
        /// <value>True to enable function calling, false to disable, or null to keep existing.</value>
        public bool? SupportsFunctionCalling { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports streaming responses.
        /// </summary>
        /// <value>True to enable streaming, false to disable, or null to keep existing.</value>
        public bool? SupportsStreaming { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports image generation.
        /// </summary>
        /// <value>True to enable image generation, false to disable, or null to keep existing.</value>
        public bool? SupportsImageGeneration { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports video generation.
        /// </summary>
        /// <value>True to enable video generation, false to disable, or null to keep existing.</value>
        public bool? SupportsVideoGeneration { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports text embeddings generation.
        /// </summary>
        /// <value>True to enable embeddings, false to disable, or null to keep existing.</value>
        public bool? SupportsEmbeddings { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of input tokens the model can process.
        /// </summary>
        /// <value>The new max input tokens, or null to keep existing.</value>
        public int? MaxInputTokens { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of output tokens the model can generate.
        /// </summary>
        /// <value>The new max output tokens, or null to keep existing.</value>
        public int? MaxOutputTokens { get; set; }

        /// <summary>
        /// Gets or sets the new activation status for the model.
        /// </summary>
        /// <remarks>
        /// Use this to activate or deactivate a model. Common scenarios:
        /// - Set to false to deactivate a deprecated model
        /// - Set to false temporarily when a model is experiencing issues
        /// - Set to true to reactivate a previously deactivated model
        /// 
        /// Deactivating a model prevents new requests but doesn't affect existing
        /// provider mappings or cost configurations. Leave null to keep current status.
        /// </remarks>
        /// <value>True to activate, false to deactivate, or null to keep existing status.</value>
        public bool? IsActive { get; set; }

        /// <summary>
        /// Gets or sets the model-specific parameter configuration for UI generation.
        /// </summary>
        /// <remarks>
        /// This JSON string contains parameter definitions that override the series-level
        /// parameters. When null or empty, the model uses its series' parameter configuration.
        /// This allows for model-specific customization while maintaining series defaults.
        /// 
        /// The JSON should follow the same schema as series parameters, defining UI controls
        /// like sliders, selects, and inputs for model-specific parameters.
        /// </remarks>
        /// <value>JSON string containing parameter definitions, or null to use series defaults.</value>
        public string? ModelParameters { get; set; }
    }
}