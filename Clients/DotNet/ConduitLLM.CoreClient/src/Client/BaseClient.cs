using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ConduitLLM.CoreClient.Constants;

namespace ConduitLLM.CoreClient.Client;

/// <summary>
/// Base class for Core API client operations providing HTTP communication functionality.
/// </summary>
public abstract class BaseClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;
    private readonly ConduitCoreClientConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the BaseClient class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="configuration">The client configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    protected BaseClient(
        HttpClient httpClient,
        ConduitCoreClientConfiguration configuration,
        ILogger? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;

        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        ConfigureHttpClient();
    }

    /// <summary>
    /// Gets the HTTP client instance.
    /// </summary>
    protected HttpClient HttpClient => _httpClient;

    /// <summary>
    /// Gets the JSON serializer options.
    /// </summary>
    protected JsonSerializerOptions JsonSerializerOptions => _jsonSerializerOptions;

    /// <summary>
    /// Gets the HTTP client instance for services.
    /// </summary>
    public HttpClient HttpClientForServices => _httpClient;

    /// <summary>
    /// Gets the JSON serializer options for services.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptionsForServices => _jsonSerializerOptions;

    /// <summary>
    /// Gets the client configuration.
    /// </summary>
    public ConduitCoreClientConfiguration Configuration => _configuration;

    /// <summary>
    /// Performs a GET request to the specified endpoint.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response to.</typeparam>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="parameters">Optional query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    protected async Task<T> GetAsync<T>(
        string endpoint,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(endpoint, parameters);
        
        _logger?.LogDebug("GET request to {Url}", url);
        
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadFromJsonAsync<T>(_jsonSerializerOptions, cancellationToken);
        return content ?? throw new InvalidOperationException("Response content was null");
    }

    /// <summary>
    /// Performs a POST request to the specified endpoint.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response to.</typeparam>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="data">The data to send in the request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    protected async Task<T> PostAsync<T>(
        string endpoint,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("POST request to {Endpoint}", endpoint);
        
        var response = await _httpClient.PostAsJsonAsync(endpoint, data, _jsonSerializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadFromJsonAsync<T>(_jsonSerializerOptions, cancellationToken);
        return content ?? throw new InvalidOperationException("Response content was null");
    }

    /// <summary>
    /// Performs a POST request with streaming response.
    /// </summary>
    /// <typeparam name="T">The type to deserialize each chunk to.</typeparam>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="data">The data to send in the request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of response chunks.</returns>
    protected async IAsyncEnumerable<T> PostStreamAsync<T>(
        string endpoint,
        object? data = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("POST streaming request to {Endpoint}", endpoint);
        
        var response = await _httpClient.PostAsJsonAsync(endpoint, data, _jsonSerializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
                
            if (string.IsNullOrWhiteSpace(line))
                continue;
                
            // Handle Server-Sent Events format
            if (line.StartsWith("data: "))
            {
                var jsonData = line.Substring(6);
                
                if (jsonData == "[DONE]")
                    yield break;
                
                T? chunk;
                try
                {
                    chunk = JsonSerializer.Deserialize<T>(jsonData, _jsonSerializerOptions);
                }
                catch (JsonException ex)
                {
                    _logger?.LogWarning(ex, "Failed to deserialize streaming chunk: {JsonData}", jsonData);
                    continue;
                }
                
                if (chunk != null)
                    yield return chunk;
            }
        }
    }

    /// <summary>
    /// Performs a GET request to the specified endpoint (public for services).
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response to.</typeparam>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="parameters">Optional query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    public async Task<T> GetForServiceAsync<T>(
        string endpoint,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync<T>(endpoint, parameters, cancellationToken);
    }

    /// <summary>
    /// Performs a POST request to the specified endpoint (public for services).
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response to.</typeparam>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="data">The data to send in the request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    public async Task<T> PostForServiceAsync<T>(
        string endpoint,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        return await PostAsync<T>(endpoint, data, cancellationToken);
    }

    /// <summary>
    /// Performs a POST request with streaming response (public for services).
    /// </summary>
    /// <typeparam name="T">The type to deserialize each chunk to.</typeparam>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="data">The data to send in the request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of response chunks.</returns>
    public async IAsyncEnumerable<T> PostStreamForServiceAsync<T>(
        string endpoint,
        object? data = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in PostStreamAsync<T>(endpoint, data, cancellationToken))
        {
            yield return chunk;
        }
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_configuration.BaseUrl.TrimEnd('/'));
        _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds);
        
        // Add authorization header
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _configuration.ApiKey);
        
        // Add organization header if specified
        if (!string.IsNullOrEmpty(_configuration.OrganizationId))
        {
            _httpClient.DefaultRequestHeaders.Add(HttpHeaders.OpenAIOrganization, _configuration.OrganizationId);
        }
        
        // Add default headers
        foreach (var header in _configuration.DefaultHeaders)
        {
            _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
        }
        
        // Add user agent
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgents.CoreClient);
    }

    private string BuildUrl(string endpoint, object? parameters)
    {
        if (parameters == null)
        {
            return endpoint;
        }

        var queryString = BuildQueryString(parameters);
        return string.IsNullOrEmpty(queryString) ? endpoint : $"{endpoint}?{queryString}";
    }

    private string BuildQueryString(object parameters)
    {
        var properties = parameters.GetType().GetProperties();
        var queryParams = new List<string>();

        foreach (var property in properties)
        {
            var value = property.GetValue(parameters);
            if (value != null)
            {
                var key = property.Name;
                var stringValue = value.ToString();
                if (!string.IsNullOrEmpty(stringValue))
                {
                    queryParams.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(stringValue)}");
                }
            }
        }

        return string.Join("&", queryParams);
    }

    /// <summary>
    /// Disposes the HTTP client and other resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    /// <param name="disposing">Whether to dispose managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}