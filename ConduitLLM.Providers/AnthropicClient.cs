using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices; // For IAsyncEnumerable
using System.Text; // For reading error content
using System.Text.Json; // For JsonException and deserializing errors

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.InternalModels; // Use external models

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers;

/// <summary>
/// Client for interacting with the Anthropic API (Claude models).
/// </summary>
public class AnthropicClient : ILLMClient
{
    // TODO: Refactor to use IHttpClientFactory
    private readonly HttpClient _httpClient;
    private readonly ProviderCredentials _credentials;
    private readonly string _providerModelId; // The actual model ID for Anthropic (e.g., claude-3-opus-20240229)
    private readonly ILogger<AnthropicClient> _logger;

    // Default base URL for Anthropic API
    private const string DefaultApiBase = "https://api.anthropic.com/v1/";
    private const string MessagesEndpoint = "messages";
    // Required Anthropic API version header
    private const string AnthropicVersion = "2023-06-01";

    public AnthropicClient(ProviderCredentials credentials, string providerModelId, ILogger<AnthropicClient> logger, HttpClient? httpClient = null)
    {
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _providerModelId = providerModelId ?? throw new ArgumentNullException(nameof(providerModelId));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            throw new ConfigurationException($"API key (x-api-key) is missing for provider '{credentials.ProviderName}'.");
        }

        // Allow injection of HttpClient for testing
        _httpClient = httpClient ?? new HttpClient();

        // Use ApiBase from credentials if provided, otherwise default
        string apiBase = string.IsNullOrWhiteSpace(credentials.ApiBase) ? DefaultApiBase : credentials.ApiBase;
        // Ensure ApiBase ends with a slash
        if (!apiBase.EndsWith('/'))
        {
            apiBase += "/";
        }
        _httpClient.BaseAddress = new Uri(apiBase);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Remove default API key header - will be set per request
        // _httpClient.DefaultRequestHeaders.Add("x-api-key", credentials.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion); // Keep version header as default
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
            throw new ConfigurationException($"API key (x-api-key) is missing for provider '{_credentials.ProviderName}' and no override was provided.");
        }

        _logger.LogInformation("Mapping Core request to Anthropic request for model alias '{ModelAlias}', provider model ID '{ProviderModelId}'", request.Model, _providerModelId);
        AnthropicMessageRequest anthropicRequest;
        try
        {
            anthropicRequest = MapToAnthropicRequest(request);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Failed to map core request to Anthropic request.");
            throw new ConfigurationException($"Invalid request structure for Anthropic provider: {ex.Message}", ex);
        }

        HttpResponseMessage? response = null;
        try
        {
            // Create request message manually for better control
            var requestUri = new Uri(_httpClient.BaseAddress!, MessagesEndpoint);

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = JsonContent.Create(anthropicRequest)
            };

            // Set x-api-key header for this specific request
            requestMessage.Headers.Add("x-api-key", effectiveApiKey);

            _logger.LogDebug("Sending chat completion request to Anthropic API: {Endpoint}", requestUri);
            response = await _httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var responseDto = await response.Content.ReadFromJsonAsync<AnthropicMessageResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);

                if (responseDto == null)
                {
                    _logger.LogError("Failed to deserialize Anthropic response despite 200 OK status");
                    throw new LLMCommunicationException("Failed to deserialize the successful response from Anthropic.");
                }

                var coreResponse = MapToCoreResponse(responseDto, request.Model);
                _logger.LogInformation("Successfully received and mapped Anthropic chat completion response.");
                return coreResponse;
            }
            else
            {
                string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
                _logger.LogError("Anthropic API request failed with status {Status}. Response: {ErrorContent}", response.StatusCode, errorContent);

                try
                {
                    // Try to parse the error as JSON first
                    var errorDto = JsonSerializer.Deserialize<AnthropicErrorResponse>(errorContent);
                    if (errorDto?.Error != null)
                    {
                        string errorMessage = $"Anthropic API Error ({errorDto.Error.Type}): {errorDto.Error.Message}";
                        _logger.LogError(errorMessage);
                        throw new LLMCommunicationException(errorMessage);
                    }
                }
                catch (JsonException)
                {
                    // If it's not JSON, treat as plain text error
                    _logger.LogWarning("Could not parse Anthropic error response as JSON. Treating as plain text.");
                }

                // Default error if JSON parsing failed or error structure unexpected
                throw new LLMCommunicationException($"Anthropic API request failed with status code {response.StatusCode}. Response: {errorContent}");
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON error processing Anthropic response.");
            throw new LLMCommunicationException("Error deserializing Anthropic response.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error communicating with Anthropic API.");
            throw new LLMCommunicationException($"HTTP request error communicating with Anthropic API: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning(ex, "Anthropic API request timed out.");
            throw new LLMCommunicationException("Anthropic API request timed out.", ex);
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(ex, "Anthropic API request was cancelled by the caller.");
            throw; // Re-throw cancellation
        }
        catch (LLMCommunicationException)
        {
            // Re-throw LLMCommunicationException directly, already logged
            throw;
        }
        catch (Exception ex) // This is a catch-all for unexpected exceptions
        {
            _logger.LogError(ex, "An unexpected error occurred during Anthropic API request.");
            throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
        }
        finally
        {
            response?.Dispose();
        }
    }

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
            throw new ConfigurationException($"API key (x-api-key) is missing for provider '{_credentials.ProviderName}' and no override was provided.");
        }

        _logger.LogInformation("Mapping Core request to Anthropic streaming request for model alias '{ModelAlias}', provider model ID '{ProviderModelId}'", request.Model, _providerModelId);
        AnthropicMessageRequest anthropicRequest;
        try
        {
            // Create a new request object with Stream = true
            anthropicRequest = MapToAnthropicRequest(request);
            // Need to create a new object since Stream may be init-only
            anthropicRequest = new AnthropicMessageRequest
            {
                Model = anthropicRequest.Model,
                Messages = anthropicRequest.Messages,
                SystemPrompt = anthropicRequest.SystemPrompt,
                MaxTokens = anthropicRequest.MaxTokens,
                Temperature = anthropicRequest.Temperature,
                TopP = anthropicRequest.TopP,
                StopSequences = anthropicRequest.StopSequences,
                Stream = true // Set streaming
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Failed to map core request to Anthropic streaming request.");
            throw new ConfigurationException($"Invalid request structure for Anthropic provider: {ex.Message}", ex);
        }

        // Declare response outside the try block
        HttpResponseMessage response;
        string currentModel = request.Model; // Use requested alias

        try
        {
            // Assign response inside the try block, passing effectiveApiKey
            response = await SetupAndSendStreamingRequestAsync(anthropicRequest, effectiveApiKey, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) // Catch setup/connection errors
        {
            _logger.LogError(ex, "Error setting up or sending initial streaming request to Anthropic API.");
            throw new LLMCommunicationException($"Failed to initiate Anthropic stream: {ex.Message}", ex);
        }

        // Process the stream
        try
        {
            await foreach (var chunk in ProcessAnthropicStreamAsync(response, currentModel, cancellationToken).ConfigureAwait(false))
            {
                yield return chunk;
            }
        }
        finally
        {
            response.Dispose();
        }
    }

    private async Task<HttpResponseMessage> SetupAndSendStreamingRequestAsync(
        AnthropicMessageRequest anthropicRequest,
        string effectiveApiKey,
        CancellationToken cancellationToken)
    {
        var requestUri = new Uri(_httpClient.BaseAddress!, MessagesEndpoint);

        _logger.LogDebug("Sending streaming request to Anthropic API: {Endpoint}", requestUri);

        // Create request message with streaming option
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(anthropicRequest)
        };

        // Set API key header
        requestMessage.Headers.Add("x-api-key", effectiveApiKey);

        var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
            _logger.LogError("Anthropic streaming request failed with status {Status}. Response: {ErrorContent}", response.StatusCode, errorContent);

            try
            {
                // Try to parse as JSON error first
                var errorDto = JsonSerializer.Deserialize<AnthropicErrorResponse>(errorContent);
                if (errorDto?.Error != null)
                {
                    throw new LLMCommunicationException($"Anthropic API Error ({errorDto.Error.Type}): {errorDto.Error.Message}");
                }
            }
            catch (JsonException)
            {
                // Not JSON, fall through to default message
            }

            throw new LLMCommunicationException($"Anthropic API request failed with status code {response.StatusCode}. Response: {errorContent}");
        }

        return response;
    }

    // Helper to process the actual stream content
    private async IAsyncEnumerable<ChatCompletionChunk> ProcessAnthropicStreamAsync(
        HttpResponseMessage response,
        string currentModel,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Stream? responseStream = null;
        StreamReader? reader = null;

        try
        {
            responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            reader = new StreamReader(responseStream, Encoding.UTF8);

            // Use a separate async iterator for the core processing loop
            await foreach (var chunk in ReadAndProcessAnthropicStreamLinesAsync(reader, currentModel, cancellationToken).ConfigureAwait(false))
            {
                yield return chunk;
            }
        }
        finally
        {
            // Ensure resources are disposed even if exceptions occur during processing
            reader?.Dispose();
            responseStream?.Dispose();
            // Response will be disposed by the caller
        }
    }

    // Inner async iterator to handle the loop and yield
    private async IAsyncEnumerable<ChatCompletionChunk> ReadAndProcessAnthropicStreamLinesAsync(
        StreamReader reader,
        string currentModel,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? currentMessageId = null;
        string? currentEvent = null;
        StringBuilder dataBuilder = new StringBuilder();

        // Process the SSE stream line by line
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(line))
            {
                // End of an event, process it
                if (currentEvent != null && dataBuilder.Length > 0)
                {
                    string jsonData = dataBuilder.ToString();
                    dataBuilder.Clear();

                    ChatCompletionChunk? chunk = null; // Variable to hold the chunk outside try/catch
                    try
                    {
                        // Process different event types
                        switch (currentEvent)
                        {
                            case "message_start":
                                var startEvent = JsonSerializer.Deserialize<AnthropicMessageStartEvent>(jsonData);
                                if (startEvent?.Message != null)
                                {
                                    currentMessageId = startEvent.Message.Id;
                                    _logger.LogDebug("Stream started. Message ID: {MessageId}, Model: {Model}",
                                        startEvent.Message.Id, startEvent.Message.Model ?? currentModel);
                                }
                                break;

                            case "content_block_delta":
                                var deltaEvent = JsonSerializer.Deserialize<AnthropicContentBlockDeltaEvent>(jsonData);
                                if (deltaEvent?.Delta?.Type == "text_delta" && deltaEvent.Delta.Text != null)
                                {
                                    chunk = new ChatCompletionChunk
                                    {
                                        Id = currentMessageId ?? Guid.NewGuid().ToString(),
                                        Model = currentModel,
                                        Object = "chat.completion.chunk",
                                        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                        Choices = new List<StreamingChoice>
                                        {
                                            new StreamingChoice
                                            {
                                                Index = deltaEvent.Index,
                                                Delta = new DeltaContent { Content = deltaEvent.Delta.Text },
                                                FinishReason = null
                                            }
                                        }
                                    };
                                }
                                break;

                            case "message_delta":
                                var messageDelta = JsonSerializer.Deserialize<AnthropicMessageDeltaEvent>(jsonData);
                                if (messageDelta?.Delta?.StopReason != null)
                                {
                                    chunk = new ChatCompletionChunk
                                    {
                                        Id = currentMessageId ?? Guid.NewGuid().ToString(),
                                        Model = currentModel,
                                        Object = "chat.completion.chunk",
                                        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                        Choices = new List<StreamingChoice>
                                        {
                                            new StreamingChoice
                                            {
                                                Index = 0,
                                                Delta = new DeltaContent(),
                                                FinishReason = MapFinishReason(messageDelta.Delta.StopReason)
                                            }
                                        }
                                    };
                                }
                                break;

                            case "error":
                                var errorEvent = JsonSerializer.Deserialize<AnthropicStreamErrorEvent>(jsonData);
                                if (errorEvent?.Error != null)
                                {
                                    string errorMsg = $"Anthropic stream error ({errorEvent.Error.Type}): {errorEvent.Error.Message}";
                                    _logger.LogError(errorMsg);
                                    throw new LLMCommunicationException(errorMsg);
                                }
                                break;

                            case "message_stop":
                                _logger.LogInformation("Received 'message_stop' event, ending stream processing.");
                                yield break; // End streaming
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "JSON error processing Anthropic event '{EventType}'. Data: {JsonData}", currentEvent, jsonData);
                        throw new LLMCommunicationException($"Error deserializing Anthropic stream event", jsonEx);
                    }

                    // Yield the chunk if we produced one
                    if (chunk != null)
                    {
                        yield return chunk;
                    }
                }

                // Reset for next event
                currentEvent = null;
            }
            else if (line.StartsWith("event:"))
            {
                currentEvent = line.Substring("event:".Length).Trim();
            }
            else if (line.StartsWith("data:"))
            {
                dataBuilder.Append(line.Substring("data:".Length).Trim());
            }
        }
    }

    /// <inheritdoc />
    public Task<List<string>> ListModelsAsync(string? apiKey = null, CancellationToken cancellationToken = default)
    {
        // Anthropic does not provide a public API endpoint to list models.
        // Returning a hardcoded list of known models. This may need updating manually.
        _logger.LogWarning("Anthropic does not provide an API to list models. Returning a hardcoded list.");

        var knownModels = new List<string>
        {
            "claude-3-opus-20240229",
            "claude-3-sonnet-20240229",
            "claude-3-haiku-20240307",
            "claude-2.1",
            "claude-2.0",
            "claude-instant-1.2"
        };

        // Return a completed task with the list
        return Task.FromResult(knownModels);
    }

    public Task<EmbeddingResponse> CreateEmbeddingAsync(EmbeddingRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
        => Task.FromException<EmbeddingResponse>(new NotSupportedException("Embeddings are not supported by AnthropicClient."));

    public Task<ImageGenerationResponse> CreateImageAsync(ImageGenerationRequest request, string? apiKey = null, CancellationToken cancellationToken = default)
        => Task.FromException<ImageGenerationResponse>(new NotSupportedException("Image generation is not supported by AnthropicClient."));

    private AnthropicMessageRequest MapToAnthropicRequest(ChatCompletionRequest coreRequest)
    {
        string? systemPrompt = null;
        var messages = new List<AnthropicMessage>();

        foreach (var msg in coreRequest.Messages)
        {
            if (string.IsNullOrWhiteSpace(msg.Role)) throw new ArgumentException("Message role cannot be null or empty.", nameof(coreRequest.Messages));
            if (string.IsNullOrWhiteSpace(msg.Content)) throw new ArgumentException("Message content cannot be null or empty.", nameof(coreRequest.Messages));

            if (msg.Role.Equals(MessageRole.System, StringComparison.OrdinalIgnoreCase))
            {
                systemPrompt = msg.Content; // Use the last system message
            }
            else if (msg.Role.Equals(MessageRole.User, StringComparison.OrdinalIgnoreCase) ||
                     msg.Role.Equals(MessageRole.Assistant, StringComparison.OrdinalIgnoreCase))
            {
                messages.Add(new AnthropicMessage { Role = msg.Role, Content = msg.Content });
            }
            else
            {
                _logger.LogWarning("Unsupported message role '{Role}' encountered for Anthropic provider. Skipping message.", msg.Role);
                // Or throw an exception if strict adherence is required:
                // throw new ArgumentException($"Unsupported message role '{msg.Role}' for Anthropic provider.");
            }
        }

        // Anthropic requires messages to alternate user/assistant and start with user.
        // Conduit likely handles this, but basic validation/logging can be useful.
        if (messages.Count == 0 || !messages[0].Role.Equals(MessageRole.User, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Anthropic messages should ideally start with a 'user' role. The API might reject this request.");
            // Consider throwing new ArgumentException("Anthropic requires messages to start with a 'user' role.");
        }
        // TODO: Add validation for alternating roles if needed.

        // Anthropic requires max_tokens. Use a sensible default if not provided.
        int maxTokens = coreRequest.MaxTokens ?? 4096; // Default from Anthropic docs

        return new AnthropicMessageRequest
        {
            Model = _providerModelId,
            Messages = messages,
            SystemPrompt = systemPrompt,
            MaxTokens = maxTokens,
            Temperature = (float?)coreRequest.Temperature,
            TopP = (float?)coreRequest.TopP,
            // TopK = coreRequest.TopK, // Map if added to Core model
            StopSequences = coreRequest.Stop, // Assumes coreRequest.Stop is IEnumerable<string>?
            Stream = coreRequest.Stream
            // TODO: Map Metadata if added to Core model and AnthropicModels
        };
    }

    private ChatCompletionResponse MapToCoreResponse(AnthropicMessageResponse anthropicResponse, string originalModelAlias)
    {
        // Validated in calling method: anthropicResponse, Content, Content[0].Text, Usage are not null.
        var responseContentBlock = anthropicResponse.Content?.FirstOrDefault(c => c.Type == "text");
        var responseUsage = anthropicResponse.Usage; // Already validated non-null in caller

        if (responseContentBlock == null || string.IsNullOrEmpty(responseContentBlock.Text))
        {
            // This case should ideally be caught earlier, but handle defensively
            _logger.LogError("Invalid Anthropic response structure encountered during mapping: Missing text content block.");
            throw new LLMCommunicationException("Invalid response structure received from Anthropic API (missing text content).");
        }

        var choice = new Choice
        {
            Index = 0,
            Message = new Message
            {
                // Anthropic response role should always be "assistant"
                // Ensure Role and Content are not null before assigning to required Message properties
                Role = anthropicResponse.Role ?? MessageRole.Assistant, // Default to Assistant if null
                Content = responseContentBlock.Text ?? string.Empty // Add null check with empty string default
            },
            FinishReason = MapFinishReason(anthropicResponse.StopReason) ?? string.Empty // Add null check with empty string default
        };

        return new ChatCompletionResponse
        {
            // Use null-coalescing for required string properties
            Id = anthropicResponse.Id ?? Guid.NewGuid().ToString(), // Generate ID if missing
            Object = anthropicResponse.Type ?? "message", // Default if missing
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), // Assign current timestamp as Anthropic doesn't provide it
            Model = originalModelAlias, // Return the alias the user requested
            Choices = new List<Choice> { choice },
            Usage = new Usage // Already validated non-null
            {
                PromptTokens = responseUsage?.InputTokens ?? 0, // Add null conditional and default value
                CompletionTokens = responseUsage?.OutputTokens ?? 0, // Add null conditional and default value
                TotalTokens = (responseUsage?.InputTokens ?? 0) + (responseUsage?.OutputTokens ?? 0)
            }
            // TODO: Map SystemFingerprint if added to Core response
        };
    }

    private static string? MapFinishReason(string? anthropicStopReason)
    {
        return anthropicStopReason switch
        {
            "end_turn" => "stop",
            "max_tokens" => "length",
            "stop_sequence" => "stop",
            _ => anthropicStopReason // Pass through unknown/null reasons
        };
    }

    // Helper to read error content safely
    private static async Task<string> ReadErrorContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log this? Maybe at the call site.
            return $"Failed to read error content: {ex.Message}";
        }
    }
}
