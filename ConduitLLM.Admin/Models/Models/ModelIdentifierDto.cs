namespace ConduitLLM.Admin.Models.Models
{
    /// <summary>
    /// DTO for creating a model identifier
    /// </summary>
    public class CreateModelIdentifierDto
    {
        /// <summary>
        /// The identifier string used by a provider
        /// </summary>
        public string Identifier { get; set; } = string.Empty;

        /// <summary>
        /// The provider type that uses this identifier (e.g., "OpenAI", "Groq")
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Whether this is the primary identifier
        /// </summary>
        public bool? IsPrimary { get; set; }

        /// <summary>
        /// Optional metadata as JSON
        /// </summary>
        public string? Metadata { get; set; }
    }

    /// <summary>
    /// DTO for updating a model identifier
    /// </summary>
    public class UpdateModelIdentifierDto
    {
        /// <summary>
        /// The identifier string used by a provider
        /// </summary>
        public string Identifier { get; set; } = string.Empty;

        /// <summary>
        /// The provider type that uses this identifier
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Whether this is the primary identifier
        /// </summary>
        public bool? IsPrimary { get; set; }

        /// <summary>
        /// Optional metadata as JSON
        /// </summary>
        public string? Metadata { get; set; }
    }
}