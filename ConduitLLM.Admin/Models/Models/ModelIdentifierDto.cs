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

        /// <summary>
        /// Provider-specific override for maximum input tokens
        /// </summary>
        public int? MaxInputTokens { get; set; }

        /// <summary>
        /// Provider-specific override for maximum output tokens
        /// </summary>
        public int? MaxOutputTokens { get; set; }

        /// <summary>
        /// Speed score relative to baseline (1.0 = baseline, 2.0 = 2x faster)
        /// </summary>
        public decimal? SpeedScore { get; set; }

        /// <summary>
        /// Quality score (0.0 to 1.0, where 1.0 = highest quality)
        /// </summary>
        public decimal? QualityScore { get; set; }

        /// <summary>
        /// Model variation or quantization level (e.g., "GGUF", "Q4_K_M", "instruct")
        /// </summary>
        public string? ProviderVariation { get; set; }
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

        /// <summary>
        /// Provider-specific override for maximum input tokens
        /// </summary>
        public int? MaxInputTokens { get; set; }

        /// <summary>
        /// Provider-specific override for maximum output tokens
        /// </summary>
        public int? MaxOutputTokens { get; set; }

        /// <summary>
        /// Speed score relative to baseline (1.0 = baseline, 2.0 = 2x faster)
        /// </summary>
        public decimal? SpeedScore { get; set; }

        /// <summary>
        /// Quality score (0.0 to 1.0, where 1.0 = highest quality)
        /// </summary>
        public decimal? QualityScore { get; set; }

        /// <summary>
        /// Model variation or quantization level (e.g., "GGUF", "Q4_K_M", "instruct")
        /// </summary>
        public string? ProviderVariation { get; set; }
    }
}