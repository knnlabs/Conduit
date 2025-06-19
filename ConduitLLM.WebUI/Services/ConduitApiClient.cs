using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using ConduitLLM.Core.Models;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Models;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Client for consuming the Conduit HTTP API
/// </summary>
public class ConduitApiClient : IConduitApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ConduitApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IServiceProvider _serviceProvider;
    private string? _webUIVirtualKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConduitApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HttpClient instance for making API requests.</param>
    /// <param name="configuration">Configuration to read API settings.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="serviceProvider">Service provider for accessing other services.</param>
    public ConduitApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ConduitApiClient> logger,
        IServiceProvider serviceProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Note: HttpClient BaseAddress is configured in Program.cs via dependency injection
        // Don't override it here as it may be different for local dev vs containerized deployment

        // Configure JSON serialization options for snake_case (OpenAI format)
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Gets the WebUI virtual key from settings if not already loaded
    /// </summary>
    private async Task<string?> GetWebUIVirtualKeyAsync()
    {
        if (_webUIVirtualKey != null)
        {
            _logger.LogDebug("Using cached WebUI virtual key");
            return _webUIVirtualKey;
        }

        try
        {
            _logger.LogDebug("Loading WebUI virtual key from settings...");
            using var scope = _serviceProvider.CreateScope();
            var globalSettingService = scope.ServiceProvider.GetRequiredService<IGlobalSettingService>();
            _webUIVirtualKey = await globalSettingService.GetSettingAsync("WebUI_VirtualKey");
            
            if (!string.IsNullOrEmpty(_webUIVirtualKey))
            {
                _logger.LogInformation("Loaded WebUI virtual key from settings (length: {Length})", _webUIVirtualKey.Length);
            }
            else
            {
                _logger.LogWarning("No WebUI virtual key found in settings");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading WebUI virtual key");
        }

        return _webUIVirtualKey;
    }

    /// <summary>
    /// Gets the list of available models from the API.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of model identifiers.</returns>
    public async Task<List<string>> GetAvailableModelsAsync(string? virtualKey = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use provided key or fall back to WebUI virtual key
            var keyToUse = virtualKey ?? await GetWebUIVirtualKeyAsync();
            
            using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/models");
            if (!string.IsNullOrEmpty(keyToUse))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", keyToUse);
            }
            
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var modelsResponse = JsonSerializer.Deserialize<ModelsResponse>(responseBody, _jsonOptions);

            if (modelsResponse?.Data == null)
            {
                return new List<string>();
            }

            return modelsResponse.Data.Select(m => m.Id).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving models from API");
            return new List<string>();
        }
    }

    /// <summary>
    /// Creates a chat completion request to the API.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The chat completion response.</returns>
    public async Task<ChatCompletionResponse?> CreateChatCompletionAsync(
        ChatCompletionRequest request,
        string? virtualKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use provided key or fall back to WebUI virtual key
            var keyToUse = virtualKey ?? await GetWebUIVirtualKeyAsync();
            
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions");
            if (!string.IsNullOrEmpty(keyToUse))
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", keyToUse);
            }
            
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
            
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(_jsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating chat completion for model {Model}", request.Model);
            return null;
        }
    }

    /// <summary>
    /// Creates an embedding request to the API.
    /// </summary>
    /// <param name="request">The embedding request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The embedding response.</returns>
    public async Task<EmbeddingResponse?> CreateEmbeddingAsync(
        EmbeddingRequest request,
        string? virtualKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use provided key or fall back to WebUI virtual key
            var keyToUse = virtualKey ?? await GetWebUIVirtualKeyAsync();
            
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/embeddings");
            if (!string.IsNullOrEmpty(keyToUse))
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", keyToUse);
            }
            
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
            
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotImplemented)
            {
                _logger.LogWarning("Embeddings endpoint not implemented on the API");
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<EmbeddingResponse>(_jsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating embeddings for model {Model}", request.Model);
            return null;
        }
    }

    /// <summary>
    /// Creates a streaming chat completion request to the API.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An async enumerable of streaming chat responses.</returns>
    public async IAsyncEnumerable<StreamingChatResponse> CreateStreamingChatCompletionAsync(
        ChatCompletionRequest request,
        string? virtualKey = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating streaming chat completion for model: {Model}", request.Model);

        // Ensure streaming is enabled
        request.Stream = true;

        // Use provided key or fall back to WebUI virtual key
        var keyToUse = virtualKey ?? await GetWebUIVirtualKeyAsync();
        
        if (!string.IsNullOrEmpty(keyToUse))
        {
            _logger.LogDebug("Using API key for authentication (length: {Length})", keyToUse.Length);
        }
        else
        {
            _logger.LogWarning("No API key available for authentication");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions");
        if (!string.IsNullOrEmpty(keyToUse))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", keyToUse);
        }
        
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Streaming chat completion request failed with status: {StatusCode}, Content: {Content}",
                response.StatusCode, errorContent);
            response.Dispose();
            yield break;
        }

        // Extract request ID if available
        var requestId = response.Headers.TryGetValues("X-Request-ID", out var values) ? values.FirstOrDefault() : null;
        if (!string.IsNullOrEmpty(requestId))
        {
            _logger.LogDebug("Streaming request ID: {RequestId}", requestId);
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        using (response)
        {
            string? currentEventType = null;
            var dataBuffer = new List<string>();

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);

                if (line == null)
                    break;

                // Handle event type
                if (line.StartsWith("event: "))
                {
                    currentEventType = line.Substring(7);
                    continue;
                }

                // Handle data
                if (line.StartsWith("data: "))
                {
                    dataBuffer.Add(line.Substring(6));
                    continue;
                }

                // Empty line means end of event
                if (string.IsNullOrEmpty(line) && dataBuffer.Count > 0)
                {
                    var jsonData = string.Join("\n", dataBuffer);
                    dataBuffer.Clear();

                    // Handle different event types
                    if (currentEventType == "content" || string.IsNullOrEmpty(currentEventType))
                    {
                        if (jsonData == "[DONE]")
                        {
                            yield return new StreamingChatResponse { EventType = "done" };
                            break;
                        }

                        ChatCompletionChunk? chunk = null;
                        if (TryDeserializeChunk(jsonData, out chunk) && chunk != null)
                        {
                            yield return new StreamingChatResponse 
                            { 
                                EventType = "content", 
                                Chunk = chunk 
                            };
                        }
                    }
                    else if (currentEventType == "metrics" || currentEventType == "metrics-final")
                    {
                        _logger.LogDebug("Received {EventType}: {Data}", currentEventType, jsonData);
                        
                        PerformanceMetrics? metrics = null;
                        if (TryDeserializeMetrics(jsonData, out metrics) && metrics != null)
                        {
                            yield return new StreamingChatResponse 
                            { 
                                EventType = currentEventType, 
                                Metrics = metrics 
                            };
                        }
                    }
                    else if (currentEventType == "done")
                    {
                        yield return new StreamingChatResponse { EventType = "done" };
                        break;
                    }
                    else if (currentEventType == "error")
                    {
                        _logger.LogError("Streaming error event: {Data}", jsonData);
                        yield return new StreamingChatResponse 
                        { 
                            EventType = "error", 
                            Error = jsonData 
                        };
                        break;
                    }

                    currentEventType = null;
                }
            }
        }
    }

    private bool TryDeserializeChunk(string jsonData, out ChatCompletionChunk? chunk)
    {
        try
        {
            chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(jsonData, _jsonOptions);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing stream chunk: {JsonData}", jsonData);
            chunk = null;
            return false;
        }
    }

    private bool TryDeserializeMetrics(string jsonData, out PerformanceMetrics? metrics)
    {
        try
        {
            metrics = JsonSerializer.Deserialize<PerformanceMetrics>(jsonData, _jsonOptions);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing metrics: {JsonData}", jsonData);
            metrics = null;
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<ImageGenerationResponse?> CreateImageAsync(
        ImageGenerationRequest request,
        string? virtualKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Prepare the request
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/v1/images/generations");
            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Use provided virtual key or get WebUI key
            var apiKey = virtualKey ?? await GetWebUIVirtualKeyAsync();
            if (!string.IsNullOrEmpty(apiKey))
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            // Add request body
            var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);
            requestMessage.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Send request
            var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<ImageGenerationResponse>(responseContent, _jsonOptions);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Image generation request failed with status {StatusCode}: {Error}", 
                    response.StatusCode, errorContent);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating image");
            throw;
        }
    }

    // Helper class for models response
    private class ModelsResponse
    {
        public List<ModelInfo> Data { get; set; } = new();
        public string Object { get; set; } = "list";
    }

    // Helper class for model info
    private class ModelInfo
    {
        public string Id { get; set; } = "";
        public string Object { get; set; } = "model";
    }
}
