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
public partial class TavilyClient : IFunctionClient
{
    private readonly FunctionConfiguration _configuration;
    private readonly FunctionCredential _credential;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly ILogger<TavilyClient> _logger;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string DefaultBaseUrl = "https://api.tavily.com";

    /// <inheritdoc />
    public FunctionProviderType ProviderType => FunctionProviderType.Tavily;

    /// <inheritdoc />
    public string ProviderName => "Tavily";

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
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _httpClientFactory = httpClientFactory;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Determine base URL (credential > configuration > default)
        _baseUrl = DetermineBaseUrl();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Determines the effective base URL for the Tavily API.
    /// </summary>
    private string DetermineBaseUrl()
    {
        // Priority: Credential BaseUrl > Configuration BaseUrl > Default
        if (!string.IsNullOrWhiteSpace(_credential.BaseUrl))
        {
            return _credential.BaseUrl.TrimEnd('/');
        }

        if (!string.IsNullOrWhiteSpace(_configuration.BaseUrl))
        {
            return _configuration.BaseUrl.TrimEnd('/');
        }

        return DefaultBaseUrl;
    }

    /// <summary>
    /// Creates an HTTP client instance.
    /// </summary>
    protected virtual HttpClient CreateHttpClient(string? apiKey = null)
    {
        HttpClient client;

        if (_httpClientFactory != null)
        {
            client = _httpClientFactory.CreateClient($"{ProviderName}FunctionClient");
        }
        else
        {
            client = new HttpClient();
        }

        ConfigureHttpClient(client, apiKey);
        return client;
    }

    /// <summary>
    /// Configures the HTTP client with headers and authentication.
    /// </summary>
    protected virtual void ConfigureHttpClient(HttpClient client, string? apiKey = null)
    {
        client.BaseAddress = new Uri(_baseUrl);
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM-Functions");

        // Tavily uses Bearer token authentication
        var effectiveApiKey = apiKey ?? _credential.ApiKey;
        if (!string.IsNullOrWhiteSpace(effectiveApiKey))
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {effectiveApiKey}");
        }

        // Default timeout (can be overridden by configuration)
        client.Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds ?? 30);
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

    /// <summary>
    /// Handles HTTP errors and creates appropriate exception messages.
    /// </summary>
    protected Exception HandleHttpError(HttpStatusCode statusCode, string? responseBody)
    {
        var message = statusCode switch
        {
            HttpStatusCode.Unauthorized => "Invalid API key for Tavily",
            HttpStatusCode.Forbidden => "Access forbidden - check API key permissions",
            HttpStatusCode.TooManyRequests => "Rate limit exceeded for Tavily API (100 RPM dev, 1000 RPM production)",
            HttpStatusCode.BadRequest => $"Bad request to Tavily API: {responseBody}",
            HttpStatusCode.ServiceUnavailable => "Tavily API is temporarily unavailable",
            HttpStatusCode.GatewayTimeout => "Tavily API request timed out",
            (HttpStatusCode)432 => "Plan usage limit exceeded - monthly API credit limit reached",
            (HttpStatusCode)433 => "Pay-as-you-go limit exceeded - PAYGO spending limit reached",
            _ => $"Tavily API error: {(int)statusCode} {statusCode}"
        };

        _logger.LogError("Tavily API error: {StatusCode} - {Message}", statusCode, message);
        return new InvalidOperationException(message);
    }
}
