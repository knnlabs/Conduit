using System.Diagnostics;
using System.Net;
using System.Text.Json;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Interfaces;
using ConduitLLM.Functions.Models;
using ConduitLLM.Functions.Providers.Tavily.Models;
using ConduitLLM.Functions.Utilities;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Functions.Providers.Tavily;

/// <summary>
/// Client for Tavily search API.
/// </summary>
/// <remarks>
/// Tavily provides AI-optimized search for RAG applications with structured results.
/// API documentation: https://docs.tavily.com/reference/search
///
/// This client handles:
/// - Search operations with various filters and topics
/// - Content extraction (snippets, raw content, images)
/// - Answer generation via LLM
/// - Usage tracking for billing
/// - Authentication verification
/// </remarks>
public partial class TavilyClient : FunctionClientBase, IFunctionClient
{
    private const string DefaultBaseUrl = "https://api.tavily.com";

    /// <inheritdoc />
    public FunctionProviderType ProviderType => FunctionProviderType.Tavily;

    /// <inheritdoc />
    public override string ProviderName => "Tavily";

    /// <summary>
    /// Creates a new instance of the TavilyClient.
    /// </summary>
    /// <param name="configuration">Function configuration.</param>
    /// <param name="credential">API credential.</param>
    /// <param name="httpClientFactory">HTTP client factory (optional, for connection pooling).</param>
    /// <param name="logger">Logger instance.</param>
    public TavilyClient(
        FunctionConfiguration configuration,
        FunctionCredential credential,
        IHttpClientFactory? httpClientFactory,
        ILogger<TavilyClient> logger)
        : base(configuration, credential, httpClientFactory, logger, DefaultBaseUrl)
    {
    }

    /// <inheritdoc />
    protected override void ApplyAuthHeader(HttpClient client, string apiKey)
    {
        // Tavily uses Bearer token authentication
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    /// <summary>
    /// Validates request parameters and converts to TavilySearchRequest.
    /// </summary>
    protected TavilySearchRequest MapToTavilySearchRequest(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("query", out var queryObj) || queryObj == null)
        {
            throw new ArgumentException("Parameter 'query' is required", nameof(parameters));
        }

        var request = new TavilySearchRequest
        {
            Query = JsonElementConverter.ConvertToString(queryObj)!
        };

        // Map optional parameters
        if (parameters.TryGetValue("topic", out var topicObj))
            request.Topic = JsonElementConverter.ConvertToString(topicObj);

        if (parameters.TryGetValue("search_depth", out var depthObj))
            request.SearchDepth = JsonElementConverter.ConvertToString(depthObj);

        if (parameters.TryGetValue("max_results", out var maxObj))
            request.MaxResults = JsonElementConverter.ConvertToInt32(maxObj);

        if (parameters.TryGetValue("include_answer", out var answerObj))
            request.IncludeAnswer = JsonElementConverter.ConvertJsonElement(answerObj);

        if (parameters.TryGetValue("include_raw_content", out var rawContentObj))
            request.IncludeRawContent = JsonElementConverter.ConvertJsonElement(rawContentObj);

        if (parameters.TryGetValue("include_images", out var imagesObj))
            request.IncludeImages = JsonElementConverter.ConvertToBoolean(imagesObj);

        if (parameters.TryGetValue("include_image_descriptions", out var imageDescObj))
            request.IncludeImageDescriptions = JsonElementConverter.ConvertToBoolean(imageDescObj);

        if (parameters.TryGetValue("include_favicon", out var faviconObj))
            request.IncludeFavicon = JsonElementConverter.ConvertToBoolean(faviconObj);

        if (parameters.TryGetValue("time_range", out var timeRangeObj))
            request.TimeRange = JsonElementConverter.ConvertToString(timeRangeObj);

        if (parameters.TryGetValue("start_date", out var startDateObj))
            request.StartDate = JsonElementConverter.ConvertToString(startDateObj);

        if (parameters.TryGetValue("end_date", out var endDateObj))
            request.EndDate = JsonElementConverter.ConvertToString(endDateObj);

        if (parameters.TryGetValue("include_domains", out var incDomainsObj))
            request.IncludeDomains = JsonElementConverter.ConvertToStringList(incDomainsObj);

        if (parameters.TryGetValue("exclude_domains", out var excDomainsObj))
            request.ExcludeDomains = JsonElementConverter.ConvertToStringList(excDomainsObj);

        if (parameters.TryGetValue("country", out var countryObj))
            request.Country = JsonElementConverter.ConvertToString(countryObj);

        if (parameters.TryGetValue("auto_parameters", out var autoParamsObj))
            request.AutoParameters = JsonElementConverter.ConvertToBoolean(autoParamsObj);

        return request;
    }

    protected override string GetHttpErrorMessage(
        HttpStatusCode statusCode,
        string? responseBody) =>
        statusCode switch
        {
            HttpStatusCode.TooManyRequests => "Rate limit exceeded for Tavily API (100 RPM dev, 1000 RPM production)",
            (HttpStatusCode)432 => "Plan usage limit exceeded - monthly API credit limit reached",
            (HttpStatusCode)433 => "Pay-as-you-go limit exceeded - PAYGO spending limit reached",
            _ => base.GetHttpErrorMessage(statusCode, responseBody)
        };
}
