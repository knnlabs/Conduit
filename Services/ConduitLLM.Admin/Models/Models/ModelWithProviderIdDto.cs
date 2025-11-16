namespace ConduitLLM.Admin.Models.Models
{
    /// <summary>
    /// Extended model DTO that includes the provider-specific identifier for the model.
    /// </summary>
    /// <remarks>
    /// This DTO extends ModelDto to include provider-specific information, particularly
    /// the identifier used by a specific provider for this model. This is useful when
    /// viewing models in the context of a particular provider.
    /// 
    /// For example, the canonical model "gpt-4" might have different identifiers at different providers:
    /// - OpenAI: "gpt-4-0613"
    /// - Azure OpenAI: "my-gpt4-deployment"
    /// - Another provider: "gpt-4-turbo-latest"
    /// 
    /// This DTO helps bridge the gap between the canonical model representation and
    /// provider-specific implementations.
    /// </remarks>
    public class ModelWithProviderIdDto : ModelDto
    {
        /// <summary>
        /// Gets or sets the provider-specific identifier for this model.
        /// </summary>
        /// <remarks>
        /// This is the actual model ID or deployment name used by the provider's API.
        /// It may differ from the canonical model name. For example:
        /// - Canonical name: "gpt-4"
        /// - Provider model ID: "gpt-4-0613" (OpenAI), "my-custom-deployment" (Azure), etc.
        /// 
        /// This identifier is pulled from the ModelIdentifier table for the specific provider,
        /// or falls back to the canonical model name if no specific identifier is configured.
        /// </remarks>
        /// <example>gpt-4-0613</example>
        /// <example>my-azure-gpt4-deployment</example>
        /// <value>The provider-specific model identifier.</value>
        public string ProviderModelId { get; set; } = string.Empty;
    }
}