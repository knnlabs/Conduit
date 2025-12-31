using System.Text.Json.Serialization;

namespace ConduitLLM.Functions.Providers.Tavily.Models;

/// <summary>
/// Request model for Tavily search API.
/// </summary>
/// <remarks>
/// Based on Tavily API documentation: https://docs.tavily.com/reference/search
/// </remarks>
public class TavilySearchRequest
{
    /// <summary>
    /// The search query string (required).
    /// </summary>
    [JsonPropertyName("query")]
    public required string Query { get; set; }

    /// <summary>
    /// Search topic category: "general" (default), "news", or "finance".
    /// </summary>
    [JsonPropertyName("topic")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Topic { get; set; }

    /// <summary>
    /// Search depth: "basic" (1 credit, default) or "advanced" (2 credits).
    /// </summary>
    [JsonPropertyName("search_depth")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SearchDepth { get; set; }

    /// <summary>
    /// Maximum number of results to return (0-20, default 5).
    /// </summary>
    [JsonPropertyName("max_results")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxResults { get; set; }

    /// <summary>
    /// Include LLM-generated answer: true, false (default), "basic", or "advanced".
    /// </summary>
    [JsonPropertyName("include_answer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? IncludeAnswer { get; set; }

    /// <summary>
    /// Include raw HTML content: true, false (default), "markdown", or "text".
    /// </summary>
    [JsonPropertyName("include_raw_content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? IncludeRawContent { get; set; }

    /// <summary>
    /// Include image search results (default: false).
    /// </summary>
    [JsonPropertyName("include_images")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IncludeImages { get; set; }

    /// <summary>
    /// Add descriptions to images (requires include_images enabled).
    /// </summary>
    [JsonPropertyName("include_image_descriptions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IncludeImageDescriptions { get; set; }

    /// <summary>
    /// Include favicon URLs in results (default: false).
    /// </summary>
    [JsonPropertyName("include_favicon")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IncludeFavicon { get; set; }

    /// <summary>
    /// Temporal filter: "day", "week", "month", or "year".
    /// </summary>
    [JsonPropertyName("time_range")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TimeRange { get; set; }

    /// <summary>
    /// Custom start date (ISO 8601 format: YYYY-MM-DD).
    /// </summary>
    [JsonPropertyName("start_date")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StartDate { get; set; }

    /// <summary>
    /// Custom end date (ISO 8601 format: YYYY-MM-DD).
    /// </summary>
    [JsonPropertyName("end_date")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EndDate { get; set; }

    /// <summary>
    /// Whitelist specific domains (max 300).
    /// </summary>
    [JsonPropertyName("include_domains")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? IncludeDomains { get; set; }

    /// <summary>
    /// Blacklist specific domains (max 150).
    /// </summary>
    [JsonPropertyName("exclude_domains")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? ExcludeDomains { get; set; }

    /// <summary>
    /// Country-specific boost (ISO country code, e.g., "US"). Only available with "general" topic.
    /// </summary>
    [JsonPropertyName("country")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Country { get; set; }

    /// <summary>
    /// Automatically configure search parameters based on query (beta feature, costs 2 credits).
    /// </summary>
    [JsonPropertyName("auto_parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AutoParameters { get; set; }
}
