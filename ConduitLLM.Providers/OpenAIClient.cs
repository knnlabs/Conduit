using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models; // Added missing using
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json; // Required for PostAsJsonAsync, ReadFromJsonAsync
using System.Text.Json.Serialization; // For JsonPropertyName
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // Added for logging
using ConduitLLM.Providers.InternalModels; // Use external models
using System.Text; // For reading error content
using System.Text.Json; // For JsonException
using System.IO; // For StreamReader
using System.Runtime.CompilerServices; // For IAsyncEnumerable

namespace ConduitLLM.Providers;

/// <summary>
/// Client for interacting with OpenAI-compatible APIs.
/// </summary>
public class OpenAIClient : ILLMClient
{
    // TODO: Refactor to use IHttpClientFactory for better resilience and management
    private readonly HttpClient _httpClient;
    private readonly ProviderCredentials _credentials;
    private readonly string _providerModelId; // The actual model ID (OpenAI) or deployment name (Azure)
    private readonly ILogger<OpenAIClient> _logger;
    private readonly string _providerName; // To distinguish between OpenAI, Azure, etc.

    // Default base URL for OpenAI API
    private const string DefaultOpenAIApiBase = "https://api.openai.com/v1/";
    private const string DefaultAzureApiVersion = "2024-02-01"; // Default Azure API version

    // Constructor now accepts providerName and httpClient
    public OpenAIClient(
        ProviderCredentials credentials, 
        string providerModelId, 
        ILogger<OpenAIClient> logger, 
        string? providerName = null,
        HttpClient? httpClient = null)
    {
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _providerModelId = providerModelId ?? throw new ArgumentNullException(nameof(providerModelId)); // For Azure, this is the deployment name
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _providerName = providerName ?? credentials.ProviderName ?? "openai"; // Default to "openai" if not specified

        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            throw new ConfigurationException($"API key is missing for provider '{_providerName.ToLowerInvariant()}'.");
        }

        // Allow injection of HttpClient for testing
        _httpClient = httpClient ?? new HttpClient();

        // Base address and default headers are set per-request now due to differences
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");
    }

    /// <inheritdoc />
    public async Task<ChatCompletionResponse> CreateChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null, // Added optional API key
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Determine the API key to use: override if provided, otherwise use configured key
        string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : _credentials.ApiKey!;
        if (string.IsNullOrWhiteSpace(effectiveApiKey))
        {
             // This should ideally not happen if constructor validation is correct, but double-check
             throw new ConfigurationException($"API key is missing for provider '{_providerName.ToLowerInvariant()}' and no override was provided.");
        }

        _logger.LogInformation("Mapping Core request to OpenAI request for model alias '{ModelAlias}', provider model ID '{ProviderModelId}'", request.Model, _providerModelId);
        var openAIRequest = MapToOpenAIRequest(request);

        HttpResponseMessage? response = null;
        try
        {
            // Construct request message manually to handle provider differences
            using var requestMessage = new HttpRequestMessage();
            requestMessage.Method = HttpMethod.Post;
            requestMessage.Content = JsonContent.Create(openAIRequest); // Use System.Net.Http.Json

            // Determine URL and Auth Header based on provider, using effectiveApiKey
            if (_providerName.Equals("azure", StringComparison.OrdinalIgnoreCase))
            {
                // Azure OpenAI
                if (string.IsNullOrWhiteSpace(_credentials.ApiBase))
                {
                    throw new ConfigurationException("ApiBase (Azure resource endpoint) is required for the 'azure' provider.");
                }
                string azureApiBase = _credentials.ApiBase.TrimEnd('/');
                string deploymentName = _providerModelId; // For Azure, providerModelId is the deployment name
                string apiVersion = !string.IsNullOrWhiteSpace(_credentials.ApiVersion) ? _credentials.ApiVersion : DefaultAzureApiVersion;
                requestMessage.RequestUri = new Uri($"{azureApiBase}/openai/deployments/{deploymentName}/chat/completions?api-version={apiVersion}");
                requestMessage.Headers.Add("api-key", effectiveApiKey); // Use effectiveApiKey
                _logger.LogDebug("Sending request to Azure OpenAI endpoint: {Endpoint}", requestMessage.RequestUri);
            }
            else
            {
                // OpenAI or other compatible (OpenRouter, FireworksAI)
                string apiBase = string.IsNullOrWhiteSpace(_credentials.ApiBase) ? DefaultOpenAIApiBase : _credentials.ApiBase;
                // Ensure ApiBase ends with a slash
                if (!apiBase.EndsWith('/')) apiBase += "/";
                // Use relative path for standard OpenAI-like structure
                requestMessage.RequestUri = new Uri(new Uri(apiBase), "v1/chat/completions");
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effectiveApiKey); // Use effectiveApiKey
                 _logger.LogDebug("Sending request to OpenAI-compatible endpoint: {Endpoint}", requestMessage.RequestUri);
            }


            response = await _httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // Attempt to read error content for better diagnostics
                string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
                _logger.LogError("openai API request failed with status code {StatusCode}. Response: {ErrorContent}", response.StatusCode, errorContent);
                // TODO: Parse OpenAI specific error structure from errorContent
                throw new LLMCommunicationException($"openai API request failed with status code {response.StatusCode}. Response: {errorContent}");
            }

            _logger.LogDebug("Received successful response from openai API.");
            var openAIResponse = await response.Content.ReadFromJsonAsync<OpenAIChatCompletionResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);

            // Validate the response structure
            if (openAIResponse == null)
            {
                _logger.LogError("Failed to deserialize the successful response from openai API.");
                throw new LLMCommunicationException("Failed to deserialize the response from openai API.");
            }
            if (openAIResponse.Choices == null || openAIResponse.Choices.Count == 0 || openAIResponse.Choices[0].Message == null)
            {
                _logger.LogWarning("openai response is missing expected choices or message content.");
                throw new LLMCommunicationException("Invalid response structure received from openai API (missing choices or message).");
            }
            if (openAIResponse.Usage == null)
            {
                 _logger.LogWarning("openai response is missing usage data.");
                 // Decide if this is critical - maybe allow it but log warning? For now, treat as error.
                 throw new LLMCommunicationException("Invalid response structure received from openai API (missing usage data).");
            }

            _logger.LogInformation("Mapping openai response back to Core response for model alias '{ModelAlias}'", request.Model);
            return MapToCoreResponse(openAIResponse, request.Model);
        }
        catch (JsonException ex)
        {
             _logger.LogError(ex, "JSON deserialization error processing openai response.");
             throw new LLMCommunicationException("Error deserializing openai response.", ex);
        }
        catch (HttpRequestException ex)
        {
            // Handle fundamental HTTP errors (network issues, DNS errors, etc.)
            _logger.LogError(ex, "HTTP request error communicating with openai API.");
            throw new LLMCommunicationException($"HTTP request error communicating with openai API: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning(ex, "openai API request timed out.");
            throw new LLMCommunicationException("openai API request timed out.", ex);
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
             _logger.LogInformation(ex, "openai API request was canceled.");
             throw; // Re-throw cancellation
        }
        catch (Exception ex) // Catch-all for unexpected errors
        {
            _logger.LogError(ex, "An unexpected error occurred while processing openai chat completion.");
            // Avoid throwing raw Exception, wrap it
            throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
        }
    }

    // --- Mapping Logic ---

    private OpenAIChatCompletionRequest MapToOpenAIRequest(ChatCompletionRequest coreRequest)
    {
        // Map core request to the DTO from InternalModels
        return new OpenAIChatCompletionRequest
        {
            Model = _providerModelId, // Use the specific model ID for the provider
            Messages = coreRequest.Messages.Select(m => new OpenAIMessage
            {
                // Ensure Role and Content are not null before assigning
                Role = m.Role ?? throw new ArgumentNullException(nameof(m.Role), "Message role cannot be null"),
                Content = m.Content ?? throw new ArgumentNullException(nameof(m.Content), "Message content cannot be null")
            }).ToList(),
            // Map only supported parameters defined in InternalModels/OpenAIModels.cs
            Temperature = (float?)coreRequest.Temperature, // Explicit cast needed
            MaxTokens = coreRequest.MaxTokens,
            // TODO: Map other parameters (TopP, N, Stream, Stop, User etc.) if added to Core request and OpenAIModels
        };
    }

    private ChatCompletionResponse MapToCoreResponse(OpenAIChatCompletionResponse openAIResponse, string originalModelAlias)
    {
        // Map DTO from InternalModels back to core response
        // Ensure Choices and the first choice/message are not null (already validated in caller, but good practice)
        var openAIChoice = openAIResponse.Choices?.FirstOrDefault();
        var openAIMessage = openAIChoice?.Message;
        var openAIUsage = openAIResponse.Usage; // Already validated non-null in caller

        if (openAIChoice == null || openAIMessage == null)
        {
            // This case should ideally be caught earlier, but handle defensively
            _logger.LogError("Invalid openai response structure encountered during mapping: Missing choice or message.");
            throw new LLMCommunicationException("Invalid response structure received from openai API (missing choice or message).");
        }

        return new ChatCompletionResponse
        {
            // Use null-coalescing for required string properties
            Id = openAIResponse.Id ?? Guid.NewGuid().ToString(), // Generate an ID if missing? Or throw?
            Object = openAIResponse.Object ?? "chat.completion", // Default if missing
            Created = openAIResponse.Created ?? 0, // Provide default 0 if null (CS0266 fix)
            Model = originalModelAlias, // Return the alias the user requested
            Choices = new List<Choice> // Assuming N=1 for now
            {
                new Choice
                {
                    Index = openAIChoice.Index,
                    // Ensure Role and Content are not null before assigning to required Message properties
                    Message = new Message {
                        Role = openAIMessage.Role ?? throw new LLMCommunicationException("openai response message role cannot be null."),
                        Content = openAIMessage.Content ?? throw new LLMCommunicationException("openai response message content cannot be null.")
                    },
                    FinishReason = openAIChoice.FinishReason ?? string.Empty // Add null check with empty string default
                }
            },
            Usage = new Usage // Already validated non-null
            {
                PromptTokens = openAIUsage?.PromptTokens ?? 0, // Add null conditional and default value
                CompletionTokens = openAIUsage?.CompletionTokens ?? 0, // Add null conditional and default value
                TotalTokens = openAIUsage?.TotalTokens ?? 0 // Add null conditional and default value
            }
            // TODO: Map SystemFingerprint if added to Core response
        };
    }

    private static async Task<string> ReadErrorContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            // Use a buffer to avoid potential issues with large error responses if needed,
            // but ReadAsStringAsync is usually fine for typical API errors.
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log this?
            return $"Failed to read error content: {ex.Message}";
        }
    }

    // --- Removed internal DTOs as they are now in InternalModels/OpenAIModels.cs ---

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
        ChatCompletionRequest request,
        string? apiKey = null, // Added optional API key
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Determine the API key to use: override if provided, otherwise use configured key
        string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : _credentials.ApiKey!;
         if (string.IsNullOrWhiteSpace(effectiveApiKey))
        {
             throw new ConfigurationException($"API key is missing for provider '{_providerName.ToLowerInvariant()}' and no override was provided.");
        }

        _logger.LogInformation("Mapping Core request to openai streaming request for model alias '{ModelAlias}', provider model ID '{ProviderModelId}'", request.Model, _providerModelId);
        // Map request, ensuring Stream = true
        var openAIRequest = MapToOpenAIRequest(request) with { Stream = true };

        // Declare response outside the try block
        HttpResponseMessage response;
        try
        {
            // Assign response inside the try block, passing the effectiveApiKey
            response = await SetupAndSendStreamingRequestAsync(openAIRequest, effectiveApiKey, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) // Catch setup/connection errors
        {
            _logger.LogError(ex, "Error setting up or sending initial streaming request to openai API.");
            // Ensure specific exception types are thrown if needed (e.g., ConfigurationException, LLMCommunicationException)
            // For simplicity, rethrowing wrapped exception, but could be more specific.
            throw new LLMCommunicationException($"Failed to initiate stream: {ex.Message}", ex);
        }

        // 2. Process the stream, passing the assigned response and originalModelAlias
        await foreach (var chunk in ProcessOpenAIStreamAsync(response, originalModelAlias: request.Model, cancellationToken).ConfigureAwait(false))
        {
             yield return chunk;
        }
    }

    // Helper to setup and send the initial streaming request
    private async Task<HttpResponseMessage> SetupAndSendStreamingRequestAsync(
        OpenAIChatCompletionRequest openAIRequest,
        string effectiveApiKey, // Pass the key to use
        CancellationToken cancellationToken)
    {
        // Construct request message manually
        using var requestMessage = new HttpRequestMessage();
            requestMessage.Method = HttpMethod.Post;
            requestMessage.Content = JsonContent.Create(openAIRequest, options: new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }); // Ensure nulls aren't sent unnecessarily

            // Determine URL and Auth Header based on provider (same logic as non-streaming), using effectiveApiKey
            if (_providerName.Equals("azure", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(_credentials.ApiBase)) throw new ConfigurationException("ApiBase (Azure resource endpoint) is required for the 'azure' provider.");
                string azureApiBase = _credentials.ApiBase.TrimEnd('/');
                string deploymentName = _providerModelId;
                string apiVersion = !string.IsNullOrWhiteSpace(_credentials.ApiVersion) ? _credentials.ApiVersion : DefaultAzureApiVersion;
                requestMessage.RequestUri = new Uri($"{azureApiBase}/openai/deployments/{deploymentName}/chat/completions?api-version={apiVersion}");
                requestMessage.Headers.Add("api-key", effectiveApiKey); // Use effectiveApiKey
                _logger.LogDebug("Sending streaming request to Azure openai endpoint: {Endpoint}", requestMessage.RequestUri);
            }
            else
            {
                string apiBase = string.IsNullOrWhiteSpace(_credentials.ApiBase) ? DefaultOpenAIApiBase : _credentials.ApiBase;
                if (!apiBase.EndsWith('/')) apiBase += "/";
                requestMessage.RequestUri = new Uri(new Uri(apiBase), "v1/chat/completions"); // Use relative path
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effectiveApiKey); // Use effectiveApiKey
                _logger.LogDebug("Sending streaming request to openai-compatible endpoint: {Endpoint}", requestMessage.RequestUri);
            }

            // Send request and get headers first - Re-add declaration
            HttpResponseMessage response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
                _logger.LogError("openai API streaming request failed with status code {StatusCode}. Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new LLMCommunicationException($"openai API streaming request failed with status code {response.StatusCode}. Response: {errorContent}");
            }

            _logger.LogDebug("Received successful streaming response header from openai API. Starting stream processing.");
            return response; // Return the successful response with headers (This was the intended return after the first check)
    }


    // Helper to process the actual stream content
    // Completely restructured to avoid yield within try/catch (which causes CS1626 error)
    private async IAsyncEnumerable<ChatCompletionChunk> ProcessOpenAIStreamAsync(HttpResponseMessage response, string originalModelAlias, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Stream? responseStream = null;
        StreamReader? reader = null;

        try
        {
            responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            reader = new StreamReader(responseStream, Encoding.UTF8);
        }
        catch (HttpRequestException ex)
        {
            throw new LLMCommunicationException($"Network error during stream setup: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new LLMCommunicationException($"IO error during stream setup: {ex.Message}", ex);
        }

        // If we get here, we have a valid reader
        try // only try/finally is allowed with yield
        {
            while (!reader.EndOfStream)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                string? line = null;
                try
                {
                    line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    throw new LLMCommunicationException($"Network error reading from stream: {ex.Message}", ex);
                }
                catch (IOException ex)
                {
                    throw new LLMCommunicationException($"IO error reading from stream: {ex.Message}", ex);
                }

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith("data:"))
                {
                    string jsonData = line.Substring("data:".Length).Trim();
                    if (jsonData.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Received [DONE] marker, ending stream processing.");
                        break;
                    }

                    ChatCompletionChunk? coreChunk = null;
                    bool deserializationFailed = false;
                    try
                    {
                        var openAIChunk = JsonSerializer.Deserialize<OpenAIChatCompletionChunk>(jsonData);
                        if (openAIChunk != null)
                        {
                            coreChunk = MapToCoreChunk(openAIChunk, originalModelAlias);
                        }
                        else
                        {
                            _logger.LogWarning("Deserialized openai chunk was null. JSON: {JsonData}", jsonData);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "JSON deserialization error processing openai stream chunk. JSON: {JsonData}", jsonData);
                        deserializationFailed = true;
                        throw new LLMCommunicationException($"Error deserializing openai stream chunk: Invalid JSON data. {ex.Message}. Data: {jsonData}", ex);
                    }
                    if (!deserializationFailed && coreChunk != null)
                    {
                        yield return coreChunk;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith(":"))
                {
                    _logger.LogTrace("Skipping non-data line in SSE stream: {Line}", line);
                }
            }
            _logger.LogInformation("Finished processing openai stream lines.");
        }
        finally
        {
            reader?.Dispose();
            responseStream?.Dispose();
            response.Dispose();
            _logger.LogDebug("Disposed openai stream resources.");
        }
    }

    // New mapping function for streaming chunks
    private ChatCompletionChunk MapToCoreChunk(OpenAIChatCompletionChunk openAIChunk, string originalModelAlias)
    {
        return new ChatCompletionChunk
        {
            Id = openAIChunk.Id,
            Object = openAIChunk.Object ?? "chat.completion.chunk", // Default if missing
            Created = openAIChunk.Created,
            Model = originalModelAlias, // Use the alias requested by the user
            Choices = (openAIChunk.Choices ?? new List<OpenAIStreamingChoice>()).Select(c => new StreamingChoice
            {
                Index = c.Index,
                // Ensure Role and Content are not null before assigning to required Message properties
                Delta = new DeltaContent
                {
                    Role = c.Delta?.Role, // Can be null
                    Content = c.Delta?.Content // Can be null
                },
                FinishReason = c.FinishReason // Can be null
            }).ToList()
        };
    }

     /// <inheritdoc />
    public virtual async Task<List<string>> ListModelsAsync(string? apiKey = null, CancellationToken cancellationToken = default)
    {
        // Azure openai does not support the /v1/models endpoint directly via API key auth.
        // Listing models typically requires Azure RBAC permissions and uses Azure management libraries,
        // which is beyond the scope of simple API key interaction.
        if (_providerName.Equals("azure", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Listing models is not supported for Azure openai provider via this client.");
            // Return an empty list or throw an exception? Let's return empty for now.
            // Consider throwing UnsupportedProviderException if this is a hard requirement.
            return new List<string>();
            // throw new UnsupportedProviderException("Listing models is not directly supported for Azure openai via API key authentication.");
        }

        // Determine the API key to use
        string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : _credentials.ApiKey!;
        if (string.IsNullOrWhiteSpace(effectiveApiKey))
        {
            throw new ConfigurationException($"API key is missing for provider '{_providerName.ToLowerInvariant()}' and no override was provided.");
        }

        // Determine base URL (openai or compatible)
        string apiBase = string.IsNullOrWhiteSpace(_credentials.ApiBase) ? DefaultOpenAIApiBase : _credentials.ApiBase;
        if (!apiBase.EndsWith('/')) apiBase += "/";
        var requestUri = new Uri(new Uri(apiBase), "v1/models"); // Use relative path

        _logger.LogDebug("Sending request to list models from: {Endpoint}", requestUri);

        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effectiveApiKey);

            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
                _logger.LogError("openai API list models request failed with status code {StatusCode}. Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new LLMCommunicationException($"openai API list models request failed with status code {response.StatusCode}. Response: {errorContent}");
            }

            var modelListResponse = await response.Content.ReadFromJsonAsync<OpenAIModelListResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);

            if (modelListResponse == null || modelListResponse.Data == null)
            {
                 _logger.LogError("Failed to deserialize the successful model list response from openai API.");
                 throw new LLMCommunicationException("Failed to deserialize the model list response from openai API.");
            }

            // Extract just the model IDs
            var modelIds = modelListResponse.Data.Select(m => m.Id).ToList();
            _logger.LogInformation("Successfully retrieved {ModelCount} models from {ProviderName}.", modelIds.Count, _providerName);
            return modelIds;
        }
        catch (JsonException ex)
        {
             _logger.LogError(ex, "JSON deserialization error processing openai model list response.");
             throw new LLMCommunicationException("Error deserializing openai model list response.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error communicating with openai API for model list.");
            throw new LLMCommunicationException($"HTTP request error communicating with openai API: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning(ex, "openai API list models request timed out.");
            throw new LLMCommunicationException("openai API request timed out.", ex);
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
             _logger.LogInformation(ex, "openai API list models request was canceled.");
             throw; // Re-throw cancellation
        }
        catch (Exception ex) // Catch-all
        {
            _logger.LogError(ex, "An unexpected error occurred while listing openai models.");
            throw new LLMCommunicationException($"An unexpected error occurred while listing models: {ex.Message}", ex);
        }
    }

}
