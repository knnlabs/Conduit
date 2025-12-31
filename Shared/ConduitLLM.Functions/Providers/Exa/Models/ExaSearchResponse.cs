using System.Text.Json.Serialization;

namespace ConduitLLM.Functions.Providers.Exa.Models;

/// <summary>
/// Response model from Exa.ai search API.
/// </summary>
public class ExaSearchResponse
{
    /// <summary>
    /// Unique request identifier from Exa.
    /// </summary>
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// The actual search type used ("neural", "keyword", etc.).
    /// </summary>
    [JsonPropertyName("resolvedSearchType")]
    public string ResolvedSearchType { get; set; } = string.Empty;

    /// <summary>
    /// Array of search results.
    /// </summary>
    [JsonPropertyName("results")]
    public List<ExaResult> Results { get; set; } = new();

    /// <summary>
    /// Cost breakdown from Exa (for reconciliation).
    /// </summary>
    [JsonPropertyName("costDollars")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExaCostBreakdown? CostDollars { get; set; }
}

/// <summary>
/// Individual search result from Exa.
/// </summary>
public class ExaResult
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
    /// Published date (ISO 8601, if available).
    /// </summary>
    [JsonPropertyName("publishedDate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PublishedDate { get; set; }

    /// <summary>
    /// Author name (if available).
    /// </summary>
    [JsonPropertyName("author")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Author { get; set; }

    /// <summary>
    /// Similarity score (for neural search).
    /// </summary>
    [JsonPropertyName("score")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Score { get; set; }

    /// <summary>
    /// Full text content (if requested).
    /// </summary>
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    /// <summary>
    /// Highlights (if requested).
    /// </summary>
    [JsonPropertyName("highlights")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Highlights { get; set; }

    /// <summary>
    /// Highlight scores (parallel to highlights array).
    /// </summary>
    [JsonPropertyName("highlightScores")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<double>? HighlightScores { get; set; }

    /// <summary>
    /// AI-generated summary (if requested).
    /// </summary>
    [JsonPropertyName("summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Summary { get; set; }

    /// <summary>
    /// Subpages crawled from this result (for get contents API).
    /// </summary>
    [JsonPropertyName("subpages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ExaResult>? Subpages { get; set; }

    /// <summary>
    /// Additional extracted data like links and images (for get contents API).
    /// </summary>
    [JsonPropertyName("extras")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExaExtras? Extras { get; set; }
}

/// <summary>
/// Cost breakdown from Exa API response.
/// All fields are nullable as Exa may not always include cost information.
/// </summary>
public class ExaCostBreakdown
{
    /// <summary>
    /// Search operation cost breakdown by search type (e.g., {"neural": 0.005}).
    /// Exa returns this as an object with search-type-specific costs.
    /// </summary>
    [JsonPropertyName("search")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, decimal>? Search { get; set; }

    /// <summary>
    /// Text extraction cost.
    /// </summary>
    [JsonPropertyName("getText")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? GetText { get; set; }

    /// <summary>
    /// Highlights extraction cost.
    /// </summary>
    [JsonPropertyName("getHighlights")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? GetHighlights { get; set; }

    /// <summary>
    /// Summary generation cost.
    /// </summary>
    [JsonPropertyName("getSummary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? GetSummary { get; set; }

    /// <summary>
    /// Total cost reported by Exa.
    /// </summary>
    [JsonPropertyName("total")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Total { get; set; }
}

/// <summary>
/// Additional extracted data from get contents API.
/// </summary>
public class ExaExtras
{
    /// <summary>
    /// Links extracted from the page.
    /// </summary>
    [JsonPropertyName("links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Links { get; set; }

    /// <summary>
    /// Image links extracted from the page.
    /// </summary>
    [JsonPropertyName("imageLinks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? ImageLinks { get; set; }
}
