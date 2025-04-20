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
using System.Text.Json.Serialization; // Added for JsonIgnoreCondition
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.InternalModels; // Assuming OllamaModels.cs will be populated here

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers;

/// <summary>
/// Client for interacting with Ollama APIs.
/// </summary>
public class OllamaClient : ILLMClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProviderCredentials _credentials;
    private readonly string _providerModelId; // The specific Ollama model tag
    private readonly ILogger<OllamaClient> _logger;
    private readonly string _providerName = "ollama"; // Hardcoded for this client

    // Default base URL for local Ollama instance
    private const string DefaultOllamaApiBase = "http://localhost:11434";

    public OllamaClient(
        ProviderCredentials credentials,
        string providerModelId,
        ILogger<OllamaClient> logger,
        IHttpClientFactory httpClientFactory)
    {
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _providerModelId = providerModelId ?? throw new ArgumentNullException(nameof(providerModelId));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

        // API Key is typically not required for local Ollama, but ApiBase might be different
        if (string.IsNullOrWhiteSpace(_credentials.ApiBase))
        {
            _logger.LogInformation("Ollama ApiBase not provided, defaulting to {DefaultBase}", DefaultOllamaApiBase);
        }
        else
        {
             _logger.LogInformation("Using provided Ollama ApiBase: {ApiBase}", _credentials.ApiBase);
        }
    }

    private string GetEffectiveApiBase() => string.IsNullOrWhiteSpace(_credentials.ApiBase) ? DefaultOllamaApiBase : _credentials.ApiBase.TrimEnd('/');


    /// <inheritdoc />
    public async Task<ChatCompletionResponse> CreateChatCompletionAsync(ChatCompletionRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        // API Key is typically ignored for Ollama

        _logger.LogInformation("Mapping Core request to Ollama request for model '{Model}'", _providerModelId);
        var ollamaRequest = MapToOllamaChatRequest(request);

        string apiBase = GetEffectiveApiBase();
        var requestUri = new Uri($"{apiBase}/api/chat");

        _logger.LogDebug("Sending request to Ollama endpoint: {Endpoint}", requestUri);

        using var httpClient = _httpClientFactory.CreateClient(_providerName);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");

        HttpResponseMessage? response = null;
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = JsonContent.Create(ollamaRequest, options: new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull })
            };
            // No specific auth headers usually needed for local Ollama

            response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
                _logger.LogError("Ollama API request failed with status code {StatusCode}. Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new LLMCommunicationException($"Ollama API request failed with status code {response.StatusCode}. Response: {errorContent}");
            }

            _logger.LogDebug("Received successful response from Ollama API.");
            var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);

            if (ollamaResponse == null || !ollamaResponse.Done || ollamaResponse.Message == null)
            {
                 _logger.LogError("Failed to deserialize or received incomplete response from Ollama API.");
                 throw new LLMCommunicationException("Invalid or incomplete response structure received from Ollama API.");
            }

            _logger.LogInformation("Mapping Ollama response back to Core response for model '{Model}'", request.Model);
            return MapToCoreChatResponse(ollamaResponse, request.Model);

        }
        catch (JsonException ex)
        {
             _logger.LogError(ex, "JSON deserialization error processing Ollama response.");
             throw new LLMCommunicationException("Error deserializing Ollama response.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error communicating with Ollama API.");
            throw new LLMCommunicationException($"HTTP request error communicating with Ollama API: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning(ex, "Ollama API request timed out.");
            throw new LLMCommunicationException("Ollama API request timed out.", ex);
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
             _logger.LogInformation(ex, "Ollama API request was canceled.");
             throw; // Re-throw cancellation
        }
        catch (Exception ex) // Catch-all
        {
            _logger.LogError(ex, "An unexpected error occurred while processing Ollama chat completion.");
            throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
        }
        finally
        {
            response?.Dispose();
        }
    }

    // Helper to setup and send the initial streaming request
    private async Task<HttpResponseMessage> SetupAndSendOllamaStreamRequestAsync(
        OllamaChatRequest ollamaRequest,
        CancellationToken cancellationToken)
    {
        string apiBase = GetEffectiveApiBase();
        var requestUri = new Uri($"{apiBase}/api/chat");

        _logger.LogDebug("Sending streaming request to Ollama endpoint: {Endpoint}", requestUri);

        // Create client from factory for each request
        using var httpClient = _httpClientFactory.CreateClient(_providerName); 
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(ollamaRequest, options: new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull })
        };

        // Send request and get headers first
        var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        // Check for non-success status code before attempting to read stream
        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
             _logger.LogError("Ollama API streaming request failed with status code {StatusCode}. Response: {ErrorContent}", response.StatusCode, errorContent);
             response.Dispose(); // Dispose response before throwing
            throw new LLMCommunicationException($"Ollama API streaming request failed with status code {response.StatusCode}. Response: {errorContent}");
        }
         _logger.LogDebug("Received successful streaming response header from Ollama API.");
        return response; // Return the successful response (caller is responsible for disposal)
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(ChatCompletionRequest request, string? apiKey = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        // API Key is typically ignored for Ollama

        _logger.LogInformation("Mapping Core request to Ollama streaming request for model '{Model}'", _providerModelId);
        var ollamaRequest = MapToOllamaChatRequest(request) with { Stream = true }; // Ensure Stream is true

        HttpResponseMessage? response = null; 
        StreamReader? reader = null;
        Stream? responseStream = null;
        IAsyncEnumerator<ChatCompletionChunk>? chunkEnumerator = null;

        try
        {
            // Setup and send the initial request
            response = await SetupAndSendOllamaStreamRequestAsync(ollamaRequest, cancellationToken).ConfigureAwait(false);
            responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            reader = new StreamReader(responseStream, Encoding.UTF8);

            // Get the enumerator from the helper method
            chunkEnumerator = ProcessOllamaStreamInternalAsync(reader, request.Model, cancellationToken).GetAsyncEnumerator(cancellationToken);

            // Iterate and yield chunks - This loop is now outside the try block with a catch clause
            while (true) 
            {
                try
                {
                    if (!await chunkEnumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        break; // End of stream
                    }
                }
                catch (Exception ex) // Catch errors during MoveNextAsync or within the helper
                {
                    _logger.LogError(ex, "Error during Ollama stream iteration.");
                    throw new LLMCommunicationException($"Error during Ollama stream iteration: {ex.Message}", ex);
                }
                yield return chunkEnumerator.Current;
            }
        }
        finally // Ensure all resources are disposed
        {
            if (chunkEnumerator != null)
            {
                await chunkEnumerator.DisposeAsync(); 
            }
            // Reader, Stream, and Response are potentially created before the loop, ensure disposal
            reader?.Dispose();
            responseStream?.Dispose();
            response?.Dispose();
             _logger.LogDebug("Disposed Ollama stream resources in main method.");
        }
    }

    // Private helper method to process the stream and yield chunks
    // This method NO LONGER handles disposal or contains try/catch blocks.
    private async IAsyncEnumerable<ChatCompletionChunk> ProcessOllamaStreamInternalAsync(
        StreamReader reader, 
        string originalModelAlias, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? line;
        // ReadLineAsync returns null when the end of the stream is reached.
        // Exceptions during ReadLineAsync (e.g., IOException) or Deserialize will propagate up.
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
             if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Stream processing canceled.", cancellationToken);

            if (string.IsNullOrWhiteSpace(line))
                continue;
            
            // Each line is a JSON object representing a chunk
            OllamaStreamChunk? ollamaChunk = JsonSerializer.Deserialize<OllamaStreamChunk>(line);

            if (ollamaChunk != null) 
            {
                yield return MapToCoreStreamChunk(ollamaChunk, originalModelAlias); 
                
                if (ollamaChunk.Done)
                {
                    _logger.LogInformation("Received 'done' marker in Ollama stream chunk, ending stream processing.");
                    break; // Exit loop normally
                }
            }
            else
            {
                 _logger.LogWarning("Deserialized Ollama stream chunk was null. JSON: {JsonLine}", line);
            }
        }
         _logger.LogInformation("Finished processing Ollama stream lines.");
    }


    /// <inheritdoc />
    public async Task<List<string>> ListModelsAsync(string? apiKey = null, CancellationToken cancellationToken = default)
    {
        // API Key is ignored for Ollama
        string apiBase = GetEffectiveApiBase();
        var requestUri = new Uri($"{apiBase}/api/tags");

        _logger.LogDebug("Sending request to list Ollama models from: {Endpoint}", requestUri);

        using var httpClient = _httpClientFactory.CreateClient(_providerName);
        // Add standard headers
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");

        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
            // No authorization typically needed for local Ollama

            using var response = await httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
                _logger.LogError("Ollama API list models request failed with status code {StatusCode}. Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new LLMCommunicationException($"Ollama API list models request failed with status code {response.StatusCode}. Response: {errorContent}");
            }

            var tagsResponse = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);

            if (tagsResponse == null || tagsResponse.Models == null)
            {
                 _logger.LogError("Failed to deserialize the successful model list response from Ollama API.");
                 throw new LLMCommunicationException("Failed to deserialize the model list response from Ollama API.");
            }

            // Extract just the model names (tags)
            var modelIds = tagsResponse.Models.Select(m => m.Name).ToList();
            _logger.LogInformation("Successfully retrieved {ModelCount} models from Ollama.", modelIds.Count);
            return modelIds;
        }
        catch (JsonException ex)
        {
             _logger.LogError(ex, "JSON deserialization error processing Ollama model list response.");
             throw new LLMCommunicationException("Error deserializing Ollama model list response.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error communicating with Ollama API for model list.");
            throw new LLMCommunicationException($"HTTP request error communicating with Ollama API: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning(ex, "Ollama API list models request timed out.");
            throw new LLMCommunicationException("Ollama API request timed out.", ex);
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
             _logger.LogInformation(ex, "Ollama API list models request was canceled.");
             throw; // Re-throw cancellation
        }
        catch (Exception ex) // Catch-all
        {
            _logger.LogError(ex, "An unexpected error occurred while listing Ollama models.");
            throw new LLMCommunicationException($"An unexpected error occurred while listing models: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<EmbeddingResponse> CreateEmbeddingAsync(EmbeddingRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Input validation: Ollama expects a single string prompt
        if (request.Input is not string prompt || string.IsNullOrWhiteSpace(prompt))
        {
             throw new ArgumentException("Ollama embedding requires a single non-empty string as input.", nameof(request.Input));
        }
        // API Key ignored

        _logger.LogInformation("Mapping Core embedding request to Ollama request for model '{Model}'", _providerModelId);
        // Note: Ollama uses the client's configured model ID (_providerModelId), not the one from the request directly.
        var ollamaRequest = new OllamaEmbeddingRequest
        {
            Model = _providerModelId,
            Prompt = prompt 
            // Options can be added here if needed/supported by Core request
        };

        string apiBase = GetEffectiveApiBase();
        var requestUri = new Uri($"{apiBase}/api/embeddings");

         _logger.LogDebug("Sending request to Ollama embeddings endpoint: {Endpoint}", requestUri);

        using var httpClient = _httpClientFactory.CreateClient(_providerName);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");

        HttpResponseMessage? response = null;
        try
        {
             using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
             {
                 Content = JsonContent.Create(ollamaRequest, options: new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull })
             };

            response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
                _logger.LogError("Ollama API embeddings request failed with status code {StatusCode}. Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new LLMCommunicationException($"Ollama API embeddings request failed with status code {response.StatusCode}. Response: {errorContent}");
            }

            _logger.LogDebug("Received successful response from Ollama embeddings API.");
            var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);

            if (ollamaResponse == null || ollamaResponse.Embedding == null)
            {
                 _logger.LogError("Failed to deserialize or received incomplete embeddings response from Ollama API.");
                 throw new LLMCommunicationException("Invalid or incomplete embeddings response structure received from Ollama API.");
            }

            // Map back to Core response
            return new EmbeddingResponse
            {
                Object = "list", // Mimic OpenAI structure
                Data = new List<EmbeddingData>
                {
                    new EmbeddingData
                    {
                        Object = "embedding",
                        Index = 0,
                        // EmbeddingData expects List<float>, Ollama provides List<float>
                        Embedding = ollamaResponse.Embedding 
                    }
                },
                Model = request.Model, // Use original requested model alias
                // Initialize all required Usage fields
                Usage = new Usage { PromptTokens = 0, CompletionTokens = 0, TotalTokens = 0 } 
            };
        }
        catch (JsonException ex)
        {
             _logger.LogError(ex, "JSON deserialization error processing Ollama embeddings response.");
             throw new LLMCommunicationException("Error deserializing Ollama embeddings response.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error communicating with Ollama API for embeddings.");
            throw new LLMCommunicationException($"HTTP request error communicating with Ollama API: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning(ex, "Ollama API embeddings request timed out.");
            throw new LLMCommunicationException("Ollama API embeddings request timed out.", ex);
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
             _logger.LogInformation(ex, "Ollama API embeddings request was canceled.");
             throw; // Re-throw cancellation
        }
        catch (Exception ex) // Catch-all
        {
            _logger.LogError(ex, "An unexpected error occurred while creating Ollama embeddings.");
            throw new LLMCommunicationException($"An unexpected error occurred while creating embeddings: {ex.Message}", ex);
        }
         finally
        {
            response?.Dispose();
        }
    }

    /// <inheritdoc />
    public Task<ImageGenerationResponse> CreateImageAsync(ImageGenerationRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Ollama does not support image generation.");
        throw new UnsupportedProviderException("Ollama does not support image generation.");
    }

    // --- Mapping Functions ---

    private OllamaChatRequest MapToOllamaChatRequest(ChatCompletionRequest coreRequest)
    {
        // Map options - only map supported ones from Core request
        OllamaOptions? options = null;
        if (coreRequest.Temperature.HasValue || coreRequest.MaxTokens.HasValue || coreRequest.TopP.HasValue) 
        {
            options = new OllamaOptions
            {
                Temperature = (float?)coreRequest.Temperature,
                NumPredict = coreRequest.MaxTokens,
                TopP = (float?)coreRequest.TopP
            };
        }

        return new OllamaChatRequest
        {
            Model = _providerModelId, 
            Messages = coreRequest.Messages.Select(m => new OllamaMessage
            {
                Role = m.Role ?? throw new ArgumentNullException(nameof(m.Role), "Message role cannot be null"),
                // Use ToString() for content, assuming it's appropriate or a string
                Content = m.Content?.ToString() ?? string.Empty, 
            }).ToList(),
            Options = options,
            Stream = false, 
        };
    }

    private ChatCompletionResponse MapToCoreChatResponse(OllamaChatResponse ollamaResponse, string originalModelAlias)
    {
        var usage = new Usage
        {
            PromptTokens = ollamaResponse.PromptEvalCount ?? 0,
            CompletionTokens = ollamaResponse.EvalCount ?? 0,
            TotalTokens = (ollamaResponse.PromptEvalCount ?? 0) + (ollamaResponse.EvalCount ?? 0)
        };

        return new ChatCompletionResponse
        {
            Id = Guid.NewGuid().ToString(), 
            Object = "chat.completion", 
            Created = DateTimeOffset.TryParse(ollamaResponse.CreatedAt, out var createdAt) ? createdAt.ToUnixTimeSeconds() : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = originalModelAlias, 
            Choices = new List<Choice>
            {
                new Choice
                {
                    Index = 0,
                    Message = new Message
                    {
                        Role = ollamaResponse.Message?.Role ?? "assistant",
                        // Ensure content mapping handles potential nulls
                        Content = ollamaResponse.Message?.Content ?? string.Empty 
                    },
                    FinishReason = FinishReason.Stop 
                }
            },
            Usage = usage
        };
    }

    private ChatCompletionChunk MapToCoreStreamChunk(OllamaStreamChunk ollamaChunk, string originalModelAlias)
    {
         Usage? usage = null;
         if (ollamaChunk.Done)
         {
             usage = new Usage
             {
                 PromptTokens = ollamaChunk.PromptEvalCount ?? 0,
                 CompletionTokens = ollamaChunk.EvalCount ?? 0,
                 TotalTokens = (ollamaChunk.PromptEvalCount ?? 0) + (ollamaChunk.EvalCount ?? 0)
             };
         }

        return new ChatCompletionChunk
        {
            Id = Guid.NewGuid().ToString(), 
            Object = "chat.completion.chunk", 
            Created = DateTimeOffset.TryParse(ollamaChunk.CreatedAt, out var createdAt) ? createdAt.ToUnixTimeSeconds() : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = originalModelAlias, 
            Choices = new List<StreamingChoice>
            {
                new StreamingChoice
                {
                    Index = 0, 
                    Delta = new DeltaContent
                    {
                        Role = ollamaChunk.Message?.Role, 
                        Content = ollamaChunk.Message?.Content 
                    },
                    FinishReason = ollamaChunk.Done ? FinishReason.Stop : null 
                }
            }
            // Usage property removed from ChatCompletionChunk
        };
    }

    // Helper to read error content, similar to OpenAIClient
    private static async Task<string> ReadErrorContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log this? Maybe pass logger instance if needed.
            return $"Failed to read error content: {ex.Message}";
        }
    }
}
