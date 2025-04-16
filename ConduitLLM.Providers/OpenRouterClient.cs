using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Providers.InternalModels; // Added for OpenAI DTOs
using System.Text; // For reading error content
using System.IO; // For StreamReader
using System.Runtime.CompilerServices; // For IAsyncEnumerable
using System.Net; // For HttpStatusCode

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with the OpenRouter API.
    /// </summary>
    public class OpenRouterClient : ILLMClient
    {
        private readonly HttpClient _httpClient;
        private readonly ProviderCredentials _credentials;
        private readonly ILogger<OpenRouterClient> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        // DTOs for OpenRouter /models response
        private class OpenRouterModel
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;
        }

        private class OpenRouterModelsResponse
        {
            [JsonPropertyName("data")]
            public List<OpenRouterModel> Data { get; set; } = new List<OpenRouterModel>();
        }

        public OpenRouterClient(ProviderCredentials credentials, ILogger<OpenRouterClient> logger)
        {
            _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Default ApiBase for OpenRouter if not provided
            string? apiBase = _credentials.ApiBase;
            if (string.IsNullOrWhiteSpace(apiBase))
            {
                _logger.LogWarning("ApiBase not found in credentials for OpenRouter, using default: https://openrouter.ai/api/v1/");
                apiBase = "https://openrouter.ai/api/v1/";
            }

            _httpClient = new HttpClient { BaseAddress = new Uri(apiBase.TrimEnd('/') + "/") };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public string ProviderName => "openrouter";

        // --- Chat Completion (Non-Streaming) ---
        public async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : _credentials.ApiKey!;
            if (string.IsNullOrWhiteSpace(effectiveApiKey)) throw new ConfigurationException($"API key is missing for provider '{ProviderName}'.");

            _logger.LogInformation("OpenRouterClient: Mapping Core request for model alias '{ModelAlias}'", request.Model);
            var openRouterRequest = MapToOpenRouterRequest(request, request.Model);

            HttpResponseMessage? response = null;
            try
            {
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
                requestMessage.Content = JsonContent.Create(openRouterRequest, options: _jsonOptions);
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effectiveApiKey);
                requestMessage.Headers.Add("HTTP-Referer", "https://conduit-llm.com"); // Replace with actual URL
                requestMessage.Headers.Add("X-Title", "ConduitLLM"); // Replace with actual app name

                _logger.LogDebug("OpenRouterClient: Sending request to {Endpoint}", requestMessage.RequestUri);
                response = await _httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
                    _logger.LogError("OpenRouterClient: API request failed: {StatusCode}. Response: {ErrorContent}", response.StatusCode, errorContent);
                    throw new LLMCommunicationException($"OpenRouter API request failed: {response.ReasonPhrase} ({response.StatusCode})", response.StatusCode, errorContent);
                }

                _logger.LogDebug("OpenRouterClient: Received successful response.");
                var openAICompatibleResponse = await response.Content.ReadFromJsonAsync<OpenAIChatCompletionResponse>(_jsonOptions, cancellationToken: cancellationToken).ConfigureAwait(false);

                if (openAICompatibleResponse == null) throw new LLMCommunicationException("Failed to deserialize response from OpenRouter API.");
                if (openAICompatibleResponse.Choices == null || !openAICompatibleResponse.Choices.Any() || openAICompatibleResponse.Choices[0].Message == null) throw new LLMCommunicationException("Invalid response structure (missing choices/message).");
                if (openAICompatibleResponse.Usage == null) throw new LLMCommunicationException("Invalid response structure (missing usage).");

                _logger.LogInformation("OpenRouterClient: Mapping response for model alias '{ModelAlias}'", request.Model);
                return MapToCoreResponse(openAICompatibleResponse, request.Model);
            }
            catch (JsonException ex) { throw new LLMCommunicationException("Error deserializing OpenRouter response.", HttpStatusCode.InternalServerError, ex.Message, ex); }
            catch (HttpRequestException ex) { throw new LLMCommunicationException($"HTTP request error: {ex.Message}", HttpStatusCode.ServiceUnavailable, ex.Message, ex); }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException) { throw new LLMCommunicationException("Request timed out.", HttpStatusCode.RequestTimeout, ex.Message, ex); }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested) { throw; } // Re-throw cancellation
            catch (Exception ex) when (ex is not LLMCommunicationException and not ConfigurationException) { throw new ConduitException($"Unexpected error: {ex.Message}", ex); }
            finally { response?.Dispose(); }
        }

        // --- Chat Completion (Streaming) ---
        public async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            string effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : _credentials.ApiKey!;
            if (string.IsNullOrWhiteSpace(effectiveApiKey)) throw new ConfigurationException($"API key is missing for provider '{ProviderName}'.");

            _logger.LogInformation("OpenRouterClient: Mapping Core request for streaming model alias '{ModelAlias}'", request.Model);
            var openRouterRequest = MapToOpenRouterRequest(request, request.Model) with { Stream = true };

            HttpResponseMessage response;
            try
            {
                response = await SetupAndSendStreamingRequestAsync(openRouterRequest, effectiveApiKey, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenRouterClient: Error setting up streaming request.");
                throw new LLMCommunicationException($"Failed to initiate stream: {ex.Message}", HttpStatusCode.InternalServerError, ex.Message, ex);
            }

            // Iterate over the helper that handles processing and disposal
            await foreach (var chunk in ProcessStreamChunksAsync(response, request.Model, cancellationToken).ConfigureAwait(false))
            {
                 yield return chunk;
            }
        }

        // --- Model Listing ---
        public async Task<List<string>> ListModelsAsync(string? apiKey = null, CancellationToken cancellationToken = default)
        {
            string effectiveApiKey = apiKey ?? _credentials.ApiKey ?? string.Empty;
            _logger.LogInformation("OpenRouterClient: Listing models using API Base: {ApiBase}", _httpClient.BaseAddress);

            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, "models");
            if (!string.IsNullOrWhiteSpace(effectiveApiKey)) requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effectiveApiKey);
            else _logger.LogWarning("OpenRouterClient: No API key available for ListModelsAsync.");
            requestMessage.Headers.Add("HTTP-Referer", "https://conduit-llm.com");
            requestMessage.Headers.Add("X-Title", "ConduitLLM");

            HttpResponseMessage? response = null;
            try
            {
                response = await _httpClient.SendAsync(requestMessage, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("OpenRouter API list models failed: {StatusCode}. Response: {Response}", response.StatusCode, errorContent);
                    throw new LLMCommunicationException($"OpenRouter list models failed: {response.ReasonPhrase} ({response.StatusCode})", response.StatusCode, errorContent);
                }

                var modelsResponse = await response.Content.ReadFromJsonAsync<OpenRouterModelsResponse>(_jsonOptions, cancellationToken);
                if (modelsResponse?.Data == null)
                {
                    _logger.LogWarning("OpenRouter API returned null/empty data for models.");
                    return new List<string>();
                }

                var modelIds = modelsResponse.Data.Select(m => m.Id).Where(id => !string.IsNullOrEmpty(id)).ToList();
                _logger.LogInformation("OpenRouterClient: Retrieved {Count} models.", modelIds.Count);
                return modelIds;
            }
            catch (JsonException ex) { throw new LLMCommunicationException("Failed to parse models response.", HttpStatusCode.InternalServerError, ex.Message, ex); }
            catch (HttpRequestException ex) { throw new LLMCommunicationException($"Network error listing models: {ex.Message}", HttpStatusCode.ServiceUnavailable, ex.Message, ex); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is not LLMCommunicationException and not ConfigurationException) { throw new ConduitException($"Unexpected error listing models: {ex.Message}", ex); }
            finally { response?.Dispose(); }
        }

        // --- Helper Methods ---

        private OpenAIChatCompletionRequest MapToOpenRouterRequest(ChatCompletionRequest coreRequest, string providerModelId) => new()
        {
            Model = providerModelId,
            Messages = coreRequest.Messages.Select(m => new OpenAIMessage
            {
                Role = m.Role ?? throw new ArgumentNullException(nameof(m.Role)),
                Content = m.Content ?? throw new ArgumentNullException(nameof(m.Content))
            }).ToList(),
            Temperature = (float?)coreRequest.Temperature,
            MaxTokens = coreRequest.MaxTokens,
        };

        private ChatCompletionResponse MapToCoreResponse(OpenAIChatCompletionResponse openAIResponse, string originalModelAlias)
        {
            var choice = openAIResponse.Choices?.FirstOrDefault();
            var message = choice?.Message;
            var usage = openAIResponse.Usage;
            if (choice == null || message == null || usage == null) throw new LLMCommunicationException("Invalid response structure.");

            return new ChatCompletionResponse
            {
                Id = openAIResponse.Id ?? Guid.NewGuid().ToString(),
                Object = openAIResponse.Object ?? "chat.completion",
                Created = openAIResponse.Created ?? 0,
                Model = originalModelAlias,
                Choices = new List<Choice> { new() {
                    Index = choice.Index,
                    Message = new Message { Role = message.Role!, Content = message.Content! },
                    FinishReason = choice.FinishReason ?? ""
                }},
                Usage = new Usage { PromptTokens = usage.PromptTokens, CompletionTokens = usage.CompletionTokens, TotalTokens = usage.TotalTokens }
            };
        }

        private ChatCompletionChunk MapToCoreChunk(OpenAIChatCompletionChunk openAIChunk, string originalModelAlias) => new()
        {
            Id = openAIChunk.Id,
            Object = openAIChunk.Object ?? "chat.completion.chunk",
            Created = openAIChunk.Created,
            Model = originalModelAlias,
            Choices = openAIChunk.Choices.Select(c => new StreamingChoice {
                Index = c.Index,
                Delta = new DeltaContent { Role = c.Delta.Role, Content = c.Delta.Content },
                FinishReason = c.FinishReason
            }).ToList()
        };

        private async Task<HttpResponseMessage> SetupAndSendStreamingRequestAsync(
            OpenAIChatCompletionRequest openRouterRequest, string effectiveApiKey, CancellationToken cancellationToken)
        {
            // Caller manages disposal
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            requestMessage.Content = JsonContent.Create(openRouterRequest, options: _jsonOptions);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effectiveApiKey);
            requestMessage.Headers.Add("HTTP-Referer", "https://conduit-llm.com");
            requestMessage.Headers.Add("X-Title", "ConduitLLM");

            _logger.LogDebug("OpenRouterClient: Sending streaming request.");
            HttpResponseMessage response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
                _logger.LogError("OpenRouterClient: API streaming request failed: {StatusCode}. Response: {ErrorContent}", response.StatusCode, errorContent);
                response.Dispose();
                throw new LLMCommunicationException($"OpenRouter API streaming request failed: {response.ReasonPhrase} ({response.StatusCode})", response.StatusCode, errorContent);
            }
            _logger.LogDebug("OpenRouterClient: Received successful streaming response header.");
            return response;
        }

        // Handles resource disposal and calls the internal enumerator
        private async IAsyncEnumerable<ChatCompletionChunk> ProcessStreamChunksAsync(
            HttpResponseMessage response,
            string originalModelAlias,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Stream? responseStream = null;
            StreamReader? reader = null;
            try
            {
                responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                reader = new StreamReader(responseStream, Encoding.UTF8);

                // Iterate the internal helper that does the actual yielding
                await foreach (var chunk in EnumerateChunksInternalAsync(reader, originalModelAlias, cancellationToken).ConfigureAwait(false))
                {
                    yield return chunk; // Yield the chunk from the internal helper
                }
            }
            // IO/Json exceptions from the internal helper will propagate here if not caught there
            // OperationCanceledException propagates naturally
            finally
            {
                // Ensure disposal happens after iteration completes or if an exception occurs
                reader?.Dispose();
                responseStream?.Dispose();
                response.Dispose();
                _logger.LogDebug("OpenRouterClient: Disposed stream resources in ProcessStreamChunksAsync.");
            }
        }

        // Internal helper containing the core loop and yield return
        // This method has NO try/catch/finally itself to avoid CS1626
        private async IAsyncEnumerable<ChatCompletionChunk> EnumerateChunksInternalAsync(
             StreamReader reader,
             string originalModelAlias,
             [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                // Let IO/ObjectDisposed exceptions propagate up to ProcessStreamChunksAsync's finally block
                string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested) yield break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.StartsWith("data:"))
                {
                    string jsonData = line.Substring("data:".Length).Trim();
                    if (jsonData.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("OpenRouterClient: Received [DONE] marker.");
                        yield break;
                    }

                    ChatCompletionChunk? coreChunk = null;
                    try // Inner try specifically for JSON parsing - this is allowed
                    {
                        var openAIChunk = JsonSerializer.Deserialize<OpenAIChatCompletionChunk>(jsonData, _jsonOptions);
                        if (openAIChunk != null) coreChunk = MapToCoreChunk(openAIChunk, originalModelAlias);
                        else _logger.LogWarning("OpenRouterClient: Deserialized stream chunk was null. JSON: {JsonData}", jsonData);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "OpenRouterClient: JSON deserialization error. JSON: {JsonData}", jsonData);
                        // Let the exception propagate to terminate the stream? Or log and continue? Propagate for now.
                        throw new LLMCommunicationException($"Error deserializing stream chunk: {ex.Message}. Data: {jsonData}", HttpStatusCode.InternalServerError, ex.Message, ex);
                    }

                    // Yield is outside the JSON try-catch, and this method has no outer try-catch/finally
                    if (coreChunk != null)
                    {
                        yield return coreChunk;
                    }
                }
                else if (!line.StartsWith(":"))
                {
                    _logger.LogTrace("OpenRouterClient: Skipping non-data line: {Line}", line);
                }
            }
            _logger.LogInformation("OpenRouterClient: Finished enumerating stream chunks.");
        }


        private static async Task<string> ReadErrorContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            try { return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false); }
            catch (Exception ex) { return $"Failed to read error content: {ex.Message}"; }
        }
    }
}
