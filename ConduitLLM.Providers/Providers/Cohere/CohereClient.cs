using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.Common.Models;
using ConduitLLM.Providers.Providers.Cohere.Models;
using ConduitLLM.Providers.Utilities;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.Cohere
{
    /// <summary>
    /// Client for interacting with the Cohere API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This client implements the ILLMClient interface for Cohere's chat API.
    /// It supports both regular and streaming chat completions.
    /// </para>
    /// <para>
    /// Key features:
    /// - Maps between Conduit's unified interface and Cohere's API structure
    /// - Handles Cohere-specific authentication and endpoint configuration
    /// - Provides comprehensive error handling and appropriate error messages
    /// - Supports streaming responses with proper SSE parsing
    /// </para>
    /// <para>
    /// Cohere's API differs from OpenAI's in several ways, including different
    /// message formats and response structures. This client handles the mapping
    /// to provide a consistent experience regardless of the underlying provider.
    /// </para>
    /// </remarks>
    public class CohereClient : CustomProviderClient
    {
        private const string DefaultBaseUrl = "https://api.cohere.ai";
        private const string ChatEndpoint = "v1/chat";

        /// <summary>
        /// Initializes a new instance of the <see cref="CohereClient"/> class.
        /// </summary>
        /// <param name="credentials">LLMProvider credentials containing API key and endpoint configuration.</param>
        /// <param name="providerModelId">The specific Cohere model ID to use (e.g., command-r-plus).</param>
        /// <param name="logger">Logger for recording diagnostic information.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        /// <exception cref="ArgumentNullException">Thrown when credentials, providerModelId, or logger is null.</exception>
        /// <exception cref="ConfigurationException">Thrown when API key is missing in the credentials.</exception>
        public CohereClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger<CohereClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                  provider,
                  keyCredential,
                  providerModelId,
                  logger,
                  httpClientFactory,
                  "cohere",
                  baseUrl: null,
                  defaultModels: defaultModels)
        {
        }

        /// <summary>
        /// Configures the HttpClient with Cohere-specific settings.
        /// </summary>
        /// <param name="client">The HTTP client to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        /// <remarks>
        /// <para>
        /// This method adds the required headers for the Cohere API.
        /// Cohere uses a Bearer token in the Authorization header for authentication.
        /// </para>
        /// </remarks>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            base.ConfigureHttpClient(client, apiKey);

            // Set Cohere-specific headers
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        /// <inheritdoc/>
        public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "ChatCompletion");

            return await ExecuteApiRequestAsync(async () =>
            {
                using var client = CreateHttpClient(apiKey);
                var cohereRequest = MapToCohereRequest(request);

                var endpoint = ChatEndpoint;
                Logger.LogDebug("Sending chat completion request to Cohere API at {Endpoint}", endpoint);

                var response = await ConduitLLM.Core.Utilities.HttpClientHelper.SendJsonRequestAsync<CohereChatRequest, CohereChatResponse>(
                    client,
                    HttpMethod.Post,
                    endpoint,
                    cohereRequest,
                    null,
                    DefaultSerializerOptions,
                    Logger,
                    cancellationToken);

                return MapFromCohereResponse(response, request.Model);
            }, "ChatCompletion", cancellationToken);
        }

        /// <inheritdoc/>
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "StreamChatCompletion");

            // Initialize variables outside try/catch so they're accessible throughout the method
            StreamReader? reader = null;
            HttpResponseMessage? response = null;

            try
            {
                using var client = CreateHttpClient(apiKey);

                // Map request and ensure streaming is enabled
                var cohereRequest = MapToCohereRequest(request) with { Stream = true };

                var endpoint = ChatEndpoint;
                Logger.LogDebug("Sending streaming chat completion request to Cohere API at {Endpoint}", endpoint);

                response = await ConduitLLM.Core.Utilities.HttpClientHelper.SendStreamingRequestAsync(
                    client,
                    HttpMethod.Post,
                    endpoint,
                    cohereRequest,
                    null,
                    DefaultSerializerOptions,
                    Logger,
                    cancellationToken);

                // Set up streaming resources
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                reader = new StreamReader(stream, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Enhance the error with provider-specific details
                Logger.LogError(ex, "Error initializing streaming chat completion from Cohere");
                throw ExceptionHandler.HandleLlmException(ex, Logger, ProviderName, request.Model ?? ProviderModelId);
            }

            // Process the streaming response outside try/catch for yield returns
            if (reader != null)
            {
                using (reader) // Ensure proper disposal
                {
                    string? generationId = null;
                    bool isFirstChunk = true;

                    while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                    {
                        string? line;
                        ChatCompletionChunk? chunkToYield = null;

                        try
                        {
                            line = await reader.ReadLineAsync();
                            if (string.IsNullOrWhiteSpace(line))
                            {
                                continue;
                            }

                            Logger.LogTrace("Received stream line: {Line}", line);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            Logger.LogError(ex, "Error reading from Cohere stream");
                            throw new LLMCommunicationException($"Error reading from Cohere stream: {ex.Message}", ex);
                        }

                        try
                        {
                            // Parse the basic event structure to get the event type
                            var baseEvent = JsonSerializer.Deserialize<CohereStreamEventBase>(line, DefaultSerializerOptions);
                            if (baseEvent == null || string.IsNullOrEmpty(baseEvent.EventType))
                            {
                                continue;
                            }

                            // Now process different event types and yield results
                            // This is now outside the main try/catch block so we can safely yield

                            switch (baseEvent.EventType)
                            {
                                case "stream-start":
                                    var startEvent = JsonSerializer.Deserialize<CohereStreamStartEvent>(line, DefaultSerializerOptions);
                                    generationId = startEvent?.GenerationId ?? $"coherestream-{Guid.NewGuid():N}";
                                    Logger.LogDebug("Cohere stream started with generation ID: {GenerationId}", generationId);
                                    // No chunk to yield for stream-start
                                    break;

                                case "text-generation":
                                    var textEvent = JsonSerializer.Deserialize<CohereTextGenerationEvent>(line, DefaultSerializerOptions);
                                    if (textEvent != null && !string.IsNullOrEmpty(textEvent.Text))
                                    {
                                        // Prepare a chunk for this text generation event
                                        chunkToYield = CreateChatCompletionChunk(
                                            textEvent.Text,
                                            request.Model ?? ProviderModelId,
                                            isFirstChunk,
                                            null,
                                            request.Model
                                        );

                                        // Only the first chunk needs the assistant role
                                        if (isFirstChunk)
                                        {
                                            isFirstChunk = false;
                                        }
                                    }
                                    break;

                                case "stream-end":
                                    var endEvent = JsonSerializer.Deserialize<CohereStreamEndEvent>(line, DefaultSerializerOptions);
                                    if (endEvent != null)
                                    {
                                        // Map Cohere finish reason to standardized format
                                        string finishReason = MapFinishReason(endEvent.FinishReason);

                                        // Prepare a final chunk with the finish reason
                                        chunkToYield = CreateChatCompletionChunk(
                                            "", // Empty content for final chunk
                                            request.Model ?? ProviderModelId,
                                            false,
                                            finishReason,
                                            request.Model
                                        );

                                        Logger.LogDebug("Cohere stream ended with finish reason: {FinishReason}", finishReason);
                                    }
                                    break;

                                // Ignore other event types for now
                                default:
                                    Logger.LogTrace("Ignoring Cohere event type: {EventType}", baseEvent.EventType);
                                    break;
                            }
                        }
                        catch (JsonException ex)
                        {
                            Logger.LogError(ex, "Error deserializing Cohere stream event: {Line}", line);
                            throw new LLMCommunicationException($"Error processing Cohere stream: {ex.Message}", ex);
                        }

                        // Now we can safely yield outside the try-catch block
                        if (chunkToYield != null)
                        {
                            yield return chunkToYield;
                        }
                    }
                }
            }

            // No need for a final catch block since we've moved all error handling into specific contexts
        }

        /// <inheritdoc/>
        public override async Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Cohere doesn't have a public model listing endpoint, so we return a static list
            return await Task.FromResult(new List<ConduitLLM.Providers.Common.Models.ExtendedModelInfo>
            {
                ConduitLLM.Providers.Common.Models.ExtendedModelInfo.Create("command-r-plus", "cohere", "command-r-plus"),
                ConduitLLM.Providers.Common.Models.ExtendedModelInfo.Create("command-r", "cohere", "command-r"),
                ConduitLLM.Providers.Common.Models.ExtendedModelInfo.Create("command", "cohere", "command"),
                ConduitLLM.Providers.Common.Models.ExtendedModelInfo.Create("command-light", "cohere", "command-light"),
                ConduitLLM.Providers.Common.Models.ExtendedModelInfo.Create("command-nightly", "cohere", "command-nightly"),
                ConduitLLM.Providers.Common.Models.ExtendedModelInfo.Create("command-light-nightly", "cohere", "command-light-nightly")
            });
        }

        /// <inheritdoc/>
        public override async Task<EmbeddingResponse> CreateEmbeddingAsync(
            EmbeddingRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Validate input
            if (request.Input == null)
            {
                throw new ValidationException("Input text is required for embeddings");
            }

            // Prepare the texts array
            var texts = new List<string>();
            if (request.Input is string singleText)
            {
                texts.Add(singleText);
            }
            else if (request.Input is IEnumerable<string> multipleTexts)
            {
                texts.AddRange(multipleTexts);
            }
            else
            {
                throw new ValidationException("Input must be a string or array of strings");
            }

            if (texts.Count == 0)
            {
                throw new ValidationException("At least one input text is required");
            }

            // Create the Cohere-specific request
            var cohereRequest = new
            {
                texts = texts,
                model = request.Model ?? "embed-english-v3.0", // Default Cohere embedding model
                input_type = "search_document", // Can be "search_document", "search_query", "classification", "clustering"
                truncate = "END" // Truncate at the end if text is too long
            };

            using var httpClient = CreateHttpClient(apiKey);
            var requestJson = JsonSerializer.Serialize(cohereRequest, DefaultJsonOptions);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync("embed", content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    // Handle error response
                    Logger.LogError("Cohere API request failed with status code {StatusCode}. Response: {ErrorContent}",
                        response.StatusCode, responseBody);
                    throw new LLMCommunicationException(
                        $"Cohere API request failed with status code {response.StatusCode}. Response: {responseBody}");
                }

                // Parse Cohere response
                var cohereResponse = JsonSerializer.Deserialize<CohereEmbedResponse>(responseBody, DefaultJsonOptions);
                if (cohereResponse?.Embeddings == null)
                {
                    throw new LLMCommunicationException(
                        "Invalid response from Cohere API");
                }

                // Map to standard embedding response
                var embeddingData = new List<EmbeddingData>();
                for (int i = 0; i < cohereResponse.Embeddings.Count; i++)
                {
                    embeddingData.Add(new EmbeddingData
                    {
                        Object = "embedding",
                        Embedding = cohereResponse.Embeddings[i],
                        Index = i
                    });
                }

                // Calculate token usage (Cohere provides this in meta)
                var usage = new Usage
                {
                    PromptTokens = cohereResponse.Meta?.BilledUnits?.InputTokens ?? 0,
                    CompletionTokens = 0, // Embeddings don't have completion tokens
                    TotalTokens = cohereResponse.Meta?.BilledUnits?.InputTokens ?? 0
                };

                return new EmbeddingResponse
                {
                    Object = "list",
                    Data = embeddingData,
                    Model = request.Model ?? "embed-english-v3.0",
                    Usage = usage
                };
            }
            catch (HttpRequestException ex)
            {
                throw new LLMCommunicationException(
                    $"Error communicating with Cohere API: {ex.Message}",
                    ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new LLMCommunicationException(
                    "Request to Cohere API timed out",
                    ex);
            }
        }

        /// <inheritdoc/>
        public override Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Cohere doesn't support image generation as of 2025
            throw new NotSupportedException("Image generation is not supported by the Cohere API");
        }

        /// <summary>
        /// Maps the provider-agnostic request to Cohere API format.
        /// </summary>
        /// <param name="request">The generic chat completion request.</param>
        /// <returns>A Cohere-specific chat request.</returns>
        /// <remarks>
        /// <para>
        /// This method transforms the standardized request format into Cohere's specific API format.
        /// Key transformations include:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>Converting the message array to Cohere's message + chat_history format</description></item>
        ///   <item><description>Extracting the system message into Cohere's preamble field</description></item>
        ///   <item><description>Mapping standardized parameters to Cohere's parameter names</description></item>
        /// </list>
        /// <para>
        /// The Cohere API expects the most recent user message separate from the chat history,
        /// unlike the unified messages array in the standard format.
        /// </para>
        /// </remarks>
        private CohereChatRequest MapToCohereRequest(ChatCompletionRequest request)
        {
            // Extract chat history and system message (preamble)
            var history = new List<CohereMessage>();
            string? preamble = null;

            // Process all but the last user message
            foreach (var message in request.Messages)
            {
                // Extract system message as preamble
                if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    preamble = ContentHelper.GetContentAsString(message.Content);
                    continue;
                }

                // Skip the last user message as it will be set as the primary message
                if (message == request.Messages.LastOrDefault(m =>
                    m.Role.Equals("user", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                // Map non-system messages to Cohere's format
                string role = message.Role.ToLowerInvariant() switch
                {
                    "user" => "USER",
                    "assistant" => "CHATBOT",
                    "tool" => "TOOL", // Not standard in Cohere but added for completeness
                    _ => string.Empty
                };

                if (string.IsNullOrEmpty(role))
                {
                    Logger.LogWarning("Unsupported message role '{Role}' encountered for Cohere chat history. Skipping message.", message.Role);
                    continue;
                }

                history.Add(new CohereMessage
                {
                    Role = role,
                    Message = ContentHelper.GetContentAsString(message.Content)
                });
            }

            // Get the last user message as the primary message
            string userMessage = request.Messages.LastOrDefault(m =>
                m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content != null
                ? ContentHelper.GetContentAsString(request.Messages.Last(m =>
                    m.Role.Equals("user", StringComparison.OrdinalIgnoreCase)).Content)
                : string.Empty;

            if (string.IsNullOrEmpty(userMessage))
            {
                Logger.LogWarning("No user message found in request for Cohere. Using empty message.");
            }

            // Create the Cohere request
            var cohereRequest = new CohereChatRequest
            {
                Model = ProviderModelId,
                Message = userMessage,
                ChatHistory = history.Count > 0 ? history : null,
                Temperature = ParameterConverter.ToTemperature(request.Temperature),
                Preamble = preamble,
                MaxTokens = request.MaxTokens,
                // Map top_p to Cohere's p parameter if provided
                P = ParameterConverter.ToProbability(request.TopP, 0.0, 1.0),
                // Map TopK to Cohere's K parameter
                K = request.TopK,
                // Map penalty parameters
                PresencePenalty = ParameterConverter.ToProbability(request.PresencePenalty),
                FrequencyPenalty = ParameterConverter.ToProbability(request.FrequencyPenalty),
                // Map seed for deterministic generation
                Seed = request.Seed,
                // Map stop to Cohere's stop_sequences if provided
                StopSequences = request.Stop
            };

            return cohereRequest;
        }

        /// <summary>
        /// Maps the Cohere API response to provider-agnostic format.
        /// </summary>
        /// <param name="response">The response from the Cohere API.</param>
        /// <param name="originalModelAlias">The original model alias from the request.</param>
        /// <returns>A provider-agnostic chat completion response.</returns>
        /// <remarks>
        /// <para>
        /// This method transforms Cohere's response format into the standardized format
        /// used throughout the application. Key transformations include:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>Setting the assistant role for the response message</description></item>
        ///   <item><description>Mapping Cohere's finish reason to standardized finish reasons</description></item>
        ///   <item><description>Extracting token usage information from Cohere's meta field</description></item>
        /// </list>
        /// </remarks>
        private ChatCompletionResponse MapFromCohereResponse(CohereChatResponse response, string? originalModelAlias)
        {
            // Extract prompt and completion tokens from response metadata
            int promptTokens = 0;
            int completionTokens = 0;

            // Try both tokens and billedUnits fields
            if (response.Meta?.Tokens != null)
            {
                promptTokens = response.Meta.Tokens.InputTokens ?? 0;
                completionTokens = response.Meta.Tokens.OutputTokens ?? 0;
            }
            else if (response.Meta?.BilledUnits != null)
            {
                promptTokens = response.Meta.BilledUnits.InputTokens ?? 0;
                completionTokens = response.Meta.BilledUnits.OutputTokens ?? 0;
            }

            // Create and return the standardized response
            return new ChatCompletionResponse
            {
                Id = response.GenerationId ?? Guid.NewGuid().ToString(),
                Object = "chat.completion", // Mimic OpenAI structure
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), // Use current time
                Model = originalModelAlias ?? ProviderModelId, // Return the alias the user requested
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new Message
                        {
                            Role = "assistant",
                            Content = response.Text
                        },
                        FinishReason = MapFinishReason(response.FinishReason)
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    TotalTokens = promptTokens + completionTokens
                },
                OriginalModelAlias = originalModelAlias
            };
        }

        /// <summary>
        /// Maps Cohere's finish reason to the standardized format.
        /// </summary>
        /// <param name="cohereFinishReason">The finish reason from Cohere's API.</param>
        /// <returns>A standardized finish reason string.</returns>
        /// <remarks>
        /// Cohere uses different finish reason strings than the standard format.
        /// This method maps them to the standard values (stop, length, content_filter, etc.)
        /// for consistency across providers.
        /// </remarks>
        private string MapFinishReason(string? cohereFinishReason)
        {
            return cohereFinishReason switch
            {
                "COMPLETE" => "stop",
                "MAX_TOKENS" => "length",
                "ERROR_TOXIC" => "content_filter",
                "ERROR_LIMIT" => "error", // Map rate limit or other limits to a generic error
                "ERROR" => "error",
                "USER_CANCEL" => "stop", // Treating cancelation as a normal stop
                null => "stop", // Default to stop for null values
                _ => cohereFinishReason.ToLowerInvariant() // Pass through unknown values in lowercase
            };
        }

        #region Authentication Verification

        /// <summary>
        /// Verifies Cohere authentication by making a test request to the models endpoint.
        /// </summary>
        public override async Task<Core.Interfaces.AuthenticationResult> VerifyAuthenticationAsync(
            string? apiKey = null,
            string? baseUrl = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : PrimaryKeyCredential.ApiKey;
                
                if (string.IsNullOrWhiteSpace(effectiveApiKey))
                {
                    return Core.Interfaces.AuthenticationResult.Failure(
                        "API key is required",
                        "No API key provided for Cohere authentication");
                }

                // Create a test client
                using var client = CreateHttpClient(effectiveApiKey);
                
                // Make a request to the models endpoint
                var modelsUrl = $"{GetHealthCheckUrl(baseUrl)}/v1/models";
                var response = await client.GetAsync(modelsUrl, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                Logger.LogInformation("Cohere auth check returned status {StatusCode}", response.StatusCode);

                // Check for authentication errors
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return Core.Interfaces.AuthenticationResult.Failure(
                        "Authentication failed",
                        "Invalid API key - Cohere requires a valid API key");
                }
                
                if (response.IsSuccessStatusCode)
                {
                    return Core.Interfaces.AuthenticationResult.Success(
                        "Connected successfully to Cohere API",
                        responseTime);
                }

                // Other errors
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Unexpected response: {response.StatusCode}",
                    await response.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error verifying Cohere authentication");
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <summary>
        /// Gets the health check URL for Cohere.
        /// </summary>
        public override string GetHealthCheckUrl(string? baseUrl = null)
        {
            var effectiveBaseUrl = !string.IsNullOrWhiteSpace(baseUrl) 
                ? baseUrl.TrimEnd('/') 
                : (!string.IsNullOrWhiteSpace(Provider.BaseUrl) 
                    ? Provider.BaseUrl.TrimEnd('/') 
                    : DefaultBaseUrl.TrimEnd('/'));
            
            return effectiveBaseUrl;
        }

        /// <summary>
        /// Gets the default base URL for Cohere.
        /// </summary>
        protected override string GetDefaultBaseUrl()
        {
            return DefaultBaseUrl;
        }

        #endregion
    }
}
