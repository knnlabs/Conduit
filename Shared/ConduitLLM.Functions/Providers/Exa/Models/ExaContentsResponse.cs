using System.Text.Json.Serialization;

namespace ConduitLLM.Functions.Providers.Exa.Models;

/// <summary>
/// Response model from Exa.ai get contents API.
/// </summary>
public class ExaContentsResponse
{
    /// <summary>
    /// Unique request identifier from Exa.
    /// </summary>
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Array of content results (one per URL).
    /// </summary>
    [JsonPropertyName("results")]
    public List<ExaResult> Results { get; set; } = new();

    /// <summary>
    /// Combined content string for LLM context (if requested).
    /// </summary>
    [JsonPropertyName("context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Context { get; set; }

    /// <summary>
    /// Status for each URL with success/error information.
    /// </summary>
    [JsonPropertyName("statuses")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ExaUrlStatus>? Statuses { get; set; }

    /// <summary>
    /// Cost breakdown from Exa (for reconciliation).
    /// </summary>
    [JsonPropertyName("costDollars")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExaCostBreakdown? CostDollars { get; set; }
}

/// <summary>
/// Status information for a single URL in get contents response.
/// </summary>
public class ExaUrlStatus
{
    /// <summary>
    /// The URL this status refers to.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Whether content retrieval was successful for this URL.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Error message if retrieval failed.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }

    /// <summary>
    /// HTTP status code from the URL (if applicable).
    /// </summary>
    [JsonPropertyName("statusCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StatusCode { get; set; }
}
