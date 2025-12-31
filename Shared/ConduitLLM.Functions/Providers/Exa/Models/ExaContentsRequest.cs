using System.Text.Json.Serialization;

namespace ConduitLLM.Functions.Providers.Exa.Models;

/// <summary>
/// Request model for Exa.ai get contents API.
/// </summary>
/// <remarks>
/// Based on Exa API documentation: https://docs.exa.ai/reference/get-contents
/// </remarks>
public class ExaContentsRequest
{
    /// <summary>
    /// Array of URLs to retrieve content from (required).
    /// </summary>
    [JsonPropertyName("urls")]
    public required List<string> Urls { get; set; }

    /// <summary>
    /// Enable text content extraction (boolean or object with maxCharacters and includeHtmlTags).
    /// </summary>
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Text { get; set; }

    /// <summary>
    /// Enable highlights extraction (object with query, numSentences, highlightsPerUrl).
    /// </summary>
    [JsonPropertyName("highlights")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Highlights { get; set; }

    /// <summary>
    /// Enable summary generation (object with query and optional schema for structured output).
    /// </summary>
    [JsonPropertyName("summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Summary { get; set; }

    /// <summary>
    /// Livecrawl mode: "never", "fallback" (default), "always", "preferred".
    /// </summary>
    [JsonPropertyName("livecrawl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Livecrawl { get; set; }

    /// <summary>
    /// Livecrawl timeout in milliseconds (default: 10000).
    /// </summary>
    [JsonPropertyName("livecrawlTimeout")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LivecrawlTimeout { get; set; }

    /// <summary>
    /// Number of subpages to crawl per URL (default: 0).
    /// </summary>
    [JsonPropertyName("subpages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Subpages { get; set; }

    /// <summary>
    /// Keyword or array of keywords to filter subpages.
    /// </summary>
    [JsonPropertyName("subpageTarget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? SubpageTarget { get; set; }

    /// <summary>
    /// Additional data to extract (object with links and imageLinks counts).
    /// </summary>
    [JsonPropertyName("extras")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Extras { get; set; }

    /// <summary>
    /// Return page contents as a context string for LLM (boolean or object).
    /// </summary>
    [JsonPropertyName("context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Context { get; set; }
}
