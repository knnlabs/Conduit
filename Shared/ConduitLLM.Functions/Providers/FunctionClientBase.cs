using System.Text.Json;
using ConduitLLM.Functions.Entities;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Functions.Providers;

/// <summary>
/// Shared scaffolding for HTTP-based function provider clients: base-URL resolution,
/// pooled HttpClient creation, and common header configuration. Authentication is
/// provider-specific and supplied via <see cref="ApplyAuthHeader"/>.
/// </summary>
public abstract class FunctionClientBase
{
    protected readonly FunctionConfiguration _configuration;
    protected readonly FunctionCredential _credential;
    protected readonly IHttpClientFactory? _httpClientFactory;
    protected readonly ILogger _logger;
    protected readonly string _baseUrl;
    protected readonly JsonSerializerOptions _jsonOptions;

    protected FunctionClientBase(
        FunctionConfiguration configuration,
        FunctionCredential credential,
        IHttpClientFactory? httpClientFactory,
        ILogger logger,
        string defaultBaseUrl)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _httpClientFactory = httpClientFactory;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Priority: Credential BaseUrl > Configuration BaseUrl > Default
        _baseUrl = !string.IsNullOrWhiteSpace(_credential.BaseUrl)
            ? _credential.BaseUrl.TrimEnd('/')
            : !string.IsNullOrWhiteSpace(_configuration.BaseUrl)
                ? _configuration.BaseUrl.TrimEnd('/')
                : defaultBaseUrl;

        _jsonOptions = Utilities.FunctionsJsonOptions.CompactWire;
    }

    /// <summary>The user-facing provider name, used for HttpClient naming and diagnostics.</summary>
    public abstract string ProviderName { get; }

    /// <summary>Adds the provider's authentication header for the given API key.</summary>
    protected abstract void ApplyAuthHeader(HttpClient client, string apiKey);

    /// <summary>
    /// Creates an HTTP client instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when IHttpClientFactory is not available.</exception>
    protected virtual HttpClient CreateHttpClient(string? apiKey = null)
    {
        if (_httpClientFactory == null)
        {
            throw new InvalidOperationException(
                $"IHttpClientFactory is required for {ProviderName} but was not injected. " +
                "Ensure IHttpClientFactory is registered in the dependency injection container. " +
                "Creating HttpClient instances directly can cause socket exhaustion under load.");
        }

        var client = _httpClientFactory.CreateClient($"{ProviderName}FunctionClient");
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

        var effectiveApiKey = apiKey ?? _credential.ApiKey;
        if (!string.IsNullOrWhiteSpace(effectiveApiKey))
        {
            ApplyAuthHeader(client, effectiveApiKey);
        }

        // Default timeout (can be overridden by configuration)
        client.Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds ?? 30);
    }
}
