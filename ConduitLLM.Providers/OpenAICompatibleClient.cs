using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;

using Microsoft.Extensions.Logging;
// Use aliases to avoid ambiguities
using CoreModels = ConduitLLM.Core.Models;
using CoreUtils = ConduitLLM.Core.Utilities;
using InternalModels = ConduitLLM.Providers.InternalModels;
using OpenAIModels = ConduitLLM.Providers.InternalModels.OpenAIModels;
using ConduitLLM.Providers.Utilities;
using ProviderHelpers = ConduitLLM.Providers.Helpers;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Base class for LLM clients that implement OpenAI-compatible APIs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This abstract class serves as a foundation for providers that implement APIs compatible 
    /// with the OpenAI API format, such as OpenAI, Azure OpenAI, Mistral AI, Groq, and others.
    /// </para>
    /// <para>
    /// It provides a standardized implementation of the <see cref="ILLMClient"/> interface 
    /// with customizable methods that derived classes can override to accommodate provider-specific
    /// behaviors while maintaining a consistent interface.
    /// </para>
    /// <para>
    /// Key features:
    /// - Standard implementations for chat completions, embeddings, and image generation
    /// - Consistent error handling and response mapping
    /// - Support for streaming responses
    /// - Extensibility through virtual methods
    /// - Comprehensive logging and diagnostics
    /// </para>
    /// </remarks>
    public abstract class OpenAICompatibleClient : BaseLLMClient
    {
        /// <summary>
        /// Gets the base URL for API requests.
        /// </summary>
        protected readonly string BaseUrl;

        /// <summary>
        /// Gets the static list of fallback models for providers where the models endpoint may not be available.
        /// </summary>
        /// <remarks>
        /// This is used when the provider doesn't support the standard /models endpoint or when it fails.
        /// Derived classes can override <see cref="GetFallbackModels"/> to provide provider-specific lists.
        /// </remarks>
        protected static readonly Dictionary<string, List<InternalModels.ExtendedModelInfo>> ProviderFallbackModels = new()
        {
            ["openai"] = new List<InternalModels.ExtendedModelInfo>
            {
                // GPT-4 Vision models
                CreateVisionCapableModel("gpt-4o", "openai", "gpt-4o"),
                CreateVisionCapableModel("gpt-4-vision", "openai", "gpt-4-vision"),
                CreateVisionCapableModel("gpt-4-vision-preview", "openai", "gpt-4-vision-preview"),
                CreateVisionCapableModel("gpt-4-turbo", "openai", "gpt-4-turbo"),
                
                // Standard GPT models
                InternalModels.ExtendedModelInfo.Create("gpt-4", "openai", "gpt-4"),
                InternalModels.ExtendedModelInfo.Create("gpt-3.5-turbo", "openai", "gpt-3.5-turbo"),
                
                // Embeddings
                InternalModels.ExtendedModelInfo.Create("text-embedding-3-large", "openai", "text-embedding-3-large"),
                InternalModels.ExtendedModelInfo.Create("text-embedding-3-small", "openai", "text-embedding-3-small"),
                
                // Image generation
                InternalModels.ExtendedModelInfo.Create("dall-e-3", "openai", "dall-e-3")
            },
            ["mistral"] = new List<InternalModels.ExtendedModelInfo>
            {
                InternalModels.ExtendedModelInfo.Create("mistral-tiny", "mistral", "mistral-tiny"),
                InternalModels.ExtendedModelInfo.Create("mistral-small", "mistral", "mistral-small"),
                InternalModels.ExtendedModelInfo.Create("mistral-medium", "mistral", "mistral-medium"),
                InternalModels.ExtendedModelInfo.Create("mistral-large-latest", "mistral", "mistral-large-latest"),
                InternalModels.ExtendedModelInfo.Create("mistral-embed", "mistral", "mistral-embed")
            },
            ["groq"] = new List<InternalModels.ExtendedModelInfo>
            {
                InternalModels.ExtendedModelInfo.Create("llama3-8b-8192", "groq", "llama3-8b-8192"),
                InternalModels.ExtendedModelInfo.Create("llama3-70b-8192", "groq", "llama3-70b-8192"),
                InternalModels.ExtendedModelInfo.Create("llama2-70b-4096", "groq", "llama2-70b-4096"),
                InternalModels.ExtendedModelInfo.Create("mixtral-8x7b-32768", "groq", "mixtral-8x7b-32768"),
                InternalModels.ExtendedModelInfo.Create("gemma-7b-it", "groq", "gemma-7b-it")
            }
        };

        /// <summary>
        /// Creates an ExtendedModelInfo with vision capability set to true.
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <param name="provider">The provider name</param>
        /// <param name="displayName">The display name (optional)</param>
        /// <returns>An ExtendedModelInfo with vision capability</returns>
        private static InternalModels.ExtendedModelInfo CreateVisionCapableModel(string id, string provider, string? displayName = null)
        {
            var modelInfo = InternalModels.ExtendedModelInfo.Create(id, provider, displayName ?? id);

            // Set the vision capability
            if (modelInfo.Capabilities == null)
            {
                modelInfo.Capabilities = new InternalModels.ModelCapabilities();
            }

            modelInfo.Capabilities.Vision = true;

            return modelInfo;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAICompatibleClient"/> class.
        /// </summary>
        /// <param name="credentials">The provider credentials.</param>
        /// <param name="providerModelId">The provider's model identifier.</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory.</param>
        /// <param name="providerName">The name of this LLM provider.</param>
        /// <param name="baseUrl">The base URL for API requests.</param>
        protected OpenAICompatibleClient(
            ProviderCredentials credentials,
            string providerModelId,
            ILogger logger,
            IHttpClientFactory? httpClientFactory = null,
            string? providerName = null,
            string? baseUrl = null,
            ProviderDefaultModels? defaultModels = null)
            : base(credentials, providerModelId, logger, httpClientFactory, providerName, defaultModels)
        {
            BaseUrl = baseUrl ?? "https://api.openai.com/v1";
        }

        /// <summary>
        /// Creates a chat completion using the OpenAI-compatible API.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A chat completion response.</returns>
        /// <remarks>
        /// This implementation:
        /// <list type="bullet">
        /// <item>Validates the request for required parameters</item>
        /// <item>Maps the generic request to the OpenAI format</item>
        /// <item>Sends the request to the provider's API</item>
        /// <item>Maps the response back to the generic format</item>
        /// <item>Handles errors in a standardized way</item>
        /// </list>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        /// <exception cref="ValidationException">Thrown when the request fails validation.</exception>
        /// <exception cref="LLMCommunicationException">Thrown when there is a communication error with the provider.</exception>
        /// <exception cref="ConfigurationException">Thrown when there is a configuration error.</exception>
        public override async Task<CoreModels.ChatCompletionResponse> CreateChatCompletionAsync(
            CoreModels.ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "ChatCompletion");

            return await ExecuteApiRequestAsync(async () =>
            {
                using var client = CreateHttpClient(apiKey);
                var openAiRequest = MapToOpenAIRequest(request);

                var endpoint = GetChatCompletionEndpoint();

                Logger.LogDebug("Sending chat completion request to {Provider} at {Endpoint}", ProviderName, endpoint);

                // Use our common HTTP client helper to send the request
                var openAiResponse = await CoreUtils.HttpClientHelper.SendJsonRequestAsync<object, OpenAIModels.OpenAIChatCompletionResponse>(
                    client,
                    HttpMethod.Post,
                    endpoint,
                    openAiRequest,
                    CreateStandardHeaders(apiKey),
                    DefaultJsonOptions,
                    Logger,
                    cancellationToken);

                return MapFromOpenAIResponse(openAiResponse, request.Model);
            }, "ChatCompletion", cancellationToken);
        }

        /// <summary>
        /// Streams a chat completion using the OpenAI-compatible API.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable of chat completion chunks.</returns>
        /// <remarks>
        /// This implementation:
        /// <list type="bullet">
        /// <item>Validates the request for required parameters</item>
        /// <item>Maps the generic request to the OpenAI format, forcing the stream parameter to true</item>
        /// <item>Establishes a streaming connection to the provider's API</item>
        /// <item>Processes the server-sent events (SSE) format</item>
        /// <item>Maps each chunk back to the generic format</item>
        /// <item>Handles errors in a standardized way</item>
        /// </list>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        /// <exception cref="ValidationException">Thrown when the request fails validation.</exception>
        /// <exception cref="LLMCommunicationException">Thrown when there is a communication error with the provider.</exception>
        /// <exception cref="ConfigurationException">Thrown when there is a configuration error.</exception>
        public override async IAsyncEnumerable<CoreModels.ChatCompletionChunk> StreamChatCompletionAsync(
            CoreModels.ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "StreamChatCompletion");

            // Get all chunks outside of try/catch to avoid the "yield in try" issue
            var chunks = await FetchStreamChunksAsync(request, apiKey, cancellationToken);

            // Now yield the chunks outside of any try blocks
            foreach (var chunk in chunks)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                yield return chunk;
            }
        }

        /// <summary>
        /// Helper method to fetch all stream chunks without yielding in a try block
        /// </summary>
        private async Task<List<CoreModels.ChatCompletionChunk>> FetchStreamChunksAsync(
            CoreModels.ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            var chunks = new List<CoreModels.ChatCompletionChunk>();

            try
            {
                using var client = CreateHttpClient(apiKey);
                var openAiRequest = PrepareStreamingRequest(request);
                var endpoint = GetChatCompletionEndpoint();

                Logger.LogDebug("Sending streaming chat completion request to {Provider} at {Endpoint}", ProviderName, endpoint);

                var response = await SendStreamingRequestAsync(client, endpoint, openAiRequest, apiKey, cancellationToken);
                chunks = await ProcessStreamingResponseAsync(response, request.Model, cancellationToken);

                return chunks;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Process the error with enhanced error extraction
                var enhancedErrorMessage = ExtractEnhancedErrorMessage(ex);
                Logger.LogError(ex, "Error in streaming chat completion from {Provider}: {Message}", ProviderName, enhancedErrorMessage);

                var error = CoreUtils.ExceptionHandler.HandleLlmException(ex, Logger, ProviderName, request.Model ?? ProviderModelId);
                throw error;
            }
        }

        /// <summary>
        /// Prepares a request for streaming by ensuring the stream parameter is set to true
        /// </summary>
        /// <param name="request">The original chat completion request</param>
        /// <returns>A request object with stream=true set</returns>
        private object PrepareStreamingRequest(CoreModels.ChatCompletionRequest request)
        {
            var openAiRequest = MapToOpenAIRequest(request);

            // Force stream parameter to true based on the request's type
            if (openAiRequest is JsonElement jsonElement)
            {
                return ForceStreamParameterInJsonElement(jsonElement);
            }
            else if (openAiRequest is Dictionary<string, object> dictObj)
            {
                dictObj["stream"] = true;
                return dictObj;
            }
            else if (openAiRequest is OpenAIModels.OpenAIChatCompletionRequest reqObj)
            {
                reqObj = reqObj with { Stream = true };
                return reqObj;
            }

            // If we can't determine the type, return the original request
            return openAiRequest;
        }

        /// <summary>
        /// Forces the stream parameter to true in a JsonElement
        /// </summary>
        /// <param name="jsonElement">The JsonElement to modify</param>
        /// <returns>An object with stream=true set</returns>
        private object ForceStreamParameterInJsonElement(JsonElement jsonElement)
        {
            var jsonObject = jsonElement.GetRawText();
            var tempObj = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonObject, DefaultJsonOptions);
            if (tempObj != null)
            {
                tempObj["stream"] = true;
                return tempObj;
            }

            // If deserialization fails, return the original element
            return jsonElement;
        }

        /// <summary>
        /// Sends a streaming request to the specified endpoint
        /// </summary>
        /// <param name="client">The HTTP client to use</param>
        /// <param name="endpoint">The endpoint to send the request to</param>
        /// <param name="request">The request object</param>
        /// <param name="apiKey">Optional API key to override the one in credentials</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>The HTTP response message</returns>
        private async Task<HttpResponseMessage> SendStreamingRequestAsync(
            HttpClient client,
            string endpoint,
            object request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return await CoreUtils.HttpClientHelper.SendStreamingRequestAsync(
                client,
                HttpMethod.Post,
                endpoint,
                request,
                CreateStandardHeaders(apiKey),
                DefaultJsonOptions,
                Logger,
                cancellationToken);
        }

        /// <summary>
        /// Processes a streaming response and returns a list of chat completion chunks
        /// </summary>
        /// <param name="response">The HTTP response message</param>
        /// <param name="originalModelAlias">The original model alias from the request</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>A list of chat completion chunks</returns>
        private async Task<List<CoreModels.ChatCompletionChunk>> ProcessStreamingResponseAsync(
            HttpResponseMessage response,
            string? originalModelAlias,
            CancellationToken cancellationToken)
        {
            var chunks = new List<CoreModels.ChatCompletionChunk>();

            // Use StreamHelper to process the SSE stream
            await foreach (var chunk in CoreUtils.StreamHelper.ProcessSseStreamAsync<OpenAIModels.OpenAIChatCompletionChunk>(
                response, Logger, DefaultJsonOptions, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                chunks.Add(MapFromOpenAIChunk(chunk, originalModelAlias));
            }

            return chunks;
        }

        /// <summary>
        /// Gets available models from the OpenAI-compatible API.
        /// </summary>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A list of available models.</returns>
        /// <remarks>
        /// This implementation attempts to retrieve models from the provider's API,
        /// but falls back to a predefined list if the request fails or if the provider
        /// doesn't support the standard /models endpoint.
        /// </remarks>
        public override async Task<List<InternalModels.ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteApiRequestAsync(async () =>
                {
                    using var client = CreateHttpClient(apiKey);

                    var endpoint = GetModelsEndpoint();

                    Logger.LogDebug("Getting available models from {Provider} at {Endpoint}", ProviderName, endpoint);

                    var response = await CoreUtils.HttpClientHelper.SendJsonRequestAsync<object, OpenAIModels.ListModelsResponse>(
                        client,
                        HttpMethod.Get,
                        endpoint,
                        new { }, // Use empty object instead of null
                        CreateStandardHeaders(apiKey),
                        DefaultJsonOptions,
                        Logger,
                        cancellationToken);

                    return response.Data
                        .Select(m => InternalModels.ExtendedModelInfo.Create(m.Id, ProviderName, m.Id))
                        .ToList();
                }, "GetModels", cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to retrieve models from {Provider} API. Returning known models.", ProviderName);
                return GetFallbackModels();
            }
        }

        /// <summary>
        /// Creates embeddings using the OpenAI-compatible API.
        /// </summary>
        /// <param name="request">The embedding request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An embedding response.</returns>
        /// <remarks>
        /// This implementation sends an embedding request to the provider's API and maps the
        /// response to the generic format. If a provider doesn't support embeddings, this method
        /// should be overridden to throw a <see cref="NotSupportedException"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        /// <exception cref="ValidationException">Thrown when the request fails validation.</exception>
        /// <exception cref="LLMCommunicationException">Thrown when there is a communication error with the provider.</exception>
        /// <exception cref="NotSupportedException">Thrown when the provider doesn't support embeddings.</exception>
        public override async Task<CoreModels.EmbeddingResponse> CreateEmbeddingAsync(
            CoreModels.EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateEmbedding");

            return await ExecuteApiRequestAsync(async () =>
            {
                using var client = CreateHttpClient(apiKey);

                var openAiRequest = new OpenAIModels.EmbeddingRequest
                {
                    Model = request.Model ?? ProviderModelId,
                    Input = request.Input,
                    EncodingFormat = request.EncodingFormat,
                    User = request.User ?? string.Empty,
                    Dimensions = request.Dimensions
                };

                var endpoint = GetEmbeddingEndpoint();

                Logger.LogDebug("Creating embeddings using {Provider} at {Endpoint}", ProviderName, endpoint);

                var response = await CoreUtils.HttpClientHelper.SendJsonRequestAsync<OpenAIModels.EmbeddingRequest, OpenAIModels.EmbeddingResponse>(
                    client,
                    HttpMethod.Post,
                    endpoint,
                    openAiRequest,
                    CreateStandardHeaders(apiKey),
                    DefaultJsonOptions,
                    Logger,
                    cancellationToken);

                return new CoreModels.EmbeddingResponse
                {
                    Data = response.Data.Select(d => new CoreModels.EmbeddingData
                    {
                        Index = d.Index,
                        Object = d.Object,
                        Embedding = d.Embedding.ToList()
                    }).ToList(),
                    Model = response.Model ?? ProviderModelId,
                    Object = response.Object ?? "embedding",
                    Usage = new CoreModels.Usage
                    {
                        PromptTokens = response.Usage?.PromptTokens ?? 0,
                        CompletionTokens = 0, // Embeddings don't have completion tokens
                        TotalTokens = response.Usage?.TotalTokens ?? 0
                    }
                };
            }, "CreateEmbedding", cancellationToken);
        }

        /// <summary>
        /// Creates images using the OpenAI-compatible API.
        /// </summary>
        /// <param name="request">The image generation request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An image generation response.</returns>
        /// <remarks>
        /// This implementation sends an image generation request to the provider's API and maps the
        /// response to the generic format. If a provider doesn't support image generation, this method
        /// should be overridden to throw a <see cref="NotSupportedException"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        /// <exception cref="ValidationException">Thrown when the request fails validation.</exception>
        /// <exception cref="LLMCommunicationException">Thrown when there is a communication error with the provider.</exception>
        /// <exception cref="NotSupportedException">Thrown when the provider doesn't support image generation.</exception>
        public override async Task<CoreModels.ImageGenerationResponse> CreateImageAsync(
            CoreModels.ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateImage");

            return await ExecuteApiRequestAsync(async () =>
            {
                using var client = CreateHttpClient(apiKey);

                // Only include quality and style for DALL-E 3
                var modelName = request.Model ?? ProviderModelId;
                var openAiRequest = modelName?.Contains("dall-e-3", StringComparison.OrdinalIgnoreCase) == true
                    ? new OpenAIModels.ImageGenerationRequest
                    {
                        Prompt = request.Prompt,
                        Model = request.Model ?? ProviderModelId,
                        N = request.N,
                        Size = request.Size ?? "1024x1024",
                        Quality = request.Quality,
                        Style = request.Style,
                        ResponseFormat = request.ResponseFormat ?? "url",
                        User = request.User
                    }
                    : new OpenAIModels.ImageGenerationRequest
                    {
                        Prompt = request.Prompt,
                        Model = request.Model ?? ProviderModelId,
                        N = request.N,
                        Size = request.Size ?? "1024x1024",
                        ResponseFormat = request.ResponseFormat ?? "url",
                        User = request.User
                    };

                var endpoint = GetImageGenerationEndpoint();

                Logger.LogInformation("Creating images using {Provider} at {Endpoint} with model {Model}, prompt: {Prompt}, size: {Size}, format: {Format}", 
                    ProviderName, endpoint, openAiRequest.Model, 
                    openAiRequest.Prompt?.Substring(0, Math.Min(50, openAiRequest.Prompt?.Length ?? 0)), 
                    openAiRequest.Size, openAiRequest.ResponseFormat);
                    
                // Log a warning about potential quota issues if using OpenAI
                if (ProviderName.Equals("openai", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogWarning("Note: OpenAI image generation errors with null messages often indicate quota/billing issues");
                }

                var response = await CoreUtils.HttpClientHelper.SendJsonRequestAsync<OpenAIModels.ImageGenerationRequest, OpenAIModels.ImageGenerationResponse>(
                    client,
                    HttpMethod.Post,
                    endpoint,
                    openAiRequest,
                    CreateStandardHeaders(apiKey),
                    DefaultJsonOptions,
                    Logger,
                    cancellationToken);

                return new CoreModels.ImageGenerationResponse
                {
                    Created = response.Created,
                    Data = response.Data.Select(d => new CoreModels.ImageData
                    {
                        Url = d.Url,
                        B64Json = d.B64Json
                        // Note: Core.Models.ImageData doesn't have RevisedPrompt property
                    }).ToList()
                };
            }, "CreateImage", cancellationToken);
        }

        /// <summary>
        /// Gets the chat completion endpoint for the provider.
        /// </summary>
        /// <returns>The full URL for the chat completions endpoint.</returns>
        /// <remarks>
        /// Derived classes can override this method to provide custom endpoints.
        /// </remarks>
        protected virtual string GetChatCompletionEndpoint()
        {
            return $"{BaseUrl}/chat/completions";
        }

        /// <summary>
        /// Gets the models endpoint for the provider.
        /// </summary>
        /// <returns>The full URL for the models endpoint.</returns>
        /// <remarks>
        /// Derived classes can override this method to provide custom endpoints.
        /// </remarks>
        protected virtual string GetModelsEndpoint()
        {
            return $"{BaseUrl}/models";
        }

        /// <summary>
        /// Gets the embedding endpoint for the provider.
        /// </summary>
        /// <returns>The full URL for the embeddings endpoint.</returns>
        /// <remarks>
        /// Derived classes can override this method to provide custom endpoints.
        /// </remarks>
        protected virtual string GetEmbeddingEndpoint()
        {
            return $"{BaseUrl}/embeddings";
        }

        /// <summary>
        /// Gets the image generation endpoint for the provider.
        /// </summary>
        /// <returns>The full URL for the image generations endpoint.</returns>
        /// <remarks>
        /// Derived classes can override this method to provide custom endpoints.
        /// </remarks>
        protected virtual string GetImageGenerationEndpoint()
        {
            return $"{BaseUrl}/images/generations";
        }

        /// <summary>
        /// Gets a fallback list of models for the provider.
        /// </summary>
        /// <returns>A list of known models for the provider.</returns>
        /// <remarks>
        /// This method is called when the models endpoint fails or is not available.
        /// Derived classes can override this method to provide a more specific list.
        /// </remarks>
        protected virtual List<InternalModels.ExtendedModelInfo> GetFallbackModels()
        {
            if (ProviderFallbackModels.TryGetValue(ProviderName.ToLowerInvariant(), out var models))
            {
                return models;
            }

            // Generic fallback for unknown providers
            return new List<InternalModels.ExtendedModelInfo>
            {
                InternalModels.ExtendedModelInfo.Create(ProviderModelId, ProviderName, ProviderModelId)
            };
        }

        /// <summary>
        /// Maps the provider-agnostic request to OpenAI format.
        /// </summary>
        /// <param name="request">The provider-agnostic request.</param>
        /// <returns>An object representing the OpenAI-formatted request.</returns>
        /// <remarks>
        /// This method maps the generic request to the format expected by OpenAI-compatible APIs.
        /// Derived classes can override this method to provide custom mapping.
        /// </remarks>
        protected virtual object MapToOpenAIRequest(CoreModels.ChatCompletionRequest request)
        {
            // Map tools if present
            List<object>? openAiTools = null;
            if (request.Tools != null && request.Tools.Count > 0)
            {
                openAiTools = request.Tools.Select(t => new
                {
                    type = t.Type ?? "function",
                    function = new
                    {
                        name = t.Function?.Name ?? "unknown",
                        description = t.Function?.Description,
                        parameters = t.Function?.Parameters
                    }
                }).Cast<object>().ToList();
            }

            // Map tool choice if present
            object? openAiToolChoice = null;
            if (request.ToolChoice != null)
            {
                // Use the GetSerializedValue method to get the properly formatted object
                openAiToolChoice = request.ToolChoice;
            }

            // Map messages with their content - handle multimodal content for vision models
            var messages = request.Messages.Select(m =>
            {
                // Check if this is a multimodal message
                if (ProviderHelpers.ContentHelper.IsTextOnly(m.Content))
                {
                    // Simple text-only message
                    return new OpenAIModels.OpenAIMessage
                    {
                        Role = m.Role,
                        Content = ProviderHelpers.ContentHelper.GetContentAsString(m.Content),
                        Name = m.Name,
                        ToolCalls = m.ToolCalls?.Select(tc => new
                        {
                            id = tc.Id,
                            type = tc.Type ?? "function",
                            function = new
                            {
                                name = tc.Function?.Name,
                                arguments = tc.Function?.Arguments
                            }
                        }).Cast<object>().ToList(),
                        ToolCallId = m.ToolCallId
                    };
                }
                else
                {
                    // Multimodal message with potential images
                    return new OpenAIModels.OpenAIMessage
                    {
                        Role = m.Role,
                        Content = MapMultimodalContent(m.Content),
                        Name = m.Name,
                        ToolCalls = m.ToolCalls?.Select(tc => new
                        {
                            id = tc.Id,
                            type = tc.Type ?? "function",
                            function = new
                            {
                                name = tc.Function?.Name,
                                arguments = tc.Function?.Arguments
                            }
                        }).Cast<object>().ToList(),
                        ToolCallId = m.ToolCallId
                    };
                }
            }).ToList();

            // Create the OpenAI request
            return new OpenAIModels.OpenAIChatCompletionRequest
            {
                Model = ProviderModelId,  // Always use the provider's model ID, not the alias
                Messages = messages,
                MaxTokens = request.MaxTokens,
                Temperature = ParameterConverter.ToTemperature(request.Temperature),
                TopP = ParameterConverter.ToProbability(request.TopP, 0.0, 1.0),
                N = request.N,
                Stop = ParameterConverter.ConvertStopSequences(request.Stop),
                PresencePenalty = ParameterConverter.ToProbability(request.PresencePenalty),
                FrequencyPenalty = ParameterConverter.ToProbability(request.FrequencyPenalty),
                LogitBias = ParameterConverter.ConvertLogitBias(request.LogitBias),
                User = request.User,
                Seed = request.Seed,
                Tools = openAiTools,
                ToolChoice = openAiToolChoice,
                ResponseFormat = request.ResponseFormat != null ? new OpenAIModels.ResponseFormat { Type = request.ResponseFormat.Type ?? "text" } : new OpenAIModels.ResponseFormat { Type = "text" },
                Stream = request.Stream ?? false
            };
        }

        /// <summary>
        /// Maps multimodal content to the format expected by OpenAI's API.
        /// </summary>
        /// <param name="content">The content object which may contain text and images</param>
        /// <returns>A properly formatted list of content parts for OpenAI</returns>
        protected virtual object MapMultimodalContent(object? content)
        {
            if (content == null)
                return "";

            if (content is string textContent)
                return textContent;

            // Create a list to hold the formatted content parts
            var contentParts = new List<object>();

            // Extract text parts
            var textParts = ProviderHelpers.ContentHelper.ExtractMultimodalContent(content);
            foreach (var text in textParts)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    contentParts.Add(new
                    {
                        type = "text",
                        text = text
                    });
                }
            }

            // Extract image URLs
            var imageUrls = ProviderHelpers.ContentHelper.ExtractImageUrls(content);
            foreach (var imageUrl in imageUrls)
            {
                contentParts.Add(new
                {
                    type = "image_url",
                    image_url = new
                    {
                        url = imageUrl.Url,
                        detail = string.IsNullOrEmpty(imageUrl.Detail) ? "auto" : imageUrl.Detail
                    }
                });
            }

            // If no parts were added, return an empty string
            if (contentParts.Count == 0)
                return "";

            return contentParts;
        }

        /// <summary>
        /// Maps the OpenAI response to provider-agnostic format.
        /// </summary>
        /// <param name="response">The response from the OpenAI API.</param>
        /// <param name="originalModelAlias">The original model alias from the request.</param>
        /// <returns>A provider-agnostic chat completion response.</returns>
        /// <remarks>
        /// This method maps the OpenAI-formatted response to the generic format used by the application.
        /// Derived classes can override this method to provide custom mapping.
        /// </remarks>
        protected virtual CoreModels.ChatCompletionResponse MapFromOpenAIResponse(
            object responseObj,
            string? originalModelAlias)
        {
            // Cast using dynamic to avoid multiple type-specific methods
            dynamic response = responseObj;

            try
            {
                // Create the basic response with required fields
                var result = CreateBasicChatCompletionResponse(response, originalModelAlias);

                // Add optional properties if they exist
                result = AddOptionalResponseProperties(result, response);

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error mapping OpenAI response: {Message}", ex.Message);

                // Create a minimal response with as much data as we can salvage
                return CreateFallbackChatCompletionResponse(response, originalModelAlias);
            }
        }

        /// <summary>
        /// Creates a basic chat completion response with required fields.
        /// </summary>
        /// <param name="response">The dynamic response from the provider.</param>
        /// <param name="originalModelAlias">The original model alias from the request.</param>
        /// <returns>A basic chat completion response.</returns>
        private CoreModels.ChatCompletionResponse CreateBasicChatCompletionResponse(
            dynamic response,
            string? originalModelAlias)
        {
            return new CoreModels.ChatCompletionResponse
            {
                Id = response.Id,
                Object = response.Object,
                Created = response.Created,
                Model = originalModelAlias ?? response.Model,
                Choices = MapDynamicChoices(response.Choices),
                Usage = MapUsage(response.Usage),
                OriginalModelAlias = originalModelAlias
            };
        }

        /// <summary>
        /// Creates a fallback chat completion response when the normal mapping fails.
        /// </summary>
        /// <param name="response">The dynamic response from the provider.</param>
        /// <param name="originalModelAlias">The original model alias from the request.</param>
        /// <returns>A minimal chat completion response.</returns>
        private CoreModels.ChatCompletionResponse CreateFallbackChatCompletionResponse(
            dynamic response,
            string? originalModelAlias)
        {
            try
            {
                // Attempt to create a basic response with as much as we can extract
                return new CoreModels.ChatCompletionResponse
                {
                    Id = TryGetProperty(response, "Id", Guid.NewGuid().ToString()),
                    Object = TryGetProperty(response, "Object", "chat.completion"),
                    Created = TryGetProperty(response, "Created", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    Model = originalModelAlias ?? TryGetProperty(response, "Model", ProviderModelId),
                    Choices = new List<CoreModels.Choice>(),
                    OriginalModelAlias = originalModelAlias
                };
            }
            catch
            {
                // Absolute fallback if everything fails
                return new CoreModels.ChatCompletionResponse
                {
                    Id = Guid.NewGuid().ToString(),
                    Object = "chat.completion",
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Model = originalModelAlias ?? ProviderModelId,
                    Choices = new List<CoreModels.Choice>(),
                    OriginalModelAlias = originalModelAlias
                };
            }
        }

        /// <summary>
        /// Maps the usage information from a dynamic response.
        /// </summary>
        /// <param name="usageInfo">The dynamic usage information.</param>
        /// <returns>A strongly-typed Usage object, or null if the input is null.</returns>
        private CoreModels.Usage? MapUsage(dynamic usageInfo)
        {
            if (usageInfo == null)
            {
                return null;
            }

            try
            {
                return new CoreModels.Usage
                {
                    PromptTokens = usageInfo.PromptTokens,
                    CompletionTokens = usageInfo.CompletionTokens,
                    TotalTokens = usageInfo.TotalTokens
                };
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error mapping usage information: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Adds optional properties to a chat completion response if they exist in the provider response.
        /// </summary>
        /// <param name="response">The chat completion response to enhance.</param>
        /// <param name="providerResponse">The dynamic provider response.</param>
        /// <returns>The enhanced chat completion response.</returns>
        private CoreModels.ChatCompletionResponse AddOptionalResponseProperties(
            CoreModels.ChatCompletionResponse response,
            dynamic providerResponse)
        {
            // Try to add SystemFingerprint
            try
            {
                var hasSysFp = HasProperty(providerResponse, "SystemFingerprint");
                if (hasSysFp)
                {
                    response.SystemFingerprint = providerResponse.SystemFingerprint;
                }
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                // Property doesn't exist, which is OK for most providers
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error adding SystemFingerprint property: {Message}", ex.Message);
            }

            // Try to add Seed
            try
            {
                var hasSeed = HasProperty(providerResponse, "Seed");
                if (hasSeed)
                {
                    response.Seed = providerResponse.Seed;
                }
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                // Property doesn't exist, which is OK for most providers
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error adding Seed property: {Message}", ex.Message);
            }

            return response;
        }

        /// <summary>
        /// Tries to get a property value from a dynamic object, returning a default value if not found.
        /// </summary>
        /// <typeparam name="T">The type of value to return.</typeparam>
        /// <param name="obj">The dynamic object to get the property from.</param>
        /// <param name="propertyName">The name of the property to get.</param>
        /// <param name="defaultValue">The default value to return if the property is not found.</param>
        /// <returns>The property value if found, or the default value if not.</returns>
        private T TryGetProperty<T>(dynamic obj, string propertyName, T defaultValue)
        {
            try
            {
                var hasProperty = HasProperty(obj, propertyName);
                if (hasProperty)
                {
                    // Try to get the property using reflection
                    var property = obj.GetType().GetProperty(propertyName);
                    if (property != null)
                    {
                        return (T)property.GetValue(obj, null);
                    }

                    // If reflection fails, try dynamic access
                    if (obj is IDictionary<string, object> dictObj && dictObj.TryGetValue(propertyName, out var dictValue))
                    {
                        return (T)dictValue;
                    }
                }
            }
            catch
            {
                // Property doesn't exist or couldn't be accessed
                // This is expected behavior for optional properties, so we use Debug level
                // Suppress logging for now since we're in a dynamic context
                // This is expected behavior when optional properties don't exist
            }

            return defaultValue;
        }

        /// <summary>
        /// Checks if a dynamic object has a specific property.
        /// </summary>
        /// <param name="obj">The dynamic object to check.</param>
        /// <param name="propertyName">The name of the property to check for.</param>
        /// <returns>True if the property exists, false otherwise.</returns>
        private bool HasProperty(dynamic obj, string propertyName)
        {
            try
            {
                // Try to access the property using reflection
                var result = obj.GetType().GetProperty(propertyName) != null;
                return result;
            }
            catch
            {
                try
                {
                    // Alternatively, try to convert to JSON and check if the property exists
                    var jsonString = System.Text.Json.JsonSerializer.Serialize(obj);
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonString);
                    System.Text.Json.JsonElement outValue;
                    return jsonDoc.RootElement.TryGetProperty(propertyName, out outValue);
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Maps the OpenAI streaming chunk to provider-agnostic format.
        /// </summary>
        /// <param name="chunk">The chunk from the OpenAI streaming API.</param>
        /// <param name="originalModelAlias">The original model alias from the request.</param>
        /// <returns>A provider-agnostic chat completion chunk.</returns>
        /// <remarks>
        /// This method maps the OpenAI-formatted streaming chunk to the generic format used by the application.
        /// Derived classes can override this method to provide custom mapping.
        /// </remarks>
        protected virtual CoreModels.ChatCompletionChunk MapFromOpenAIChunk(
            object chunkObj,
            string? originalModelAlias)
        {
            // Cast using dynamic to avoid multiple type-specific methods
            dynamic chunk = chunkObj;

            return new CoreModels.ChatCompletionChunk
            {
                Id = chunk.Id,
                Object = chunk.Object,
                Created = chunk.Created,
                Model = originalModelAlias ?? chunk.Model, // Use original alias if provided
                SystemFingerprint = chunk.SystemFingerprint,
                Choices = MapDynamicStreamingChoices(chunk.Choices),
                OriginalModelAlias = originalModelAlias
            };
        }

        /// <summary>
        /// Maps dynamic choices from a response to strongly-typed Choice objects.
        /// </summary>
        /// <param name="dynamicChoices">The dynamic choices collection from response.</param>
        /// <returns>A list of strongly-typed Choice objects.</returns>
        private List<CoreModels.Choice> MapDynamicChoices(dynamic dynamicChoices)
        {
            var choices = new List<CoreModels.Choice>();

            // Handle null choices
            if (dynamicChoices == null)
            {
                return choices;
            }

            try
            {
                foreach (var choice in dynamicChoices)
                {
                    try
                    {
                        var mappedChoice = MapSingleChoice(choice);
                        choices.Add(mappedChoice);
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail on individual choice processing
                        Logger.LogWarning("Error processing choice: {Error}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log and return whatever choices we managed to process
                Logger.LogError(ex, "Error mapping choices");
            }

            return choices;
        }

        /// <summary>
        /// Maps a single dynamic choice to a strongly-typed Choice object.
        /// </summary>
        /// <param name="choice">The dynamic choice to map.</param>
        /// <returns>A strongly-typed Choice object.</returns>
        private CoreModels.Choice MapSingleChoice(dynamic choice)
        {
            var mappedChoice = new CoreModels.Choice
            {
                Index = choice.Index,
                FinishReason = choice.FinishReason,
                Message = new CoreModels.Message
                {
                    Role = choice.Message.Role,
                    Content = choice.Message.Content
                }
            };

            // Handle tool calls if present
            if (choice.Message.ToolCalls != null)
            {
                mappedChoice.Message.ToolCalls = MapResponseToolCalls(choice.Message.ToolCalls);
            }

            // Handle tool_call_id if present (for tool response messages)
            if (choice.Message.ToolCallId != null)
            {
                mappedChoice.Message.ToolCallId = choice.Message.ToolCallId?.ToString();
            }

            return mappedChoice;
        }

        /// <summary>
        /// Maps dynamic tool calls from a response to strongly-typed ToolCall objects.
        /// </summary>
        /// <param name="toolCalls">The dynamic tool calls to map.</param>
        /// <returns>A list of strongly-typed ToolCall objects.</returns>
        private List<CoreModels.ToolCall> MapResponseToolCalls(dynamic toolCalls)
        {
            var mappedToolCalls = new List<CoreModels.ToolCall>();

            foreach (var toolCall in toolCalls)
            {
                try
                {
                    var mappedToolCall = MapSingleResponseToolCall(toolCall);
                    mappedToolCalls.Add(mappedToolCall);
                }
                catch (Exception ex)
                {
                    // Log but continue with other tool calls
                    Logger.LogWarning(ex, "Error mapping tool call");
                }
            }

            return mappedToolCalls;
        }

        /// <summary>
        /// Maps a single dynamic tool call from a response to a strongly-typed ToolCall object.
        /// </summary>
        /// <param name="toolCall">The dynamic tool call to map.</param>
        /// <returns>A strongly-typed ToolCall object.</returns>
        private CoreModels.ToolCall MapSingleResponseToolCall(dynamic toolCall)
        {
            return new CoreModels.ToolCall
            {
                Id = toolCall.id?.ToString() ?? Guid.NewGuid().ToString(),
                Type = toolCall.type?.ToString() ?? "function",
                Function = new CoreModels.FunctionCall
                {
                    Name = toolCall.function?.name?.ToString() ?? "unknown",
                    Arguments = toolCall.function?.arguments?.ToString() ?? "{}"
                }
            };
        }

        /// <summary>
        /// Maps dynamic streaming choices to strongly-typed StreamingChoice objects.
        /// </summary>
        /// <param name="dynamicChoices">The dynamic streaming choices collection.</param>
        /// <returns>A list of strongly-typed StreamingChoice objects.</returns>
        private List<CoreModels.StreamingChoice> MapDynamicStreamingChoices(dynamic dynamicChoices)
        {
            try
            {
                var choices = new List<CoreModels.StreamingChoice>();

                // Handle null choices
                if (dynamicChoices == null)
                {
                    return choices;
                }

                foreach (var choice in dynamicChoices)
                {
                    try
                    {
                        var mappedChoice = MapSingleStreamingChoice(choice);
                        choices.Add(mappedChoice);
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail on individual choice processing
                        Logger.LogWarning(ex, "Error processing streaming choice");
                    }
                }

                return choices;
            }
            catch (Exception ex)
            {
                // Log and return empty choices rather than failing
                Logger.LogError(ex, "Error mapping streaming choices");
                return new List<CoreModels.StreamingChoice>();
            }
        }

        /// <summary>
        /// Maps a single dynamic streaming choice to a strongly-typed StreamingChoice object.
        /// </summary>
        /// <param name="choice">The dynamic choice to map.</param>
        /// <returns>A strongly-typed StreamingChoice object.</returns>
        private CoreModels.StreamingChoice MapSingleStreamingChoice(dynamic choice)
        {
            var streamingChoice = new CoreModels.StreamingChoice
            {
                Index = choice.Index,
                FinishReason = choice.FinishReason,
                Delta = new CoreModels.DeltaContent
                {
                    Role = choice.Delta?.Role,
                    Content = choice.Delta?.Content
                }
            };

            // Handle tool calls if present
            if (choice.Delta != null && choice.Delta.ToolCalls != null)
            {
                streamingChoice.Delta.ToolCalls = MapToolCalls(choice.Delta.ToolCalls);
            }

            return streamingChoice;
        }

        /// <summary>
        /// Maps dynamic tool calls to strongly-typed ToolCallChunk objects.
        /// </summary>
        /// <param name="toolCalls">The dynamic tool calls to map.</param>
        /// <returns>A list of strongly-typed ToolCallChunk objects.</returns>
        private List<CoreModels.ToolCallChunk> MapToolCalls(dynamic toolCalls)
        {
            var mappedToolCalls = new List<CoreModels.ToolCallChunk>();

            foreach (var toolCall in toolCalls)
            {
                try
                {
                    var mappedToolCall = MapSingleToolCall(toolCall);
                    mappedToolCalls.Add(mappedToolCall);
                }
                catch (Exception ex)
                {
                    // Log but don't fail
                    Logger.LogWarning(ex, "Error processing tool call in stream");
                }
            }

            return mappedToolCalls;
        }

        /// <summary>
        /// Maps a single dynamic tool call to a strongly-typed ToolCallChunk object.
        /// </summary>
        /// <param name="toolCall">The dynamic tool call to map.</param>
        /// <returns>A strongly-typed ToolCallChunk object.</returns>
        private CoreModels.ToolCallChunk MapSingleToolCall(dynamic toolCall)
        {
            var mappedToolCall = new CoreModels.ToolCallChunk
            {
                Index = toolCall.Index,
                Id = toolCall.Id,
                Type = toolCall.Type
            };

            if (toolCall.Function != null)
            {
                mappedToolCall.Function = new CoreModels.FunctionCallChunk
                {
                    Name = toolCall.Function.Name,
                    Arguments = toolCall.Function.Arguments
                };
            }

            return mappedToolCall;
        }

        /// <summary>
        /// Configure the HTTP client with provider-specific settings.
        /// </summary>
        /// <param name="client">The HTTP client to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        /// <remarks>
        /// This method adds standard headers and authentication to the HTTP client.
        /// Derived classes can override this method to provide provider-specific configuration.
        /// </remarks>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            base.ConfigureHttpClient(client, apiKey);

            // Set the base address if not already set
            if (client.BaseAddress == null && !string.IsNullOrEmpty(BaseUrl))
            {
                client.BaseAddress = new Uri(BaseUrl);
            }

            // Add OpenAI API version header if needed
            // client.DefaultRequestHeaders.Add("OpenAI-Version", "2023-05-15");
        }

        /// <inheritdoc />
        public override Task<Core.Models.ProviderCapabilities> GetCapabilitiesAsync(string? modelId = null)
        {
            var model = modelId ?? ProviderModelId;
            
            // For OpenAI-compatible providers, we provide sensible defaults
            // Individual providers can override this with more specific capabilities
            return Task.FromResult(new Core.Models.ProviderCapabilities
            {
                Provider = ProviderName,
                ModelId = model,
                ChatParameters = new Core.Models.ChatParameterSupport
                {
                    Temperature = true,
                    MaxTokens = true,
                    TopP = true,
                    TopK = false, // Most OpenAI-compatible APIs don't support top-k
                    Stop = true,
                    PresencePenalty = true,
                    FrequencyPenalty = true,
                    LogitBias = true,
                    N = true,
                    User = true,
                    Seed = true,
                    ResponseFormat = true,
                    Tools = true,
                    Constraints = new Core.Models.ParameterConstraints
                    {
                        TemperatureRange = new Core.Models.Range<double>(0.0, 2.0),
                        TopPRange = new Core.Models.Range<double>(0.0, 1.0),
                        MaxStopSequences = 4,
                        MaxTokenLimit = 4096 // Conservative default
                    }
                },
                Features = new Core.Models.FeatureSupport
                {
                    Streaming = true,
                    Embeddings = false, // Usually separate models
                    ImageGeneration = false, // Usually separate models
                    VisionInput = false, // Provider-specific
                    FunctionCalling = true,
                    AudioTranscription = false, // Provider-specific
                    TextToSpeech = false // Provider-specific
                }
            });
        }

        /// <summary>
        /// Extracts a more helpful error message from exception details.
        /// </summary>
        /// <param name="ex">The exception to extract information from.</param>
        /// <returns>An enhanced error message.</returns>
        /// <remarks>
        /// This method attempts to extract more helpful error information from exceptions.
        /// It looks for patterns in error messages and extracts the most relevant information.
        /// </remarks>
        protected virtual string ExtractEnhancedErrorMessage(Exception ex)
        {
            // Try to extract error details in order of preference:

            // 1. Look for "Response:" pattern in the message
            var msg = ex.Message;
            var responseIdx = msg.IndexOf("Response:");
            if (responseIdx >= 0)
            {
                var extracted = msg.Substring(responseIdx + "Response:".Length).Trim();
                if (!string.IsNullOrEmpty(extracted))
                {
                    return extracted;
                }
            }

            // 2. Look for JSON content in the message
            var jsonStart = msg.IndexOf("{");
            var jsonEnd = msg.LastIndexOf("}");
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonPart = msg.Substring(jsonStart, jsonEnd - jsonStart + 1);
                try
                {
                    var json = JsonDocument.Parse(jsonPart);
                    if (json.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        if (errorElement.TryGetProperty("message", out var messageElement))
                        {
                            return messageElement.GetString() ?? msg;
                        }
                    }
                }
                catch
                {
                    // If parsing fails, continue to the next method
                }
            }

            // 3. Look for Body data in the exception's Data dictionary
            if (ex.Data.Contains("Body") && ex.Data["Body"] is string body && !string.IsNullOrEmpty(body))
            {
                return body;
            }

            // 4. Try inner exception
            if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
            {
                return ex.InnerException.Message;
            }

            // 5. Fallback to original message
            return msg;
        }
    }
}
