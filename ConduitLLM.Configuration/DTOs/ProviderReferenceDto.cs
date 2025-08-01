namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Lightweight DTO for referencing providers without exposing sensitive data
    /// </summary>
    public class ProviderReferenceDto
    {
        /// <summary>
        /// The unique identifier of the provider - this is the canonical identifier
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The provider type (e.g., OpenAI, Anthropic)
        /// </summary>
        public ProviderType ProviderType { get; set; }

        /// <summary>
        /// Display name for the provider instance (e.g., "Production OpenAI", "Dev Azure OpenAI")
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Whether this provider is currently enabled
        /// </summary>
        public bool IsEnabled { get; set; }
    }
}