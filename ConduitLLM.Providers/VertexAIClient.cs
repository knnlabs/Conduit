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
using ConduitLLM.Providers.InternalModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers;

/// <summary>
/// Client for interacting with Google Vertex AI API.
/// </summary>
public class VertexAIClient : ILLMClient
{
    private readonly HttpClient _httpClient;
    private readonly ProviderCredentials _credentials;
    private readonly string _modelAlias;
    private readonly ILogger<VertexAIClient> _logger;

    // Constants
    private const string DefaultRegion = "us-central1";
    private const string DefaultApiVersion = "v1";

    public VertexAIClient(
        ProviderCredentials credentials,
        string modelAlias,
        ILogger<VertexAIClient> logger,
        HttpClient? httpClient = null)
    {
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _modelAlias = modelAlias ?? throw new ArgumentNullException(nameof(modelAlias));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            throw new ConfigurationException("API key is missing for Google Vertex AI provider.");
        }

        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
    }

    /// <inheritdoc />
    public async Task<ChatCompletionResponse> CreateChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        _logger.LogInformation("Creating chat completion with Google Vertex AI for model {Model}", _modelAlias);
        
        // Determine the API key to use
        string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : _credentials.ApiKey!;
        
        try
        {
            // Get the model information
            var (modelId, modelType) = GetVertexAIModelInfo(_modelAlias);
            
            // Create the appropriate request based on model type
            string apiEndpoint = BuildVertexAIEndpoint(modelId, modelType);
            
            // Prepare the request based on model type
            HttpResponseMessage response;
            
            if (modelType.Equals("gemini", StringComparison.OrdinalIgnoreCase))
            {
                var geminiRequest = PrepareGeminiRequest(request);
                response = await SendGeminiRequestAsync(apiEndpoint, geminiRequest, effectiveApiKey, cancellationToken);
            }
            else if (modelType.Equals("palm", StringComparison.OrdinalIgnoreCase))
            {
                var palmRequest = PreparePaLMRequest(request);
                response = await SendPaLMRequestAsync(apiEndpoint, palmRequest, effectiveApiKey, cancellationToken);
            }
            else
            {
                throw new UnsupportedProviderException($"Unsupported Vertex AI model type: {modelType}");
            }
            
            // Process the response
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Google Vertex AI API request failed with status code {StatusCode}. Response: {ErrorContent}",
                    response.StatusCode, errorContent);
                throw new LLMCommunicationException(
                    $"Google Vertex AI API request failed with status code {response.StatusCode}. Response: {errorContent}");
            }
            
            // Deserialize based on model type
            if (modelType.Equals("gemini", StringComparison.OrdinalIgnoreCase))
            {
                return await ProcessGeminiResponseAsync(response, request.Model, cancellationToken);
            }
            else
            {
                return await ProcessPaLMResponseAsync(response, request.Model, cancellationToken);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error communicating with Google Vertex AI API");
            throw new LLMCommunicationException($"HTTP request error communicating with Google Vertex AI API: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization error processing Google Vertex AI response");
            throw new LLMCommunicationException("Error deserializing Google Vertex AI response", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning(ex, "Google Vertex AI API request timed out");
            throw new LLMCommunicationException("Google Vertex AI API request timed out", ex);
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(ex, "Google Vertex AI API request was canceled");
            throw; // Re-throw cancellation
        }
        catch (Exception ex) when (ex is not UnsupportedProviderException
                               && ex is not ConfigurationException
                               && ex is not LLMCommunicationException)
        {
            _logger.LogError(ex, "An unexpected error occurred while processing Google Vertex AI chat completion");
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
        _logger.LogInformation("Streaming is not natively supported in this Vertex AI client implementation. Simulating streaming.");

        // Use the non-streaming endpoint and simulate streaming per prediction/candidate
        ChatCompletionResponse fullResponse;
        try
        {
            fullResponse = await CreateChatCompletionAsync(request, apiKey, cancellationToken);
        }
        catch (LLMCommunicationException ex) when (ex.Message.Contains("or response is empty"))
        {
            // For streaming, treat empty response as empty stream, not exception
            yield break;
        }

        if (fullResponse.Choices == null || fullResponse.Choices.Count == 0)
        {
            yield break;
        }

        foreach (var choice in fullResponse.Choices)
        {
            yield return new ChatCompletionChunk
            {
                Id = Guid.NewGuid().ToString(),
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model,
                Choices = new List<StreamingChoice>
                {
                    new StreamingChoice
                    {
                        Index = choice.Index,
                        Delta = new DeltaContent
                        {
                            Role = choice.Message?.Role,
                            Content = choice.Message?.Content
                        },
                        FinishReason = choice.FinishReason
                    }
                }
            };
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> ListModelsAsync(string? apiKey = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing available models from Google Vertex AI");
        
        // Vertex AI doesn't have a simple API endpoint to list models via API key
        // Return hard-coded list of commonly available models
        await Task.Delay(1, cancellationToken); // Adding await to make this truly async
        
        return new List<string>
        {
            "gemini-1.0-pro",
            "gemini-1.0-pro-vision",
            "gemini-1.5-pro",
            "gemini-1.5-flash",
            "text-bison@002",
            "chat-bison@002",
            "text-unicorn@001",
            "embedding-gecko@001",
            "embedding-001"
        };
    }
    
    #region Helper Methods
    
    private (string ModelId, string ModelType) GetVertexAIModelInfo(string modelAlias)
    {
        // Map model aliases to actual Vertex AI model IDs and types
        return modelAlias.ToLowerInvariant() switch
        {
            // Gemini models
            "gemini-pro" or "gemini-1.0-pro" => ("gemini-1.0-pro", "gemini"),
            "gemini-pro-vision" or "gemini-1.0-pro-vision" => ("gemini-1.0-pro-vision", "gemini"),
            "gemini-1.5-pro" => ("gemini-1.5-pro", "gemini"),
            "gemini-1.5-flash" => ("gemini-1.5-flash", "gemini"),
            
            // PaLM models
            "text-bison" or "text-bison@002" => ("text-bison@002", "palm"),
            "chat-bison" or "chat-bison@002" => ("chat-bison@002", "palm"),
            "text-unicorn" or "text-unicorn@001" => ("text-unicorn@001", "palm"),
            
            // Default to the model alias itself and assume Gemini for newer models
            _ when modelAlias.StartsWith("gemini", StringComparison.OrdinalIgnoreCase) => (modelAlias, "gemini"),
            _ => (modelAlias, "palm")  // Default to PaLM for other models
        };
    }
    
    private string BuildVertexAIEndpoint(string modelId, string modelType)
    {
        // Determine API version and region
        string apiVersion = !string.IsNullOrWhiteSpace(_credentials.ApiVersion) 
            ? _credentials.ApiVersion 
            : DefaultApiVersion;
            
        string region = !string.IsNullOrWhiteSpace(_credentials.ApiBase)
            ? _credentials.ApiBase
            : DefaultRegion;
            
        // Form the endpoint based on model type
        string baseUrl = $"https://{region}-aiplatform.googleapis.com/{apiVersion}";
        
        if (modelType.Equals("gemini", StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseUrl}/projects/{GetProjectId()}/locations/{region}/publishers/google/models/{modelId}:predict";
        }
        else
        {
            return $"{baseUrl}/projects/{GetProjectId()}/locations/{region}/publishers/google/models/{modelId}:predict";
        }
    }
    
    private string GetProjectId()
    {
        // Extract or provide project ID
        // In a real implementation, this would come from configuration
        // For now, extract from ApiBase or use a default
        string projectId = "your-project-id";
        
        // Since ApiRegion doesn't exist, we'll use a pattern for now
        // In a real implementation, this should come from proper configuration
        if (!string.IsNullOrWhiteSpace(_credentials.ApiVersion))
        {
            projectId = _credentials.ApiVersion;
        }
        
        return projectId;
    }
    
    private VertexAIGeminiRequest PrepareGeminiRequest(ChatCompletionRequest request)
    {
        var geminiRequest = new VertexAIGeminiRequest
        {
            Contents = new List<VertexAIGeminiContent>(),
            GenerationConfig = new VertexAIGenerationConfig
            {
                Temperature = (float?)request.Temperature,
                MaxOutputTokens = request.MaxTokens,
                TopP = (float?)request.TopP
            }
        };
        
        // Map messages
        foreach (var message in request.Messages)
        {
            var geminiContent = new VertexAIGeminiContent
            {
                Role = MapCoreRoleToGeminiRole(message.Role),
                Parts = new List<VertexAIGeminiPart>
                {
                    new VertexAIGeminiPart
                    {
                        Text = message.Content
                    }
                }
            };
            
            geminiRequest.Contents.Add(geminiContent);
        }
        
        return geminiRequest;
    }
    
    private VertexAIPredictionRequest PreparePaLMRequest(ChatCompletionRequest request)
    {
        // Format messages for PaLM
        StringBuilder prompt = new StringBuilder();
        
        // Extract system message if present
        var systemMessage = request.Messages.FirstOrDefault(m => 
            m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));
            
        if (systemMessage != null)
        {
            prompt.AppendLine(systemMessage.Content);
            prompt.AppendLine();
        }
        
        // Format the conversation
        foreach (var message in request.Messages.Where(m => 
            !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)))
        {
            string role = message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                ? "Assistant"
                : "Human";
                
            prompt.AppendLine($"{role}: {message.Content}");
        }
        
        // Add the final assistant prompt
        prompt.AppendLine("Assistant:");
        
        // Create the PaLM request
        var palmRequest = new VertexAIPredictionRequest
        {
            Instances = new List<object>
            {
                new VertexAIPaLMInstance
                {
                    Prompt = prompt.ToString().Trim()
                }
            },
            Parameters = new VertexAIParameters
            {
                Temperature = (float?)request.Temperature,
                MaxOutputTokens = request.MaxTokens,
                TopP = (float?)request.TopP
            }
        };
        
        return palmRequest;
    }
    
    private async Task<HttpResponseMessage> SendGeminiRequestAsync(
        string endpoint, 
        VertexAIGeminiRequest request, 
        string apiKey,
        CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
        requestMessage.Content = JsonContent.Create(request, options: new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        
        // Add API key as query parameter
        var uriBuilder = new UriBuilder(requestMessage.RequestUri!);
        var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
        query["key"] = apiKey;
        uriBuilder.Query = query.ToString();
        requestMessage.RequestUri = uriBuilder.Uri;
        
        return await _httpClient.SendAsync(requestMessage, cancellationToken);
    }
    
    private async Task<HttpResponseMessage> SendPaLMRequestAsync(
        string endpoint, 
        VertexAIPredictionRequest request, 
        string apiKey,
        CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
        requestMessage.Content = JsonContent.Create(request, options: new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        
        // Add API key as query parameter
        var uriBuilder = new UriBuilder(requestMessage.RequestUri!);
        var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
        query["key"] = apiKey;
        uriBuilder.Query = query.ToString();
        requestMessage.RequestUri = uriBuilder.Uri;
        
        return await _httpClient.SendAsync(requestMessage, cancellationToken);
    }
    
    private async Task<ChatCompletionResponse> ProcessGeminiResponseAsync(
        HttpResponseMessage response,
        string originalModelAlias,
        CancellationToken cancellationToken)
    {
        var vertexResponse = await response.Content.ReadFromJsonAsync<VertexAIPredictionResponse>(
            cancellationToken: cancellationToken);
        
        if (vertexResponse?.Predictions == null || !vertexResponse.Predictions.Any())
        {
            _logger.LogError("Failed to deserialize the response from Google Vertex AI or response is empty");
            throw new LLMCommunicationException("Failed to deserialize the response from Google Vertex AI or response is empty");
        }
        
        // Map all candidates from all predictions to core Choices
        var choices = new List<Choice>();
        int idx = 0;
        foreach (var prediction in vertexResponse.Predictions)
        {
            if (prediction.Candidates != null)
            {
                foreach (var candidate in prediction.Candidates)
                {
                    var content = candidate.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;
                    choices.Add(new Choice
                    {
                        Index = idx++,
                        Message = new Message
                        {
                            Role = "assistant",
                            Content = content
                        },
                        FinishReason = candidate.FinishReason
                    });
                }
            }
        }

        if (choices.Count == 0)
        {
            _logger.LogError("Gemini response has no candidates");
            throw new LLMCommunicationException("Gemini response has no candidates");
        }

        // Create the core response
        return new ChatCompletionResponse
        {
            Id = Guid.NewGuid().ToString(),
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = originalModelAlias, // Return the requested model alias
            Choices = choices,
            Usage = new Usage
            {
                // Vertex AI doesn't provide token usage in the response
                // Estimate based on text length
                PromptTokens = EstimateTokenCount(string.Join(" ", choices.Select(c => c.Message.Content)) ),
                CompletionTokens = EstimateTokenCount(string.Join(" ", choices.Select(c => c.Message.Content)) ),
                TotalTokens = 0 // Will be calculated below
            }
        };
    }
    
    private async Task<ChatCompletionResponse> ProcessPaLMResponseAsync(
        HttpResponseMessage response,
        string originalModelAlias,
        CancellationToken cancellationToken)
    {
        var vertexResponse = await response.Content.ReadFromJsonAsync<VertexAIPredictionResponse>(
            cancellationToken: cancellationToken);
            
        if (vertexResponse?.Predictions == null || !vertexResponse.Predictions.Any())
        {
            _logger.LogError("Failed to deserialize the response from Google Vertex AI PaLM or response is empty");
            throw new LLMCommunicationException("Failed to deserialize the response from Google Vertex AI PaLM or response is empty");
        }
        
        // Get the first prediction
        var prediction = vertexResponse.Predictions![0];
        
        if (string.IsNullOrEmpty(prediction.Content))
        {
            _logger.LogError("Vertex AI PaLM response has empty content");
            throw new LLMCommunicationException("Vertex AI PaLM response has empty content");
        }
        
        // Create the core response
        return new ChatCompletionResponse
        {
            Id = Guid.NewGuid().ToString(),
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = originalModelAlias, // Return the requested model alias
            Choices = new List<Choice>
            {
                new Choice
                {
                    Index = 0,
                    Message = new Message
                    {
                        Role = "assistant",
                        Content = prediction.Content
                    },
                    FinishReason = "stop" // PaLM doesn't provide finish reason in this format
                }
            },
            Usage = new Usage
            {
                // Vertex AI doesn't provide token usage in the response
                // Estimate based on text length
                PromptTokens = EstimateTokenCount(string.Join(" ", prediction.Content)),
                CompletionTokens = EstimateTokenCount(prediction.Content ?? string.Empty),
                TotalTokens = 0 // Will be calculated below
            }
        };
    }
    
    private string MapCoreRoleToGeminiRole(string? coreRole)
    {
        return coreRole?.ToLowerInvariant() switch
        {
            "user" => "user",
            "assistant" => "model",
            "system" => "user", // Gemini doesn't have a system role, prepend to first user message
            _ => coreRole ?? "user" // Default to user for unknown roles
        };
    }
    
    private int EstimateTokenCount(string text)
    {
        // Very rough token count estimation
        // In a real implementation, use a proper tokenizer
        if (string.IsNullOrEmpty(text))
            return 0;
            
        // Approximately 4 characters per token for English text
        return text.Length / 4;
    }
    
    #endregion
}
