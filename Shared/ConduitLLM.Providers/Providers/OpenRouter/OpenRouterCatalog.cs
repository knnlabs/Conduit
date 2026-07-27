using System.Text.Json.Serialization;

namespace ConduitLLM.Providers.OpenRouter;

/// <summary>OpenRouter's typed <c>/models</c> response contract.</summary>
internal sealed record OpenRouterCatalogResponse
{
    [JsonPropertyName("data")]
    public required List<OpenRouterCatalogModel> Data { get; init; }
}

/// <summary>Model metadata returned by OpenRouter's <c>/models</c> endpoint.</summary>
internal sealed record OpenRouterCatalogModel
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("canonical_slug")]
    public string? CanonicalSlug { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

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

/// <summary>Architecture metadata returned for an OpenRouter model.</summary>
internal sealed record OpenRouterCatalogArchitecture
{
    [JsonPropertyName("input_modalities")]
    public List<string>? InputModalities { get; init; }

    [JsonPropertyName("output_modalities")]
    public List<string>? OutputModalities { get; init; }

    [JsonPropertyName("tokenizer")]
    public string? Tokenizer { get; init; }
}

/// <summary>Per-unit pricing strings returned for an OpenRouter model.</summary>
internal sealed record OpenRouterCatalogPricing
{
    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }

    [JsonPropertyName("completion")]
    public string? Completion { get; init; }

    [JsonPropertyName("request")]
    public string? Request { get; init; }

    [JsonPropertyName("image")]
    public string? Image { get; init; }

    [JsonPropertyName("web_search")]
    public string? WebSearch { get; init; }

    [JsonPropertyName("internal_reasoning")]
    public string? InternalReasoning { get; init; }

    [JsonPropertyName("input_cache_read")]
    public string? InputCacheRead { get; init; }

    [JsonPropertyName("input_cache_write")]
    public string? InputCacheWrite { get; init; }
}

/// <summary>Top-provider token limits returned for an OpenRouter model.</summary>
internal sealed record OpenRouterCatalogTopProvider
{
    [JsonPropertyName("max_completion_tokens")]
    public int? MaxCompletionTokens { get; init; }

    [JsonPropertyName("context_length")]
    public int? ContextLength { get; init; }
}
