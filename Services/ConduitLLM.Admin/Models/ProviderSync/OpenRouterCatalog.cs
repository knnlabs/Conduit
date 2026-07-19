using System.Text.Json.Serialization;

namespace ConduitLLM.Admin.Models.ProviderSync
{
    /// <summary>Typed subset of OpenRouter's /models response used for drift detection.</summary>
    internal record OpenRouterCatalogResponse
    {
        [JsonPropertyName("data")]
        public List<OpenRouterCatalogModel>? Data { get; init; }
    }

    internal record OpenRouterCatalogModel
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("canonical_slug")]
        public string? CanonicalSlug { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("context_length")]
        public int? ContextLength { get; init; }

        [JsonPropertyName("architecture")]
        public OpenRouterCatalogArchitecture? Architecture { get; init; }

        [JsonPropertyName("pricing")]
        public OpenRouterCatalogPricing? Pricing { get; init; }

        [JsonPropertyName("top_provider")]
        public OpenRouterCatalogTopProvider? TopProvider { get; init; }

        [JsonPropertyName("supported_parameters")]
        public List<string>? SupportedParameters { get; init; }

        [JsonPropertyName("expiration_date")]
        public string? ExpirationDate { get; init; }
    }

    internal record OpenRouterCatalogArchitecture
    {
        [JsonPropertyName("input_modalities")]
        public List<string>? InputModalities { get; init; }

        [JsonPropertyName("output_modalities")]
        public List<string>? OutputModalities { get; init; }
    }

    internal record OpenRouterCatalogPricing
    {
        [JsonPropertyName("prompt")]
        public string? Prompt { get; init; }

        [JsonPropertyName("completion")]
        public string? Completion { get; init; }

        [JsonPropertyName("input_cache_read")]
        public string? InputCacheRead { get; init; }

        [JsonPropertyName("input_cache_write")]
        public string? InputCacheWrite { get; init; }
    }

    internal record OpenRouterCatalogTopProvider
    {
        [JsonPropertyName("max_completion_tokens")]
        public int? MaxCompletionTokens { get; init; }

        [JsonPropertyName("context_length")]
        public int? ContextLength { get; init; }
    }
}
