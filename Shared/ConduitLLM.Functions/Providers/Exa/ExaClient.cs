using System.Diagnostics;
using System.Net;
using System.Text.Json;
using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Interfaces;
using ConduitLLM.Functions.Models;
using ConduitLLM.Functions.Providers.Exa.Models;
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
public partial class ExaClient : IFunctionClient
{
    private readonly FunctionConfiguration _configuration;
    private readonly FunctionCredential _credential;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly ILogger<ExaClient> _logger;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string DefaultBaseUrl = "https://api.exa.ai";

    /// <inheritdoc />
    public FunctionProviderType ProviderType => FunctionProviderType.Exa;

    /// <inheritdoc />
    public string ProviderName => "Exa";

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
    /// Determines the effective base URL for the Exa API.
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

        // Exa uses x-api-key header
        var effectiveApiKey = apiKey ?? _credential.ApiKey;
        if (!string.IsNullOrWhiteSpace(effectiveApiKey))
        {
            client.DefaultRequestHeaders.Add("x-api-key", effectiveApiKey);
        }

        // Default timeout (can be overridden by configuration)
        client.Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds ?? 30);
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
            Query = queryObj.ToString()!
        };

        // Map optional parameters
        if (parameters.TryGetValue("type", out var typeObj))
            request.Type = typeObj.ToString();

        if (parameters.TryGetValue("numResults", out var numObj))
            request.NumResults = Convert.ToInt32(numObj);

        if (parameters.TryGetValue("category", out var catObj))
            request.Category = catObj.ToString();

        if (parameters.TryGetValue("includeDomains", out var incObj) && incObj is List<string> incDomains)
            request.IncludeDomains = incDomains;

        if (parameters.TryGetValue("excludeDomains", out var excObj) && excObj is List<string> excDomains)
            request.ExcludeDomains = excDomains;

        if (parameters.TryGetValue("startCrawlDate", out var startCrawlObj))
            request.StartCrawlDate = startCrawlObj.ToString();

        if (parameters.TryGetValue("endCrawlDate", out var endCrawlObj))
            request.EndCrawlDate = endCrawlObj.ToString();

        if (parameters.TryGetValue("startPublishedDate", out var startPubObj))
            request.StartPublishedDate = startPubObj.ToString();

        if (parameters.TryGetValue("endPublishedDate", out var endPubObj))
            request.EndPublishedDate = endPubObj.ToString();

        if (parameters.TryGetValue("text", out var textObj))
            request.Text = textObj;

        if (parameters.TryGetValue("highlights", out var highlightsObj))
            request.Highlights = highlightsObj;

        if (parameters.TryGetValue("summary", out var summaryObj))
            request.Summary = summaryObj;

        if (parameters.TryGetValue("livecrawl", out var livecrawlObj))
            request.Livecrawl = livecrawlObj.ToString();

        if (parameters.TryGetValue("userLocation", out var locationObj))
            request.UserLocation = locationObj.ToString();

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

    /// <summary>
    /// Handles HTTP errors and creates appropriate exception messages.
    /// </summary>
    protected Exception HandleHttpError(HttpStatusCode statusCode, string? responseBody)
    {
        var message = statusCode switch
        {
            HttpStatusCode.Unauthorized => "Invalid API key for Exa",
            HttpStatusCode.Forbidden => "Access forbidden - check API key permissions",
            HttpStatusCode.TooManyRequests => "Rate limit exceeded for Exa API",
            HttpStatusCode.BadRequest => $"Bad request to Exa API: {responseBody}",
            HttpStatusCode.ServiceUnavailable => "Exa API is temporarily unavailable",
            HttpStatusCode.GatewayTimeout => "Exa API request timed out",
            _ => $"Exa API error: {(int)statusCode} {statusCode}"
        };

        _logger.LogError("Exa API error: {StatusCode} - {Message}", statusCode, message);
        return new InvalidOperationException(message);
    }
}
