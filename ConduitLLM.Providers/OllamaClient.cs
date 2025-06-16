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

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Revised client for interacting with self-hosted Ollama APIs using the new client hierarchy.
    /// Provides standardized handling of API requests and responses for LLM model inference.
    /// </summary>
    public class OllamaClient : CustomProviderClient
    {
        // Default base URL for local Ollama instance
        private const string DefaultOllamaApiBase = "http://localhost:11434";

        /// <summary>
        /// Initializes a new instance of the <see cref="OllamaClient"/> class.
        /// </summary>
        /// <param name="credentials">The credentials for accessing the Ollama API.</param>
        /// <param name="providerModelId">The model identifier to use (e.g., llama3:latest).</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">The HTTP client factory for creating HttpClient instances.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public OllamaClient(
            ProviderCredentials credentials,
            string providerModelId,
            ILogger logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                credentials,
                providerModelId,
                logger,
                httpClientFactory,
                "Ollama",
                string.IsNullOrWhiteSpace(credentials.ApiBase) ? DefaultOllamaApiBase : credentials.ApiBase,
                defaultModels)
        {
            if (string.IsNullOrWhiteSpace(credentials.ApiBase))
            {
                Logger.LogInformation("Ollama API base not provided, defaulting to {DefaultBase}", DefaultOllamaApiBase);
            }
        }

        /// <inheritdoc/>
        protected override void ValidateCredentials()
        {
            // Ollama typically doesn't require API key, so we override the base validation
            // to skip the API key check but ensure other validations are performed

            // We still need valid credentials, but API key is optional
            if (Credentials == null)
            {
                throw new ConfigurationException($"Credentials cannot be null for provider '{ProviderName}'");
            }

            // No need to check API key for Ollama
        }

        /// <inheritdoc/>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            // Customize configuration for Ollama - no Authorization header needed
            // but keep other standard headers
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "ConduitLLM");

            // Set the base address if not already set
            if (client.BaseAddress == null && !string.IsNullOrEmpty(BaseUrl))
            {
                client.BaseAddress = new Uri(BaseUrl.TrimEnd('/'));
            }
        }

        /// <inheritdoc/>
        public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateChatCompletionAsync");

            Logger.LogInformation("Mapping Core request to Ollama request for model '{Model}'", ProviderModelId);
            var ollamaRequest = MapToOllamaChatRequest(request);

            try
            {
                return await ExecuteApiRequestAsync(
                    async () =>
                    {
                        using var client = CreateHttpClient();
                        var requestUri = "api/chat";
                        Logger.LogDebug("Sending request to Ollama endpoint: {Endpoint}", requestUri);

                        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
                        {
                            Content = JsonContent.Create(ollamaRequest, options: new JsonSerializerOptions
                            {
                                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                            })
                        };

                        var response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
                            Logger.LogError("Ollama API request failed with status code {StatusCode}. Response: {ErrorContent}",
                                response.StatusCode, errorContent);
                            throw new LLMCommunicationException(
                                $"Ollama API request failed with status code {response.StatusCode}. Response: {errorContent}");
                        }

                        Logger.LogDebug("Received successful response from Ollama API.");
                        var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
                            cancellationToken: cancellationToken).ConfigureAwait(false);

                        if (ollamaResponse == null || !ollamaResponse.Done || ollamaResponse.Message == null)
                        {
                            Logger.LogError("Failed to deserialize or received incomplete response from Ollama API.");
                            throw new LLMCommunicationException("Invalid or incomplete response structure received from Ollama API.");
                        }

                        Logger.LogInformation("Mapping Ollama response back to Core response for model '{Model}'", request.Model);
                        return MapToCoreChatResponse(ollamaResponse, request.Model);
                    },
                    "CreateChatCompletionAsync",
                    cancellationToken);
            }
            catch (LLMCommunicationException)
            {
                // Re-throw LLMCommunicationException directly
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred while processing Ollama chat completion.");
                throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "StreamChatCompletionAsync");

            Logger.LogInformation($"Mapping Core request to Ollama streaming request for model '{ProviderModelId}'");
            var ollamaRequest = MapToOllamaChatRequest(request) with { Stream = true }; // Ensure streaming is enabled

            // We need to track if we've sent any chunks yet for proper assistant message formatting
            bool isFirstChunk = true;

            // Get the data from the API
            List<string> allLines = await FetchStreamLinesAsync(ollamaRequest, cancellationToken);

            // Process each line and yield chunks outside any try blocks
            foreach (var line in allLines)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                // Parse the line
                OllamaStreamChunk? ollamaChunk = null;
                try
                {
                    ollamaChunk = JsonSerializer.Deserialize<OllamaStreamChunk>(line);
                }
                catch (JsonException ex)
                {
                    Logger.LogError(ex, "Error deserializing Ollama stream chunk: {Line}", line);
                    continue; // Skip problematic lines
                }

                // Skip null chunks
                if (ollamaChunk == null)
                {
                    Logger.LogWarning($"Deserialized Ollama stream chunk was null. JSON: {line}");
                    continue;
                }

                // Only yield if we have actual content
                if (ollamaChunk.Message != null && !string.IsNullOrEmpty(ollamaChunk.Message.Content))
                {
                    var chunk = MapToCoreStreamChunk(ollamaChunk, request.Model, isFirstChunk);
                    isFirstChunk = false;
                    yield return chunk;
                }

                // If this is the final chunk, yield a chunk with the finish reason
                if (ollamaChunk.Done && !isFirstChunk) // Avoid empty final chunk if we've had no content
                {
                    Logger.LogInformation("Received 'done' marker in Ollama stream, ending stream processing.");
                    yield return CreateChatCompletionChunk(
                        string.Empty,
                        request.Model,
                        false,
                        "stop",
                        request.Model);
                    break;
                }
            }

            Logger.LogInformation("Finished processing Ollama stream.");
        }

        /// <summary>
        /// Fetches all lines from the Ollama streaming API.
        /// </summary>
        /// <param name="ollamaRequest">The request to send to Ollama.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A list of all JSON lines from the stream.</returns>
        private async Task<List<string>> FetchStreamLinesAsync(
            OllamaChatRequest ollamaRequest,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage? response = null;
            StreamReader? reader = null;
            Stream? responseStream = null;
            List<string> lines = new List<string>();

            try
            {
                // Setup and send the initial request
                using var client = CreateHttpClient();
                var requestUri = "api/chat";
                Logger.LogDebug($"Sending streaming request to Ollama endpoint: {requestUri}");

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = JsonContent.Create(ollamaRequest, options: new JsonSerializerOptions
                    {
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    })
                };

                // Send request with ResponseHeadersRead to enable streaming
                response = await client.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
                    Logger.LogError($"Ollama API streaming request failed with status code {response.StatusCode}. Response: {errorContent}");
                    throw new LLMCommunicationException(
                        $"Ollama API streaming request failed with status code {response.StatusCode}. Response: {errorContent}");
                }

                responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                reader = new StreamReader(responseStream, Encoding.UTF8);

                // Read all lines from the stream
                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    lines.Add(line);
                }

                return lines;
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("Ollama streaming operation was canceled.");
                throw;
            }
            catch (LLMCommunicationException)
            {
                // Re-throw LLMCommunicationException directly
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred during Ollama stream processing.");
                throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
            }
            finally
            {
                // Ensure resources are disposed
                reader?.Dispose();
                responseStream?.Dispose();
                response?.Dispose();
                Logger.LogDebug("Disposed Ollama stream resources.");
            }
        }

        /// <inheritdoc/>
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteApiRequestAsync(
                    async () =>
                    {
                        using var client = CreateHttpClient();
                        var requestUri = "api/tags";
                        Logger.LogDebug("Sending request to list Ollama models from: {Endpoint}", requestUri);

                        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
                        var response = await client.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
                            Logger.LogError("Ollama API list models request failed with status code {StatusCode}. Response: {ErrorContent}",
                                response.StatusCode, errorContent);
                            throw new LLMCommunicationException(
                                $"Ollama API list models request failed with status code {response.StatusCode}. Response: {errorContent}");
                        }

                        var tagsResponse = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(
                            cancellationToken: cancellationToken).ConfigureAwait(false);

                        if (tagsResponse == null || tagsResponse.Models == null)
                        {
                            Logger.LogError("Failed to deserialize the successful model list response from Ollama API.");
                            throw new LLMCommunicationException("Failed to deserialize the model list response from Ollama API.");
                        }

                        // Map to ModelInfo objects using the Create factory method
                        var models = tagsResponse.Models.Select(m =>
                            ExtendedModelInfo.Create(m.Name, ProviderName, m.Name)
                                .WithCapabilities(DetermineModelCapabilities(m))
                                .WithTokenLimits(new ModelTokenLimits
                                {
                                    // Ollama doesn't provide token limits in API response
                                    // Use sensible defaults based on family/parameter size
                                    MaxInputTokens = EstimateMaxInputTokens(m),
                                    MaxOutputTokens = EstimateMaxOutputTokens(m)
                                })
                        ).ToList();

                        Logger.LogInformation($"Successfully retrieved {models.Count} models from Ollama.");
                        return models;
                    },
                    "GetModelsAsync",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred while listing Ollama models.");
                throw new LLMCommunicationException($"An unexpected error occurred while listing models: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public override async Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateEmbeddingAsync");

            // Input validation: Ollama expects a single string prompt
            if (request.Input == null)
            {
                throw new ValidationException("Embedding input cannot be null for Ollama embedding request");
            }

            // Convert input to string
            string prompt;
            if (request.Input is string promptStr)
            {
                prompt = promptStr;
            }
            else if (request.Input is IEnumerable<string> stringList)
            {
                // Join multiple strings if provided as a list (use first one for compatibility)
                var strings = stringList.ToList();
                if (strings.Count == 0)
                {
                    throw new ValidationException("Embedding input list cannot be empty for Ollama embedding request");
                }
                prompt = strings[0];
                if (strings.Count > 1)
                {
                    Logger.LogWarning("Ollama embedding only supports a single string input. Using first item from the list.");
                }
            }
            else
            {
                throw new ValidationException("Unsupported input type for Ollama embedding request");
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ValidationException("Embedding prompt cannot be empty for Ollama embedding request");
            }

            Logger.LogInformation("Mapping Core embedding request to Ollama request for model '{Model}'", ProviderModelId);
            var ollamaRequest = new OllamaEmbeddingRequest
            {
                Model = ProviderModelId,
                Prompt = prompt
            };

            try
            {
                return await ExecuteApiRequestAsync(
                    async () =>
                    {
                        using var client = CreateHttpClient();
                        var requestUri = "api/embeddings";
                        Logger.LogDebug("Sending request to Ollama embeddings endpoint: {Endpoint}", requestUri);

                        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
                        {
                            Content = JsonContent.Create(ollamaRequest, options: new JsonSerializerOptions
                            {
                                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                            })
                        };

                        var response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            string errorContent = await ReadErrorContentAsync(response, cancellationToken).ConfigureAwait(false);
                            Logger.LogError("Ollama API embeddings request failed with status code {StatusCode}. Response: {ErrorContent}",
                                response.StatusCode, errorContent);
                            throw new LLMCommunicationException(
                                $"Ollama API embeddings request failed with status code {response.StatusCode}. Response: {errorContent}");
                        }

                        Logger.LogDebug("Received successful response from Ollama embeddings API.");
                        var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(
                            cancellationToken: cancellationToken).ConfigureAwait(false);

                        if (ollamaResponse == null || ollamaResponse.Embedding == null)
                        {
                            Logger.LogError("Failed to deserialize or received incomplete embeddings response from Ollama API.");
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
                                    Embedding = ollamaResponse.Embedding
                                }
                            },
                            Model = request.Model ?? ProviderModelId, // Use original requested model alias or fallback to provider model ID
                            Usage = new Usage
                            {
                                PromptTokens = EstimateTokenCount(prompt),
                                CompletionTokens = 0,
                                TotalTokens = EstimateTokenCount(prompt)
                            }
                        };
                    },
                    "CreateEmbeddingAsync",
                    cancellationToken);
            }
            catch (ValidationException)
            {
                // Re-throw validation exceptions directly
                throw;
            }
            catch (LLMCommunicationException)
            {
                // Re-throw LLMCommunicationException directly
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred while creating Ollama embeddings.");
                throw new LLMCommunicationException($"An unexpected error occurred while creating embeddings: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public override Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            Logger.LogWarning("Ollama does not support image generation.");
            return Task.FromException<ImageGenerationResponse>(
                new NotSupportedException("Image generation is not supported by Ollama."));
        }

        #region Helper Methods

        private OllamaChatRequest MapToOllamaChatRequest(ChatCompletionRequest coreRequest)
        {
            // Map options - only map supported ones from Core request
            OllamaOptions? options = null;
            if (coreRequest.Temperature.HasValue || coreRequest.MaxTokens.HasValue || coreRequest.TopP.HasValue ||
                (coreRequest.Stop != null && coreRequest.Stop.Any()))
            {
                options = new OllamaOptions
                {
                    Temperature = (float?)coreRequest.Temperature,
                    NumPredict = coreRequest.MaxTokens,
                    TopP = (float?)coreRequest.TopP,
                    Stop = coreRequest.Stop?.ToList()
                };
            }

            return new OllamaChatRequest
            {
                Model = ProviderModelId,
                Messages = coreRequest.Messages.Select(m => new OllamaMessage
                {
                    Role = m.Role ?? throw new ArgumentNullException(nameof(m.Role), "Message role cannot be null"),
                    Content = ConduitLLM.Providers.Helpers.ContentHelper.GetContentAsString(m.Content)
                }).ToList(),
                Options = options,
                Stream = false // Will be set to true for streaming
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
                Created = DateTimeOffset.TryParse(ollamaResponse.CreatedAt, out var createdAt) ?
                    createdAt.ToUnixTimeSeconds() : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = originalModelAlias,
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new Message
                        {
                            Role = ollamaResponse.Message?.Role ?? "assistant",
                            Content = ollamaResponse.Message?.Content ?? string.Empty
                        },
                        FinishReason = "stop"
                    }
                },
                Usage = usage,
                OriginalModelAlias = originalModelAlias
            };
        }

        private ChatCompletionChunk MapToCoreStreamChunk(
            OllamaStreamChunk ollamaChunk,
            string originalModelAlias,
            bool isFirstChunk)
        {
            string? role = isFirstChunk ? ollamaChunk.Message?.Role : null;
            string? content = ollamaChunk.Message?.Content ?? string.Empty;
            string? finishReason = ollamaChunk.Done ? "stop" : null;

            return CreateChatCompletionChunk(
                content,
                originalModelAlias,
                isFirstChunk,
                finishReason,
                originalModelAlias);
        }

        private ModelCapabilities DetermineModelCapabilities(OllamaModelInfo modelInfo)
        {
            var capabilities = new ModelCapabilities
            {
                Chat = true, // Most Ollama models support chat
                TextGeneration = true,
                Embeddings = true, // Most Ollama models support embeddings
                ImageGeneration = false, // Ollama doesn't support image generation
                Vision = false // Default to false
            };

            // Check family for vision capabilities
            if (modelInfo.Details?.Families != null)
            {
                var families = modelInfo.Details.Families
                    .Concat(new[] { modelInfo.Details.Family })
                    .Select(f => f.ToLowerInvariant());

                // Models with vision support typically have these in family
                if (families.Any(f => f.Contains("vision") ||
                    f.Contains("clip") ||
                    f.Contains("llava") ||
                    f.Contains("multimodal")))
                {
                    capabilities.Vision = true;
                }
            }

            return capabilities;
        }

        private int EstimateMaxInputTokens(OllamaModelInfo modelInfo)
        {
            // Estimate based on model family/param size
            if (modelInfo.Details?.ParameterSize != null)
            {
                // Extract parameter size and convert to numeric value if possible
                if (modelInfo.Details.ParameterSize.EndsWith("B", StringComparison.OrdinalIgnoreCase))
                {
                    if (float.TryParse(modelInfo.Details.ParameterSize.Substring(0, modelInfo.Details.ParameterSize.Length - 1), out float sizeInBillions))
                    {
                        // Rough estimate based on parameter size
                        if (sizeInBillions >= 70) return 32000; // Very large models like Llama 70B
                        if (sizeInBillions >= 30) return 16000; // Large models
                        if (sizeInBillions >= 13) return 8000;  // Medium-large models
                        if (sizeInBillions >= 7) return 4000;   // Medium models
                        return 2000;                            // Small models
                    }
                }
            }

            // Check family for context size estimation
            var family = modelInfo.Details?.Family.ToLowerInvariant() ?? "";

            if (family.Contains("llama3"))
                return 8000;
            if (family.Contains("llama2"))
                return 4000;
            if (family.Contains("mistral"))
                return 8000;
            if (family.Contains("mixtral"))
                return 32000;

            // Default for unknown models
            return 4000;
        }

        private int EstimateMaxOutputTokens(OllamaModelInfo modelInfo)
        {
            // Output tokens are typically a fraction of input tokens
            return EstimateMaxInputTokens(modelInfo) / 2;
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
}
