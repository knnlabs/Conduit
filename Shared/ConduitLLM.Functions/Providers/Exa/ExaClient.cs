using System.Diagnostics;
using System.Net;
using System.Text.Json;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Interfaces;
using ConduitLLM.Functions.Models;
using ConduitLLM.Functions.Providers.Exa.Models;
using ConduitLLM.Functions.Utilities;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Functions.Providers.Exa;

/// <summary>
/// Client for Exa.ai neural and keyword search API.
/// </summary>
/// <remarks>
/// Exa provides embeddings-based semantic search and traditional keyword search.
/// API documentation: https://docs.exa.ai/reference/search
///
/// This client handles:
/// - Search operations with various filters
/// - Content extraction (text, highlights, summaries)
/// - Usage tracking for billing
/// - Authentication verification
/// </remarks>
public partial class ExaClient : FunctionClientBase, IFunctionClient
{
    private const string DefaultBaseUrl = "https://api.exa.ai";

    /// <inheritdoc />
    public FunctionProviderType ProviderType => FunctionProviderType.Exa;

    /// <inheritdoc />
    public override string ProviderName => "Exa";

    /// <summary>
    /// Creates a new instance of the ExaClient.
    /// </summary>
    /// <param name="configuration">Function configuration.</param>
    /// <param name="credential">API credential.</param>
    /// <param name="httpClientFactory">HTTP client factory (optional, for connection pooling).</param>
    /// <param name="logger">Logger instance.</param>
    public ExaClient(
        FunctionConfiguration configuration,
        FunctionCredential credential,
        IHttpClientFactory? httpClientFactory,
        ILogger<ExaClient> logger)
        : base(configuration, credential, httpClientFactory, logger, DefaultBaseUrl)
    {
    }

    /// <inheritdoc />
    protected override void ApplyAuthHeader(HttpClient client, string apiKey)
    {
        // Exa uses x-api-key header
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
    }

    /// <summary>
    /// Validates request parameters and converts to ExaSearchRequest.
    /// </summary>
    protected ExaSearchRequest MapToExaSearchRequest(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("query", out var queryObj) || queryObj == null)
        {
            throw new ArgumentException("Parameter 'query' is required", nameof(parameters));
        }

        var request = new ExaSearchRequest
        {
            Query = JsonElementConverter.ConvertToString(queryObj)!
        };

        // Map optional parameters
        if (parameters.TryGetValue("type", out var typeObj))
            request.Type = JsonElementConverter.ConvertToString(typeObj);

        if (parameters.TryGetValue("numResults", out var numObj))
            request.NumResults = JsonElementConverter.ConvertToInt32(numObj);

        if (parameters.TryGetValue("category", out var catObj))
            request.Category = JsonElementConverter.ConvertToString(catObj);

        if (parameters.TryGetValue("includeDomains", out var incObj))
            request.IncludeDomains = JsonElementConverter.ConvertToStringList(incObj);

        if (parameters.TryGetValue("excludeDomains", out var excObj))
            request.ExcludeDomains = JsonElementConverter.ConvertToStringList(excObj);

        if (parameters.TryGetValue("startCrawlDate", out var startCrawlObj))
            request.StartCrawlDate = JsonElementConverter.ConvertToString(startCrawlObj);

        if (parameters.TryGetValue("endCrawlDate", out var endCrawlObj))
            request.EndCrawlDate = JsonElementConverter.ConvertToString(endCrawlObj);

        if (parameters.TryGetValue("startPublishedDate", out var startPubObj))
            request.StartPublishedDate = JsonElementConverter.ConvertToString(startPubObj);

        if (parameters.TryGetValue("endPublishedDate", out var endPubObj))
            request.EndPublishedDate = JsonElementConverter.ConvertToString(endPubObj);

        if (parameters.TryGetValue("text", out var textObj))
            request.Text = JsonElementConverter.ConvertJsonElement(textObj);

        if (parameters.TryGetValue("highlights", out var highlightsObj))
            request.Highlights = JsonElementConverter.ConvertJsonElement(highlightsObj);

        if (parameters.TryGetValue("summary", out var summaryObj))
            request.Summary = JsonElementConverter.ConvertJsonElement(summaryObj);

        if (parameters.TryGetValue("livecrawl", out var livecrawlObj))
            request.Livecrawl = JsonElementConverter.ConvertToString(livecrawlObj);

        if (parameters.TryGetValue("userLocation", out var locationObj))
            request.UserLocation = JsonElementConverter.ConvertToString(locationObj);

        return request;
    }

    /// <inheritdoc />
    public async Task<FunctionExecutionResult> ExecuteAsync(
        Dictionary<string, object> parameters,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        // Route based on operation type
        if (parameters.ContainsKey("urls"))
        {
            // Get contents operation (requires 'urls' parameter)
            return await ExecuteGetContentsAsync(parameters, apiKey, cancellationToken);
        }
        else if (parameters.ContainsKey("query"))
        {
            // Search operation (requires 'query' parameter)
            return await ExecuteSearchAsync(parameters, apiKey, cancellationToken);
        }
        else
        {
            throw new ArgumentException(
                "Either 'query' (for search) or 'urls' (for get contents) must be provided",
                nameof(parameters));
        }
    }

}
