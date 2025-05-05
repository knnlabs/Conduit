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
using Microsoft.Extensions.Logging;
using ConduitLLM.Providers.InternalModels;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Core.Models;

// Use explicit namespaces to avoid ambiguity
using CoreModels = ConduitLLM.Core.Models;
using InternalModels = ConduitLLM.Providers.InternalModels;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with the Anthropic API to access Claude models for text generation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This client implements the ILLMClient interface for interacting with Anthropic's Claude models.
    /// It supports chat completions with both synchronous and streaming responses.
    /// </para>
    /// <para>
    /// Key features:
    /// - Supports Claude 3 models (Opus, Sonnet, Haiku) and older Claude models
    /// - Handles proper mapping between Conduit's message format and Anthropic's API format
    /// - Supports streaming responses with SSE (Server-Sent Events) parsing
    /// - Includes comprehensive error handling and logging
    /// </para>
    /// <para>
    /// The client automatically handles system messages by moving them to the
    /// dedicated systemPrompt parameter used by Anthropic's API.
    /// </para>
    /// </remarks>
    public class AnthropicClient : BaseLLMClient
    {
        /// <summary>
        /// Default base URL for the Anthropic API
        /// </summary>
        private const string DefaultApiBase = "https://api.anthropic.com/v1";
        
        /// <summary>
        /// Required Anthropic API version header value
        /// </summary>
        private const string AnthropicVersion = "2023-06-01";

        /// <summary>
        /// Initializes a new instance of the AnthropicClient class.
        /// </summary>
        /// <param name="credentials">Provider credentials containing API key and endpoint configuration.</param>
        /// <param name="providerModelId">The specific Anthropic model ID to use (e.g., claude-3-opus-20240229).</param>
        /// <param name="logger">Logger for recording diagnostic information.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances.</param>
        /// <exception cref="ArgumentNullException">Thrown when credentials, providerModelId, or logger is null.</exception>
        /// <exception cref="ConfigurationException">Thrown when API key is missing in the credentials.</exception>
        public AnthropicClient(
            ProviderCredentials credentials, 
            string providerModelId, 
            ILogger<AnthropicClient> logger,
            IHttpClientFactory? httpClientFactory = null)
            : base(
                  credentials, 
                  providerModelId, 
                  logger, 
                  httpClientFactory, 
                  "anthropic")
        {
        }

        /// <summary>
        /// Validates that the required credentials are present for Anthropic API access.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Anthropic requires an API key for authentication. This method validates that
        /// the API key is present in the credentials before allowing any API calls.
        /// </para>
        /// <para>
        /// The API key should be provided in the ApiKey property of the ProviderCredentials object.
        /// </para>
        /// </remarks>
        /// <exception cref="ConfigurationException">Thrown when the API key is missing or empty.</exception>
        protected override void ValidateCredentials()
        {
            if (string.IsNullOrWhiteSpace(Credentials.ApiKey))
            {
                throw new ConfigurationException($"API key (x-api-key) is missing for provider '{ProviderName}'");
            }
        }

        /// <summary>
        /// Configures the HttpClient with necessary headers and settings for Anthropic API requests.
        /// </summary>
        /// <param name="client">The HttpClient to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        /// <remarks>
        /// <para>
        /// Anthropic API requires specific headers for authentication and API versioning:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>anthropic-version: The Anthropic API version to use</description></item>
        ///   <item><description>x-api-key: The API key for authentication</description></item>
        ///   <item><description>Accept: application/json for response format</description></item>
        /// </list>
        /// <para>
        /// Unlike many other providers, Anthropic uses a custom x-api-key header instead of
        /// the standard Authorization header with Bearer token. This method removes the
        /// Authorization header set by the base class and adds the custom header.
        /// </para>
        /// <para>
        /// This method also sets the base URL for API requests based on the ApiBase property
        /// of the ProviderCredentials, or falls back to the default Anthropic API URL.
        /// </para>
        /// </remarks>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            base.ConfigureHttpClient(client, apiKey);
            
            // Set Anthropic-specific headers
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            
            // Remove the Authorization header set by the base class
            client.DefaultRequestHeaders.Authorization = null;
            
            // Set the base address
            string apiBase = string.IsNullOrWhiteSpace(Credentials.ApiBase) ? DefaultApiBase : Credentials.ApiBase;
            client.BaseAddress = new Uri(apiBase.TrimEnd('/'));
        }

        /// <summary>
        /// Creates a chat completion using the Anthropic API.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A chat completion response.</returns>
        /// <remarks>
        /// <para>
        /// This method sends a request to the Anthropic API to generate a completions for
        /// a conversation. It follows these steps:
        /// </para>
        /// <list type="number">
        ///   <item><description>Validates the request for required parameters</description></item>
        ///   <item><description>Maps the generic request to Anthropic's format</description></item>
        ///   <item><description>Sends the request to Anthropic's messages endpoint</description></item>
        ///   <item><description>Maps the response back to the generic format</description></item>
        /// </list>
        /// <para>
        /// The implementation uses the ExecuteApiRequestAsync helper method to provide
        /// standardized error handling and retry logic.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        /// <exception cref="ValidationException">Thrown when the request fails validation.</exception>
        /// <exception cref="LLMCommunicationException">Thrown when there is a communication error with Anthropic.</exception>
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
                var anthropicRequest = MapToAnthropicRequest(request);
                
                var response = await ConduitLLM.Core.Utilities.HttpClientHelper.SendJsonRequestAsync<AnthropicMessageRequest, AnthropicMessageResponse>(
                    client,
                    HttpMethod.Post,
                    "/v1/messages",
                    anthropicRequest,
                    null,
                    DefaultJsonOptions,
                    Logger,
                    cancellationToken);
                
                return MapFromAnthropicResponse(response, request.Model);
            }, "ChatCompletion", cancellationToken);
        }

        /// <summary>
        /// Streams a chat completion using the Anthropic API.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable of chat completion chunks.</returns>
        /// <remarks>
        /// <para>
        /// This method sends a streaming request to the Anthropic API to generate a completion for
        /// a conversation, returning results as they become available. It follows these steps:
        /// </para>
        /// <list type="number">
        ///   <item><description>Validates the request for required parameters</description></item>
        ///   <item><description>Maps the generic request to Anthropic's format, enforcing stream=true</description></item>
        ///   <item><description>Establishes a streaming connection to Anthropic's messages endpoint</description></item>
        ///   <item><description>Processes the SSE (Server-Sent Events) stream using StreamHelper</description></item>
        ///   <item><description>Maps each streaming event to a chat completion chunk</description></item>
        ///   <item><description>Yields each chunk as it becomes available</description></item>
        ///   <item><description>Creates a final chunk with finish_reason when the message_stop event is received</description></item>
        /// </list>
        /// <para>
        /// Anthropic's streaming format differs from the OpenAI standard. This method handles
        /// the conversion between formats, focusing on content_block_delta events for content
        /// and message_stop events for completion status.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        /// <exception cref="ValidationException">Thrown when the request fails validation.</exception>
        /// <exception cref="LLMCommunicationException">Thrown when there is a communication error with Anthropic.</exception>
        /// <exception cref="ConfigurationException">Thrown when there is a configuration error.</exception>
        public override async IAsyncEnumerable<CoreModels.ChatCompletionChunk> StreamChatCompletionAsync(
            CoreModels.ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "StreamChatCompletion");
            
            HttpResponseMessage? response = null;
            
            try
            {
                using var client = CreateHttpClient(apiKey);
                // Create a new request with Stream explicitly set to true
                var baseRequest = MapToAnthropicRequest(request);
                var anthropicRequest = new AnthropicMessageRequest
                {
                    Model = baseRequest.Model,
                    Messages = baseRequest.Messages,
                    MaxTokens = baseRequest.MaxTokens,
                    SystemPrompt = baseRequest.SystemPrompt,
                    Temperature = baseRequest.Temperature,
                    TopP = baseRequest.TopP,
                    TopK = baseRequest.TopK,
                    StopSequences = baseRequest.StopSequences,
                    Stream = true
                };
                
                response = await ConduitLLM.Core.Utilities.HttpClientHelper.SendStreamingRequestAsync(
                    client,
                    HttpMethod.Post,
                    "/v1/messages",
                    anthropicRequest,
                    null,
                    DefaultJsonOptions,
                    Logger,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogError(ex, "Error streaming chat completion from Anthropic: {ErrorMessage}", ex.Message);
                throw new LLMCommunicationException($"Error streaming chat completion from Anthropic: {ex.Message}", ex);
            }
            
            // Process the stream outside of the try/catch block to avoid yielding in try
            if (response != null)
            {
                await foreach (var chunk in ProcessAnthropicStreamAsync(response, request.Model, cancellationToken))
                {
                    yield return chunk;
                }
            }
        }
        
        /// <summary>
        /// Processes the streaming response from Anthropic.
        /// </summary>
        /// <param name="response">The HTTP response containing the stream.</param>
        /// <param name="modelId">The model identifier.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable of chat completion chunks.</returns>
        private async IAsyncEnumerable<CoreModels.ChatCompletionChunk> ProcessAnthropicStreamAsync(
            HttpResponseMessage response, 
            string modelId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Create a wrapped enumerator to handle errors outside the yielding loop
            IAsyncEnumerable<AnthropicMessageStreamEvent> streamEvents;
            
            try
            {
                // Get the stream of events but don't start consuming it yet
                streamEvents = StreamHelper.ProcessSseStreamAsync<AnthropicMessageStreamEvent>(
                    response, Logger, DefaultJsonOptions, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogError(ex, "Error initializing Anthropic stream: {ErrorMessage}", ex.Message);
                throw new LLMCommunicationException($"Error initializing Anthropic stream: {ex.Message}", ex);
            }
            
            // Process the events outside the try/catch block
            await foreach (var chunk in streamEvents.WithCancellation(cancellationToken))
            {
                // Only process content blocks, ignore other event types
                if (chunk.Type == "content_block_delta")
                {
                    // Map Anthropic stream event to chat completion chunk
                    var deltaContent = chunk.Delta?.Text ?? "";
                    var index = chunk.Index;
                    
                    yield return new CoreModels.ChatCompletionChunk
                    {
                        // Generate a new ID since AnthropicMessageStreamEvent doesn't have a Message.Id property
                        Id = $"chatcmpl-{Guid.NewGuid():N}",
                        Object = "chat.completion.chunk",
                        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Model = modelId,
                        Choices = new List<CoreModels.StreamingChoice>
                        {
                            new CoreModels.StreamingChoice
                            {
                                Index = 0,
                                Delta = new CoreModels.DeltaContent
                                {
                                    Role = index == 0 ? "assistant" : null, // Only include role in first chunk
                                    Content = deltaContent
                                },
                                FinishReason = null // Will be set in the final chunk
                            }
                        },
                        OriginalModelAlias = modelId
                    };
                }
                else if (chunk.Type == "message_stop")
                {
                    // We've reached the end of the response
                    // Return a final chunk with finish_reason
                    yield return new CoreModels.ChatCompletionChunk
                    {
                        Id = $"chatcmpl-{Guid.NewGuid():N}",
                        Object = "chat.completion.chunk",
                        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Model = modelId,
                        Choices = new List<CoreModels.StreamingChoice>
                        {
                            new CoreModels.StreamingChoice
                            {
                                Index = 0,
                                Delta = new CoreModels.DeltaContent(),
                                FinishReason = "stop"
                            }
                        },
                        OriginalModelAlias = modelId
                    };
                }
            }
        }
        /// <summary>
        /// Helper method to create a final stream chunk with a finish reason.
        /// </summary>
        /// <param name="modelId">The model identifier.</param>
        /// <param name="finishReason">The reason the generation finished.</param>
        /// <returns>A chat completion chunk with the finish reason.</returns>
        private CoreModels.ChatCompletionChunk CreateFinalStreamChunk(string modelId, string finishReason)
        {
            return new CoreModels.ChatCompletionChunk
            {
                Id = $"chatcmpl-{Guid.NewGuid():N}",
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = modelId,
                Choices = new List<CoreModels.StreamingChoice>
                {
                    new CoreModels.StreamingChoice
                    {
                        Index = 0,
                        FinishReason = finishReason,
                        Delta = new CoreModels.DeltaContent()
                    }
                }
            };
        }

        /// <summary>
        /// Maps an Anthropic stream event to a chat completion chunk.
        /// </summary>
        /// <param name="chunk">The Anthropic message stream event.</param>
        /// <param name="modelId">The model identifier.</param>
        /// <returns>A chat completion chunk.</returns>
        private CoreModels.ChatCompletionChunk MapFromAnthropicStreamEvent(AnthropicMessageStreamEvent chunk, string modelId)
        {
            return new CoreModels.ChatCompletionChunk
            {
                Id = $"chatcmpl-{Guid.NewGuid():N}",
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = modelId,
                Choices = new List<CoreModels.StreamingChoice>
                {
                    new CoreModels.StreamingChoice
                    {
                        Index = 0,
                        Delta = new CoreModels.DeltaContent
                        {
                            Content = chunk.Delta?.Text,
                            Role = "assistant"
                        }
                    }
                }
            };
        }
        
        /// <summary>
        /// Gets available models from the Anthropic API.
        /// </summary>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A list of available models.</returns>
        /// <remarks>
        /// <para>
        /// Anthropic does not provide a models endpoint like other LLM providers. Instead,
        /// this method returns a static list of known Anthropic models. The current list includes:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>Claude 3 models (Opus, Sonnet, Haiku)</description></item>
        ///   <item><description>Claude 2.1 and 2.0</description></item>
        ///   <item><description>Claude Instant 1.2</description></item>
        /// </list>
        /// <para>
        /// This method is implemented as a Task for consistency with the interface, but
        /// it doesn't actually make any API requests.
        /// </para>
        /// </remarks>
        public override async Task<List<InternalModels.ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Anthropic doesn't have a models endpoint, so we return a static list of supported models
            return await Task.FromResult(new List<InternalModels.ExtendedModelInfo>
            {
                InternalModels.ExtendedModelInfo.Create("claude-3-opus-20240229", "anthropic", "claude-3-opus-20240229"),
                InternalModels.ExtendedModelInfo.Create("claude-3-sonnet-20240229", "anthropic", "claude-3-sonnet-20240229"),
                InternalModels.ExtendedModelInfo.Create("claude-3-haiku-20240307", "anthropic", "claude-3-haiku-20240307"),
                InternalModels.ExtendedModelInfo.Create("claude-2.1", "anthropic", "claude-2.1"),
                InternalModels.ExtendedModelInfo.Create("claude-2.0", "anthropic", "claude-2.0"),
                InternalModels.ExtendedModelInfo.Create("claude-instant-1.2", "anthropic", "claude-instant-1.2")
            });
        }

        /// <summary>
        /// Creates embeddings using the Anthropic API (not currently supported).
        /// </summary>
        /// <param name="request">The embedding request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>This method does not return as it throws a NotSupportedException.</returns>
        /// <remarks>
        /// <para>
        /// As of early 2025, Anthropic does not provide a public embeddings API.
        /// This method is implemented to fulfill the ILLMClient interface but will
        /// always throw a NotSupportedException.
        /// </para>
        /// <para>
        /// If Anthropic adds embedding support in the future, this method should be
        /// updated to implement the actual API call.
        /// </para>
        /// </remarks>
        /// <exception cref="NotSupportedException">Always thrown as embeddings are not supported by Anthropic.</exception>
        public override Task<CoreModels.EmbeddingResponse> CreateEmbeddingAsync(
            CoreModels.EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Embeddings are not currently supported by the Anthropic API");
        }

        /// <summary>
        /// Generates images using the Anthropic API (not currently supported).
        /// </summary>
        /// <param name="request">The image generation request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>This method does not return as it throws a NotSupportedException.</returns>
        /// <remarks>
        /// <para>
        /// As of early 2025, Anthropic does not provide an image generation API.
        /// Although Claude models can analyze images, they cannot generate them.
        /// This method is implemented to fulfill the ILLMClient interface but will
        /// always throw a NotSupportedException.
        /// </para>
        /// <para>
        /// If Anthropic adds image generation support in the future, this method should
        /// be updated to implement the actual API call.
        /// </para>
        /// </remarks>
        /// <exception cref="NotSupportedException">Always thrown as image generation is not supported by Anthropic.</exception>
        public override Task<CoreModels.ImageGenerationResponse> CreateImageAsync(
            CoreModels.ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Image generation is not currently supported by the Anthropic API");
        }

        /// <summary>
        /// Maps the provider-agnostic request to Anthropic API format.
        /// </summary>
        /// <param name="request">The generic chat completion request to map.</param>
        /// <returns>An Anthropic-compatible message request.</returns>
        /// <remarks>
        /// <para>
        /// This method handles several key transformations specific to the Anthropic API:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>Extracts system messages and moves them to Anthropic's dedicated system prompt field</description></item>
        ///   <item><description>Processes user and assistant messages into the format expected by Anthropic</description></item>
        ///   <item><description>Handles tool calls using Anthropic's content blocks format</description></item>
        ///   <item><description>Manages tool responses (tool_result) with the appropriate format</description></item>
        ///   <item><description>Maps common parameters like temperature and top_p</description></item>
        /// </list>
        /// <para>
        /// Special handling is implemented for multimodal content and tool usage, converting
        /// from the provider-agnostic format to Anthropic's content blocks structure.
        /// </para>
        /// </remarks>
        private AnthropicMessageRequest MapToAnthropicRequest(CoreModels.ChatCompletionRequest request)
        {
            string systemPrompt = "";

            // Extract the system message if present
            var userAndAssistantMessages = new List<AnthropicMessage>();
            foreach (var message in request.Messages)
            {
                if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    // Anthropic uses a dedicated system prompt field instead of a message
                    systemPrompt = ContentHelper.GetContentAsString(message.Content);
                }
                else if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase) || 
                         message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    // Process standard messages
                    AnthropicMessage anthropicMessage;
                    
                    // Handle tool calls and results if present
                    if (message.ToolCalls != null && message.ToolCalls.Count > 0)
                    {
                        // Convert to Anthropic's content format
                        var contentParts = ContentHelper.ExtractMultimodalContent(message.Content ?? "");
                        var contentBlocks = contentParts.Select(part => 
                            new AnthropicContentBlock { Type = "text", Text = part }).ToList();
                        
                        // Add tool calls at the end
                        foreach (var toolCall in message.ToolCalls)
                        {
                            contentBlocks.Add(new AnthropicContentBlock { 
                                Type = "tool_use", 
                                Id = toolCall.Id,
                                Name = toolCall.Function?.Name ?? "",
                                Input = toolCall.Function?.Arguments ?? "{}"
                            });
                        }
                        
                        anthropicMessage = new AnthropicMessage
                        {
                            Role = message.Role.ToLowerInvariant(),
                            Content = contentBlocks
                        };
                    }
                    else if (message.ToolCallId != null)
                    {
                        // Tool result message
                        anthropicMessage = new AnthropicMessage
                        {
                            Role = message.Role.ToLowerInvariant(),
                            Content = new List<AnthropicContentBlock> {
                                new AnthropicContentBlock {
                                    Type = "tool_result",
                                    ToolCallId = message.ToolCallId,
                                    Content = ContentHelper.GetContentAsString(message.Content)
                                }
                            }
                        };
                    }
                    else
                    {
                        // Standard text message
                        anthropicMessage = new AnthropicMessage
                        {
                            Role = message.Role.ToLowerInvariant(),
                            Content = ContentHelper.GetContentAsString(message.Content)
                        };
                    }
                    
                    userAndAssistantMessages.Add(anthropicMessage);
                }
                // Ignore any other role types (like function)
            }

            // Create the Anthropic request
            var anthropicRequest = new AnthropicMessageRequest
            {
                Model = request.Model ?? ProviderModelId,
                Messages = userAndAssistantMessages,
                SystemPrompt = !string.IsNullOrEmpty(systemPrompt) ? systemPrompt : null,
                MaxTokens = request.MaxTokens ?? 4096, // Default max tokens if not specified
                Temperature = (float?)request.Temperature,
                TopP = (float?)request.TopP,
                Stream = request.Stream ?? false,
                StopSequences = request.Stop
            };

            return anthropicRequest;
        }

        /// <summary>
        /// Maps the Anthropic API response to provider-agnostic format.
        /// </summary>
        /// <param name="response">The response from the Anthropic API.</param>
        /// <param name="originalModelAlias">The original model alias from the request.</param>
        /// <returns>A provider-agnostic chat completion response.</returns>
        /// <remarks>
        /// <para>
        /// This method transforms the Anthropic-specific response structure into the standardized
        /// format used throughout the application. Key transformations include:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>Converting Anthropic's content blocks into a single content string and/or tool calls</description></item>
        ///   <item><description>Mapping Anthropic's usage metrics (input_tokens, output_tokens) to standard format</description></item>
        ///   <item><description>Preserving the original model alias if it was different from the provider model ID</description></item>
        ///   <item><description>Standardizing object types and structure to match the OpenAI-like format used across the application</description></item>
        /// </list>
        /// <para>
        /// When processing content, this method handles both string content and content blocks,
        /// extracting text blocks and tool use blocks into their respective formats.
        /// </para>
        /// </remarks>
        private CoreModels.ChatCompletionResponse MapFromAnthropicResponse(
            AnthropicMessageResponse response, 
            string? originalModelAlias)
        {
            string responseContent = "";
            List<CoreModels.ToolCall>? toolCalls = null;
            
            // Process the content of the response, which could be a string or blocks
            if (response.Content is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                var textContent = new StringBuilder();
                var toolUseBlocks = new List<JsonElement>();
                
                // Process each content block
                foreach (JsonElement block in jsonElement.EnumerateArray())
                {
                    if (block.TryGetProperty("type", out var typeElement))
                    {
                        string? blockType = typeElement.GetString();
                        
                        if (blockType == "text" && block.TryGetProperty("text", out var textElement))
                        {
                            textContent.Append(textElement.GetString());
                        }
                        else if (blockType == "tool_use")
                        {
                            toolUseBlocks.Add(block);
                        }
                    }
                }
                
                responseContent = textContent.ToString();
                
                // Process tool calls if any
                if (toolUseBlocks.Count > 0)
                {
                    toolCalls = new List<CoreModels.ToolCall>();
                    
                    foreach (var block in toolUseBlocks)
                    {
                        string? id = null;
                        string? name = null;
                        string? input = null;
                        
                        if (block.TryGetProperty("id", out var idElement))
                            id = idElement.GetString();
                            
                        if (block.TryGetProperty("name", out var nameElement))
                            name = nameElement.GetString();
                            
                        if (block.TryGetProperty("input", out var inputElement))
                            input = inputElement.GetString();
                            
                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                        {
                            toolCalls.Add(new CoreModels.ToolCall
                            {
                                Id = id,
                                Type = "function", // Standardize as "function" in our API
                                Function = new CoreModels.FunctionCall
                                {
                                    Name = name,
                                    Arguments = input ?? "{}"
                                }
                            });
                        }
                    }
                }
            }
            else
            {
                // Fallback for string content or other content types
                responseContent = ContentHelper.GetContentAsString(response.Content);
            }
            
            // Create the standardized response
            var result = new CoreModels.ChatCompletionResponse
            {
                Id = response.Id ?? Guid.NewGuid().ToString(),
                Object = "chat.completion", // Standardize as OpenAI-like type
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = response.Model ?? "unknown-model",
                Choices = new List<CoreModels.Choice>
                {
                    new CoreModels.Choice
                    {
                        Index = 0,
                        Message = new CoreModels.Message
                        {
                            Role = "assistant",
                            Content = responseContent ?? string.Empty,
                            ToolCalls = toolCalls
                        },
                        FinishReason = response.StopReason ?? "unknown"
                    }
                },
                Usage = new CoreModels.Usage
                {
                    PromptTokens = response.Usage.InputTokens,
                    CompletionTokens = response.Usage.OutputTokens,
                    TotalTokens = response.Usage.InputTokens + response.Usage.OutputTokens
                },
                OriginalModelAlias = originalModelAlias
            };
            
            return result;
        }

        /// <summary>
        /// Maps the Anthropic streaming event to provider-agnostic chat completion chunk format.
        /// </summary>
        /// <param name="streamEvent">The streaming event from the Anthropic API.</param>
        /// <param name="originalModelAlias">The original model alias from the request.</param>
        /// <returns>A provider-agnostic chat completion chunk.</returns>
        /// <remarks>
        /// <para>
        /// This method transforms Anthropic's streaming event format into the standardized
        /// chat completion chunk format used throughout the application. Anthropic uses a
        /// different streaming format than the standard OpenAI-like format, so this method
        /// handles the conversion.
        /// </para>
        /// <para>
        /// Key transformations include:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>Converting content_block_delta events to text chunks</description></item>
        ///   <item><description>Including the assistant role only in the first chunk</description></item>
        ///   <item><description>Creating a standardized chunk ID if none is provided by Anthropic</description></item>
        ///   <item><description>Preserving the original model alias if it was different from the provider model ID</description></item>
        /// </list>
        /// <para>
        /// Note that finish_reason is not included in these chunks; it's added in a final chunk
        /// when a message_stop event is received.
        /// </para>
        /// </remarks>
        
        /// <summary>
        /// Creates a final chunk with finish reason for streaming responses.
        /// </summary>
        /// <param name="originalModelAlias">The original model alias from the request.</param>
        /// <param name="finishReason">The reason why the generation stopped.</param>
        /// <returns>A provider-agnostic chat completion chunk with finish reason.</returns>
        /// <remarks>
        /// <para>
        /// This method creates a special final chunk to indicate the end of a streaming response
        /// and includes a finish reason. In the Anthropic API, the finish reason comes in a
        /// separate message_stop event rather than being included in the content deltas.
        /// </para>
        /// <para>
        /// The finish reason is important for the client to understand why the generation stopped,
        /// such as reaching a natural conclusion (stop), hitting token limits (length), or
        /// encountering other stopping conditions.
        /// </para>
        /// <para>
        /// This final chunk contains no content, only the finish_reason field, and follows the
        /// OpenAI-compatible streaming format.
        /// </para>
        /// </remarks>
    }
}