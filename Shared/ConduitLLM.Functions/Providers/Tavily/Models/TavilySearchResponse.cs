using System.Text.Json.Serialization;

namespace ConduitLLM.Functions.Providers.Tavily.Models;

/// <summary>
/// Response model from Tavily search API.
/// </summary>
public class TavilySearchResponse
{
    /// <summary>
    /// The original search query.
    /// </summary>
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// LLM-generated answer to the query (if requested).
    /// </summary>
    [JsonPropertyName("answer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Answer { get; set; }

    /// <summary>
    /// Image search results (if requested).
    /// </summary>
    [JsonPropertyName("images")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TavilyImage>? Images { get; set; }

    /// <summary>
    /// Text search results.
    /// </summary>
    [JsonPropertyName("results")]
    public List<TavilyResult> Results { get; set; } = new();

    /// <summary>
    /// Response time in seconds.
    /// </summary>
    [JsonPropertyName("response_time")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? ResponseTime { get; set; }

    /// <summary>
    /// Unique request identifier from Tavily.
    /// </summary>
    [JsonPropertyName("request_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequestId { get; set; }

    /// <summary>
    /// Auto-configured parameters (if auto_parameters was enabled).
    /// </summary>
    [JsonPropertyName("auto_parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? AutoParameters { get; set; }
}

/// <summary>
/// Individual text search result from Tavily.
/// </summary>
public class TavilyResult
{
    /// <summary>
    /// Page title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Page URL.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// AI-extracted relevant content snippets.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Relevancy score (0-1).
    /// </summary>
    [JsonPropertyName("score")]
    public double Score { get; set; }

    /// <summary>
    /// Cleaned and parsed HTML content (if include_raw_content was requested).
    /// </summary>
    [JsonPropertyName("raw_content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RawContent { get; set; }

    /// <summary>
    /// Favicon URL (if include_favicon was requested).
    /// </summary>
    [JsonPropertyName("favicon")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Favicon { get; set; }

    /// <summary>
    /// Publication date (ISO 8601, only available for news topic).
    /// </summary>
    [JsonPropertyName("published_date")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PublishedDate { get; set; }
}

/// <summary>
/// Individual image search result from Tavily.
/// </summary>
public class TavilyImage
{
    /// <summary>
    /// Image URL.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Image description (if include_image_descriptions was requested).
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }
}
