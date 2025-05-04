using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.InternalModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers;

/// <summary>
/// Client for interacting with HuggingFace Inference API.
/// </summary>
public class HuggingFaceClient : ILLMClient
{
    private readonly HttpClient _httpClient;
    private readonly ProviderCredentials _credentials;
    private readonly string _modelId;
    private readonly ILogger<HuggingFaceClient> _logger;

    // Constants
    private const string DefaultApiBase = "https://api-inference.huggingface.co/models/";

    public HuggingFaceClient(
        ProviderCredentials credentials,
        string modelId,
        ILogger<HuggingFaceClient> logger,
        HttpClient? httpClient = null)
    {
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _modelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            throw new ConfigurationException("API key is missing for HuggingFace Inference API provider.");
        }
        
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.ApiKey);
    }

    /// <inheritdoc />
    public async Task<ChatCompletionResponse> CreateChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        _logger.LogInformation("Creating chat completion with HuggingFace Inference API for model {Model}", _modelId);
        
        // Update API key if provided
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
        
        try
        {
            // Determine endpoint based on model type
            string apiEndpoint = GetHuggingFaceEndpoint(_modelId);
            
            // Different models require different input formats
            // For simplicity, we'll convert messages to a text prompt
            string formattedPrompt = FormatChatMessages(request.Messages);
            
            // Create request based on model type
            var hfRequest = new HuggingFaceTextGenerationRequest
            {
                Inputs = formattedPrompt,
                Parameters = new HuggingFaceParameters
                {
                    MaxNewTokens = request.MaxTokens,
                    Temperature = request.Temperature,
                    TopP = request.TopP,
                    DoSample = true,
                    ReturnFullText = false
                },
                Options = new HuggingFaceOptions
                {
                    WaitForModel = true
                }
            };
            
            // Send request
            using var response = await _httpClient.PostAsJsonAsync(
                apiEndpoint, 
                hfRequest,
                new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull },
                cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("HuggingFace Inference API request failed with status code {StatusCode}. Response: {ErrorContent}",
                    response.StatusCode, errorContent);
                throw new LLMCommunicationException(
                    $"HuggingFace Inference API request failed with status code {response.StatusCode}. Response: {errorContent}");
            }
            
            // Parse response
            string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
            
            if (contentType.Contains("json"))
            {
                return await ProcessJsonResponseAsync(response, request.Model, cancellationToken);
            }
            else
            {
                // Handle plain text response
                string textResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                return CreateChatCompletionResponse(request.Model, textResponse);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error communicating with HuggingFace Inference API");
            throw new LLMCommunicationException($"HTTP request error communicating with HuggingFace Inference API: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization error processing HuggingFace Inference API response");
            throw new LLMCommunicationException("Error deserializing HuggingFace Inference API response", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning(ex, "HuggingFace Inference API request timed out");
            throw new LLMCommunicationException("HuggingFace Inference API request timed out", ex);
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(ex, "HuggingFace Inference API request was canceled");
            throw; // Re-throw cancellation
        }
        catch (Exception ex) when (ex is not ConfigurationException && ex is not LLMCommunicationException)
        {
            _logger.LogError(ex, "An unexpected error occurred while processing HuggingFace Inference API chat completion");
            throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        _logger.LogInformation("Streaming is not natively supported in HuggingFace Inference API client. Simulating streaming.");
        
        // HuggingFace Inference API doesn't support streaming directly
        // Simulate streaming by breaking up the response
        var fullResponse = await CreateChatCompletionAsync(request, apiKey, cancellationToken);
        
        if (fullResponse.Choices == null || !fullResponse.Choices.Any() ||
            fullResponse.Choices[0].Message?.Content == null)
        {
            yield break;
        }
        
        // Simulate streaming by breaking up the content
        string content = ContentHelper.GetContentAsString(fullResponse.Choices[0].Message!.Content);
        
        // Generate a random ID for this streaming session
        string streamId = Guid.NewGuid().ToString();
        
        // Initial chunk with role
        yield return new ChatCompletionChunk
        {
            Id = streamId,
            Object = "chat.completion.chunk",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = request.Model,
            Choices = new List<StreamingChoice>
            {
                new StreamingChoice
                {
                    Index = 0,
                    Delta = new DeltaContent
                    {
                        Role = "assistant",
                        Content = null
                    }
                }
            }
        };
        
        // Break content into chunks (words or sentences could be used)
        var words = content.Split(' ');
        
        // Send content in chunks
        StringBuilder currentChunk = new StringBuilder();
        foreach (var word in words)
        {
            // Add delay to simulate real streaming
            await Task.Delay(25, cancellationToken);
            
            currentChunk.Append(word).Append(' ');
            
            // Send every few words
            if (currentChunk.Length > 0)
            {
                yield return new ChatCompletionChunk
                {
                    Id = streamId,
                    Object = "chat.completion.chunk",
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Model = request.Model,
                    Choices = new List<StreamingChoice>
                    {
                        new StreamingChoice
                        {
                            Index = 0,
                            Delta = new DeltaContent
                            {
                                Content = currentChunk.ToString()
                            }
                        }
                    }
                };
                
                currentChunk.Clear();
            }
            
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }
        }
        
        // Final chunk with finish reason
        yield return new ChatCompletionChunk
        {
            Id = streamId,
            Object = "chat.completion.chunk",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = request.Model,
            Choices = new List<StreamingChoice>
            {
                new StreamingChoice
                {
                    Index = 0,
                    Delta = new DeltaContent(),
                    FinishReason = fullResponse.Choices[0].FinishReason
                }
            }
        };
    }

    /// <inheritdoc />
    public async Task<List<string>> ListModelsAsync(string? apiKey = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("HuggingFace Inference API does not provide a model listing endpoint. Returning commonly used models.");
        
        // HuggingFace Inference API doesn't have an endpoint to list models
        // Return a list of popular models as an example
        await Task.Delay(1, cancellationToken); // Adding await to make this truly async
        
        return new List<string>
        {
            "gpt2",
            "mistralai/Mistral-7B-Instruct-v0.2",
            "meta-llama/Llama-2-7b-chat-hf",
            "facebook/bart-large-cnn",
            "google/flan-t5-xl",
            "EleutherAI/gpt-neox-20b",
            "bigscience/bloom",
            "microsoft/DialoGPT-large",
            "sentence-transformers/all-MiniLM-L6-v2",
            "tiiuae/falcon-7b-instruct"
        };
    }

    public Task<EmbeddingResponse> CreateEmbeddingAsync(EmbeddingRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
        => Task.FromException<EmbeddingResponse>(new NotSupportedException("Embeddings are not supported by HuggingFaceClient."));

    public Task<ImageGenerationResponse> CreateImageAsync(ImageGenerationRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
        => Task.FromException<ImageGenerationResponse>(new NotSupportedException("Image generation is not supported by HuggingFaceClient."));
    
    #region Helper Methods
    
    private string GetHuggingFaceEndpoint(string modelId)
    {
        string apiBase = !string.IsNullOrWhiteSpace(_credentials.ApiBase)
            ? _credentials.ApiBase.TrimEnd('/')
            : DefaultApiBase.TrimEnd('/');
            
        return $"{apiBase}/{modelId}";
    }
    
    private string FormatChatMessages(List<Message> messages)
    {
        // Different models expect different formats
        // For simplicity, we'll use a generic chat format here
        // In a real implementation, you would adapt this based on the model
        
        StringBuilder formattedChat = new StringBuilder();
        
        // Extract system message if present
        var systemMessage = messages.FirstOrDefault(m => 
            m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));
            
        if (systemMessage != null)
        {
            formattedChat.AppendLine(ContentHelper.GetContentAsString(systemMessage.Content));
            formattedChat.AppendLine();
        }
        
        // Format the conversation
        foreach (var message in messages.Where(m => 
            !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)))
        {
            string role = message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                ? "Assistant"
                : "Human";
                
            formattedChat.AppendLine($"{role}: {ContentHelper.GetContentAsString(message.Content)}");
        }
        
        // Add final prompt for assistant
        formattedChat.Append("Assistant: ");
        
        return formattedChat.ToString();
    }
    
    private async Task<ChatCompletionResponse> ProcessJsonResponseAsync(
        HttpResponseMessage response,
        string originalModelAlias,
        CancellationToken cancellationToken)
    {
        try
        {
            // Try to parse as array first (some HF models return array of results)
            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            if (jsonContent.TrimStart().StartsWith("["))
            {
                // Parse as array
                var arrayResponse = JsonSerializer.Deserialize<List<HuggingFaceTextGenerationResponse>>(
                    jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                if (arrayResponse != null && arrayResponse.Count > 0)
                {
                    return CreateChatCompletionResponse(originalModelAlias, arrayResponse[0].GeneratedText ?? string.Empty);
                }
            }
            else
            {
                // Parse as single object
                var objectResponse = JsonSerializer.Deserialize<HuggingFaceTextGenerationResponse>(
                    jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                if (objectResponse != null)
                {
                    return CreateChatCompletionResponse(originalModelAlias, objectResponse.GeneratedText ?? string.Empty);
                }
            }
            
            // If all fails, return error
            _logger.LogError("Could not parse HuggingFace response: {Content}", jsonContent);
            throw new LLMCommunicationException("Invalid response format from HuggingFace Inference API");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing JSON response from HuggingFace");
            throw new LLMCommunicationException("Error parsing JSON response from HuggingFace Inference API", ex);
        }
    }
    
    private ChatCompletionResponse CreateChatCompletionResponse(string model, string content)
    {
        // Estimate token counts based on text length
        int estimatedPromptTokens = EstimateTokenCount(content);
        int estimatedCompletionTokens = EstimateTokenCount(content);
        
        return new ChatCompletionResponse
        {
            Id = Guid.NewGuid().ToString(),
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = model,
            Choices = new List<Choice>
            {
                new Choice
                {
                    Index = 0,
                    Message = new Message
                    {
                        Role = "assistant",
                        Content = content
                    },
                    FinishReason = "stop"
                }
            },
            Usage = new Usage
            {
                // HuggingFace doesn't provide token usage
                // Provide estimated counts based on text length
                PromptTokens = estimatedPromptTokens,
                CompletionTokens = estimatedCompletionTokens,
                TotalTokens = estimatedPromptTokens + estimatedCompletionTokens
            }
        };
    }
    
    private int EstimateTokenCount(string text)
    {
        // Rough token count estimation
        if (string.IsNullOrEmpty(text))
            return 0;
            
        // Approximately 4 characters per token for English text
        // This is a rough estimate that works for many models but isn't exact
        return Math.Max(1, text.Length / 4);
    }
    
    #endregion
}