using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ConduitLLM.Core.Models;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Client for consuming the Conduit HTTP API
/// </summary>
public class ConduitApiClient : IConduitApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ConduitApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConduitApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HttpClient instance for making API requests.</param>
    /// <param name="configuration">Configuration to read API settings.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public ConduitApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ConduitApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Note: HttpClient BaseAddress is configured in Program.cs via dependency injection
        // Don't override it here as it may be different for local dev vs containerized deployment
        
        // Get the admin API key to use for requests
        string adminApiKey = configuration["ApiClient:AdminApiKey"] ?? "";
        if (!string.IsNullOrEmpty(adminApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminApiKey);
        }

        // Configure JSON serialization options for snake_case (OpenAI format)
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Gets the list of available models from the API.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of model identifiers.</returns>
    public async Task<List<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ModelsResponse>("/v1/models", _jsonOptions, cancellationToken);

            if (response?.Data == null)
            {
                return new List<string>();
            }

            return response.Data.Select(m => m.Id).ToList();
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
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/v1/chat/completions", 
                request, 
                _jsonOptions, 
                cancellationToken);

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
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/v1/embeddings", 
                request, 
                _jsonOptions, 
                cancellationToken);

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
    /// <returns>An async enumerable of chat completion chunks.</returns>
    public async IAsyncEnumerable<ChatCompletionChunk> CreateStreamingChatCompletionAsync(
        ChatCompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating streaming chat completion for model: {Model}", request.Model);

        // Ensure streaming is enabled
        request.Stream = true;
        
        var response = await _httpClient.PostAsJsonAsync("/v1/chat/completions", request, _jsonOptions, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Streaming chat completion request failed with status: {StatusCode}, Content: {Content}", 
                response.StatusCode, errorContent);
            response.Dispose();
            yield break;
        }
        
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        using (response)
        {
            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                
                if (string.IsNullOrEmpty(line) || !line.StartsWith("data: "))
                    continue;
                    
                var jsonData = line.Substring(6); // Remove "data: " prefix
                
                if (jsonData == "[DONE]")
                    break;
                    
                ChatCompletionChunk? chunk = null;
                if (TryDeserializeChunk(jsonData, out chunk) && chunk != null)
                {
                    yield return chunk;
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