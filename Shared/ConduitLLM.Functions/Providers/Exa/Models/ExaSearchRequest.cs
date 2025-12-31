using System.Text.Json.Serialization;

namespace ConduitLLM.Functions.Providers.Exa.Models;

/// <summary>
/// Request model for Exa.ai search API.
/// </summary>
/// <remarks>
/// Based on Exa API documentation: https://docs.exa.ai/reference/search
/// </remarks>
public class ExaSearchRequest
{
    /// <summary>
    /// The search query string (required).
    /// </summary>
    [JsonPropertyName("query")]
    public required string Query { get; set; }

    /// <summary>
    /// Search type: "neural", "keyword", "auto" (default), or "fast".
    /// </summary>
    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    /// <summary>
    /// Number of results to return (1-100, default 10).
    /// </summary>
    [JsonPropertyName("numResults")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NumResults { get; set; }

    /// <summary>
    /// Filter by content category (e.g., "news", "research paper", "github", "pdf").
    /// </summary>
    [JsonPropertyName("category")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Category { get; set; }

    /// <summary>
    /// Include only results from these domains (max 1200).
    /// </summary>
    [JsonPropertyName("includeDomains")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? IncludeDomains { get; set; }

    /// <summary>
    /// Exclude results from these domains (max 1200).
    /// </summary>
    [JsonPropertyName("excludeDomains")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? ExcludeDomains { get; set; }

    /// <summary>
    /// Filter by crawl date start (ISO 8601).
    /// </summary>
    [JsonPropertyName("startCrawlDate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StartCrawlDate { get; set; }

    /// <summary>
    /// Filter by crawl date end (ISO 8601).
    /// </summary>
    [JsonPropertyName("endCrawlDate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EndCrawlDate { get; set; }

    /// <summary>
    /// Filter by published date start (ISO 8601).
    /// </summary>
    [JsonPropertyName("startPublishedDate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StartPublishedDate { get; set; }

    /// <summary>
    /// Filter by published date end (ISO 8601).
    /// </summary>
    [JsonPropertyName("endPublishedDate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EndPublishedDate { get; set; }

    /// <summary>
    /// Enable text content extraction (boolean or object with maxCharacters).
    /// </summary>
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Text { get; set; }

    /// <summary>
    /// Enable highlights extraction (boolean or object with query, numSentences, highlightsPerUrl).
    /// </summary>
    [JsonPropertyName("highlights")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Highlights { get; set; }

    /// <summary>
    /// Enable summary generation (boolean or object with query).
    /// </summary>
    [JsonPropertyName("summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Summary { get; set; }

    /// <summary>
    /// Livecrawl mode: "never", "fallback", "always", "preferred".
    /// </summary>
    [JsonPropertyName("livecrawl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Livecrawl { get; set; }

    /// <summary>
    /// User location for geographic filtering (ISO country code, e.g., "US").
    /// </summary>
    [JsonPropertyName("userLocation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserLocation { get; set; }
}
