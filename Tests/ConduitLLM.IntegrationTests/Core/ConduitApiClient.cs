using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.IntegrationTests.Core;

public class ConduitApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ConduitApiClient> _logger;
    private readonly TestConfiguration _config;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public ConduitApiClient(TestConfiguration config, ILogger<ConduitApiClient> logger)
    {
        _config = config;
        _logger = logger;
        _httpClient = new HttpClient
        {
            // Use chat timeout as it's the longest operation we'll perform
            Timeout = TimeSpan.FromSeconds(Math.Max(config.Environment.Timeouts.Default, config.Environment.Timeouts.Chat))
        };
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
    
    // Admin API Methods
    
    public async Task<ApiResponse<T>> AdminGetAsync<T>(string endpoint)
    {
        return await ExecuteAsync<T>(HttpMethod.Get, _config.Environment.AdminApiUrl, endpoint, null, true);
    }
    
    public async Task<ApiResponse<T>> AdminPostAsync<T>(string endpoint, object? payload = null)
    {
        return await ExecuteAsync<T>(HttpMethod.Post, _config.Environment.AdminApiUrl, endpoint, payload, true);
    }
    
    public async Task<ApiResponse<T>> AdminPutAsync<T>(string endpoint, object? payload = null)
    {
        return await ExecuteAsync<T>(HttpMethod.Put, _config.Environment.AdminApiUrl, endpoint, payload, true);
    }

    /// <summary>
    /// Sends a PATCH to the Admin API. Virtual keys, virtual key groups, and IP filters
    /// require an If-Match header; "*" matches any current version.
    /// </summary>
    public async Task<ApiResponse<T>> AdminPatchAsync<T>(string endpoint, object? payload = null, string? ifMatch = null)
    {
        return await ExecuteAsync<T>(HttpMethod.Patch, _config.Environment.AdminApiUrl, endpoint, payload, true, ifMatch: ifMatch);
    }
    
    public async Task<ApiResponse<T>> AdminDeleteAsync<T>(string endpoint)
    {
        return await ExecuteAsync<T>(HttpMethod.Delete, _config.Environment.AdminApiUrl, endpoint, null, true);
    }
    
    // Gateway API Methods
    
    public async Task<ApiResponse<T>> CoreGetAsync<T>(string endpoint, string? virtualKey = null)
    {
        return await ExecuteAsync<T>(HttpMethod.Get, _config.Environment.CoreApiUrl, endpoint, null, false, virtualKey);
    }
    
    public async Task<ApiResponse<T>> CorePostAsync<T>(string endpoint, object? payload = null, string? virtualKey = null)
    {
        return await ExecuteAsync<T>(HttpMethod.Post, _config.Environment.CoreApiUrl, endpoint, payload, false, virtualKey);
    }
    
    // Generic execution method
    private async Task<ApiResponse<T>> ExecuteAsync<T>(
        HttpMethod method,
        string baseUrl,
        string endpoint,
        object? payload,
        bool isAdmin,
        string? virtualKey = null,
        string? ifMatch = null)
    {
        var url = $"{baseUrl}{endpoint}";
        var request = new HttpRequestMessage(method, url);

        if (ifMatch != null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }
        
        // Add authentication
        if (isAdmin)
        {
            var adminKey = _config.Environment.AdminApiKey 
                ?? ConfigurationLoader.GetAdminApiKeyFromDockerCompose()
                ?? Environment.GetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY");
                
            if (string.IsNullOrEmpty(adminKey))
            {
                throw new InvalidOperationException("Admin API key not configured. Check test-config.yaml or docker-compose.dev.yml");
            }
            
            request.Headers.Add("X-API-Key", adminKey);
        }
        else if (!string.IsNullOrEmpty(virtualKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", virtualKey);
        }
        
        // Add payload if present
        if (payload != null)
        {
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            _logger.LogDebug("Request payload: {Payload}", json);
        }
        
        try
        {
            _logger.LogInformation("{Method} {Url}", method, url);
            var response = await _httpClient.SendAsync(request);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Response ({StatusCode}): {Content}", response.StatusCode, responseContent);
            
            if (response.IsSuccessStatusCode)
            {
                if (string.IsNullOrWhiteSpace(responseContent))
                {
                    return new ApiResponse<T>
                    {
                        Success = true,
                        StatusCode = (int)response.StatusCode,
                        Data = default
                    };
                }
                
                var data = JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
                return new ApiResponse<T>
                {
                    Success = true,
                    StatusCode = (int)response.StatusCode,
                    Data = data
                };
            }
            else
            {
                _logger.LogError("Request failed with status {StatusCode}: {Response}", response.StatusCode, responseContent);
                Console.WriteLine($"API Error: {response.StatusCode} - {responseContent}");
                return new ApiResponse<T>
                {
                    Success = false,
                    StatusCode = (int)response.StatusCode,
                    Error = string.IsNullOrWhiteSpace(responseContent) ? $"HTTP {response.StatusCode}" : responseContent,
                    Data = default
                };
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("Request timeout for {Method} {Url}", method, url);
            return new ApiResponse<T>
            {
                Success = false,
                Error = "Request timeout",
                Data = default
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing {Method} {Url}", method, url);
            return new ApiResponse<T>
            {
                Success = false,
                Error = ex.Message,
                Data = default
            };
        }
    }
    
    public void SetTimeout(TimeSpan timeout)
    {
        _httpClient.Timeout = timeout;
    }

    /// <summary>
    /// Generic POST method for Admin API (convenience wrapper around AdminPostAsync).
    /// Returns the response data directly, throwing on failure.
    /// </summary>
    public async Task<T> PostAsync<T>(string endpoint, object? payload = null)
    {
        var response = await AdminPostAsync<T>(endpoint, payload);
        if (!response.Success)
        {
            throw new HttpRequestException($"POST {endpoint} failed: {response.Error}");
        }
        return response.Data!;
    }

    /// <summary>
    /// Send a raw HttpRequestMessage for streaming scenarios.
    /// The request URL should be relative to the Core API URL.
    /// </summary>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption)
    {
        // Ensure the request URL is absolute, using Core API as base
        if (!request.RequestUri?.IsAbsoluteUri ?? true)
        {
            var relativePath = request.RequestUri?.ToString() ?? "";
            request.RequestUri = new Uri($"{_config.Environment.CoreApiUrl}{relativePath}");
        }

        _logger.LogInformation("{Method} {Url}", request.Method, request.RequestUri);
        return await _httpClient.SendAsync(request, completionOption);
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
}

// DTOs moved to ApiDtos.cs for better organization