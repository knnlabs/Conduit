using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.AdminClient.Client;

/// <summary>
/// Base class for API client operations providing HTTP communication and caching functionality.
/// </summary>
public abstract class BaseApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    protected readonly ILogger? _logger;
    private readonly IMemoryCache? _cache;
    private readonly ConduitAdminClientConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private bool _disposed;

    /// <summary>
    /// Gets the HTTP client instance for advanced operations.
    /// </summary>
    protected HttpClient HttpClient => _httpClient;

    /// <summary>
    /// Initializes a new instance of the BaseApiClient class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="configuration">The client configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="cache">Optional memory cache instance.</param>
    protected BaseApiClient(
        HttpClient httpClient,
        ConduitAdminClientConfiguration configuration,
        ILogger? logger = null,
        IMemoryCache? cache = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
        _cache = cache;

        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        ConfigureHttpClient();
    }

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
    /// Performs a POST request to the specified endpoint without expecting a response body.
    /// </summary>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="data">The data to send in the request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task PostAsync(
        string endpoint,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("POST request to {Endpoint}", endpoint);
        
        var response = await _httpClient.PostAsJsonAsync(endpoint, data, _jsonSerializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Performs a PUT request to the specified endpoint.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response to.</typeparam>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="data">The data to send in the request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    protected async Task<T> PutAsync<T>(
        string endpoint,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("PUT request to {Endpoint}", endpoint);
        
        var response = await _httpClient.PutAsJsonAsync(endpoint, data, _jsonSerializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadFromJsonAsync<T>(_jsonSerializerOptions, cancellationToken);
        return content ?? throw new InvalidOperationException("Response content was null");
    }

    /// <summary>
    /// Performs a PUT request to the specified endpoint without expecting a response body.
    /// </summary>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="data">The data to send in the request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task PutAsync(
        string endpoint,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("PUT request to {Endpoint}", endpoint);
        
        var response = await _httpClient.PutAsJsonAsync(endpoint, data, _jsonSerializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Performs a DELETE request to the specified endpoint.
    /// </summary>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task DeleteAsync(
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("DELETE request to {Endpoint}", endpoint);
        
        var response = await _httpClient.DeleteAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Executes a function with caching support.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="factory">The function to execute if not cached.</param>
    /// <param name="expiration">Cache expiration time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached or computed result.</returns>
    protected async Task<T> WithCacheAsync<T>(
        string cacheKey,
        Func<Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        if (!_configuration.EnableCaching || _cache == null)
        {
            return await factory();
        }

        if (_cache.TryGetValue(cacheKey, out T? cachedValue) && cachedValue != null)
        {
            _logger?.LogDebug("Cache hit for key {CacheKey}", cacheKey);
            return cachedValue;
        }

        _logger?.LogDebug("Cache miss for key {CacheKey}", cacheKey);
        var result = await factory();
        
        var cacheExpiration = expiration ?? TimeSpan.FromSeconds(_configuration.CacheTimeoutSeconds);
        _cache.Set(cacheKey, result, cacheExpiration);
        
        return result;
    }

    /// <summary>
    /// Invalidates cached data for the specified key.
    /// </summary>
    /// <param name="cacheKey">The cache key to invalidate.</param>
    protected void InvalidateCache(string cacheKey)
    {
        _cache?.Remove(cacheKey);
        _logger?.LogDebug("Invalidated cache for key {CacheKey}", cacheKey);
    }

    /// <summary>
    /// Generates a cache key from the provided parameters.
    /// </summary>
    /// <param name="prefix">The cache key prefix.</param>
    /// <param name="parameters">Parameters to include in the key.</param>
    /// <returns>A cache key string.</returns>
    protected string GetCacheKey(string prefix, params object[] parameters)
    {
        var key = new StringBuilder(prefix);
        foreach (var param in parameters)
        {
            key.Append(':').Append(param?.ToString() ?? "null");
        }
        return key.ToString();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(ConduitAdminClientConfiguration.NormalizeApiUrl(_configuration.AdminApiUrl));
        _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds);
        
        // Add master key header
        _httpClient.DefaultRequestHeaders.Add("X-Master-Key", _configuration.MasterKey);
        
        // Add default headers
        foreach (var header in _configuration.DefaultHeaders)
        {
            _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
        }
        
        // Add user agent
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ConduitLLM.AdminClient/1.0.0");
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