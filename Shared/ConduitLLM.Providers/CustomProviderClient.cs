using System.Text.Json;
using System.Text.Json.Serialization;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Base class for LLM client implementations with custom APIs that don't follow the OpenAI-compatible pattern.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This abstract class serves as a foundation for providers with unique API formats that
    /// differ significantly from the OpenAI API structure, such as Anthropic, Cohere, Gemini, and others.
    /// </para>
    /// <para>
    /// It provides a standardized implementation framework for the <see cref="ILLMClient"/> interface
    /// with customizable methods that derived classes must implement to accommodate provider-specific
    /// behaviors while maintaining a consistent interface.
    /// </para>
    /// <para>
    /// Key features:
    /// - Common API request and error handling utilities
    /// - Centralized credential validation and HTTP client configuration
    /// - Standardized logging and exception management
    /// - Abstractions for provider-specific API request and response mapping
    /// </para>
    /// <para>
    /// Derived classes must implement the abstract methods to provide provider-specific functionality
    /// and can override virtual methods to customize behavior as needed.
    /// </para>
    /// </remarks>
    public abstract class CustomProviderClient : BaseLLMClient
    {
        /// <summary>
        /// Gets the base URL for API requests.
        /// </summary>
        protected readonly string BaseUrl;

        /// <summary>
        /// Gets the default JSON serialization options.
        /// </summary>
        protected static readonly JsonSerializerOptions DefaultSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomProviderClient"/> class.
        /// </summary>
        /// <param name="credentials">The provider credentials.</param>
        /// <param name="providerModelId">The provider's model identifier.</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory.</param>
        /// <param name="providerName">The name of this LLM provider.</param>
        /// <param name="baseUrl">The base URL for API requests.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        protected CustomProviderClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger logger,
            IHttpClientFactory? httpClientFactory = null,
            string? providerName = null,
            string? baseUrl = null,
            ProviderDefaultModels? defaultModels = null)
            : base(provider, keyCredential, providerModelId, logger, httpClientFactory, providerName, defaultModels)
        {
            BaseUrl = !string.IsNullOrEmpty(baseUrl)
                ? baseUrl
                : !string.IsNullOrEmpty(provider.BaseUrl)
                    ? provider.BaseUrl
                    : throw new ConfigurationException($"Base URL must be provided either directly or via BaseUrl in provider for {ProviderName}");
        }

        /// <summary>
        /// Configures the HttpClient with provider-specific settings.
        /// </summary>
        /// <param name="client">The HTTP client to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        /// <remarks>
        /// This method adds standard headers and sets the base address for the HTTP client.
        /// Derived classes should override this method to provide provider-specific configuration
        /// and call the base implementation to ensure common configuration is applied.
        /// </remarks>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            base.ConfigureHttpClient(client, apiKey);

            // Set the base address if not already set
            if (client.BaseAddress == null && !string.IsNullOrEmpty(BaseUrl))
            {
                client.BaseAddress = new Uri(BaseUrl.TrimEnd('/'));
            }
        }

        /// <summary>
        /// Validates a request before sending it to the API.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <param name="request">The request to validate.</param>
        /// <param name="operationName">The name of the operation for error messages.</param>
        /// <remarks>
        /// This method performs basic validation on the request.
        /// Derived classes should override this method to add provider-specific validation
        /// and call the base implementation to ensure common validation is performed.
        /// </remarks>
        protected override void ValidateRequest<TRequest>(TRequest request, string operationName)
        {
            base.ValidateRequest(request, operationName);

            // Add common validation for CustomProviderClient
            if (request is ChatCompletionRequest chatRequest)
            {
                if (chatRequest.Messages == null || chatRequest.Messages.Count() == 0)
                {
                    throw new ValidationException($"{operationName}: Messages cannot be null or empty");
                }
            }
            else if (request is EmbeddingRequest embeddingRequest)
            {
                if (embeddingRequest.Input == null)
                {
                    throw new ValidationException($"{operationName}: Input cannot be null");
                }
            }
            else if (request is ImageGenerationRequest imageRequest)
            {
                if (string.IsNullOrWhiteSpace(imageRequest.Prompt))
                {
                    throw new ValidationException($"{operationName}: Prompt cannot be null or empty");
                }
            }
        }

        /// <summary>
        /// Extracts a detailed error message from an HTTP response.
        /// </summary>
        /// <param name="response">The HTTP response.</param>
        /// <param name="errorJsonContent">The error content as a string.</param>
        /// <returns>A detailed error message.</returns>
        /// <remarks>
        /// This method attempts to extract a meaningful error message from a provider's error response.
        /// Different providers format their error messages differently, so derived classes should
        /// override this method to provide provider-specific error extraction logic.
        /// </remarks>
        protected virtual string ExtractErrorDetails(HttpResponseMessage response, string errorJsonContent)
        {
            if (string.IsNullOrEmpty(errorJsonContent))
            {
                return $"HTTP error {(int)response.StatusCode}: {response.ReasonPhrase}";
            }

            // Try to parse as JSON to extract error message
            try
            {
                var errorJson = JsonDocument.Parse(errorJsonContent);
                var errorRoot = errorJson.RootElement;

                // Try common error message paths
                if (errorRoot.TryGetProperty("error", out var errorObj))
                {
                    if (errorObj.TryGetProperty("message", out var messageObj))
                    {
                        return messageObj.GetString() ?? errorJsonContent;
                    }
                }

                // Try other common patterns
                if (errorRoot.TryGetProperty("message", out var directMessageObj))
                {
                    return directMessageObj.GetString() ?? errorJsonContent;
                }

                // Just return the raw content if we couldn't extract
                return errorJsonContent;
            }
            catch
            {
                // If parsing fails, return the raw content
                return errorJsonContent;
            }
        }

        /// <summary>
        /// Creates a final chat completion response when using custom mapping from provider-specific format.
        /// </summary>
        /// <param name="content">The content of the response.</param>
        /// <param name="model">The model used for generation.</param>
        /// <param name="promptTokens">The number of tokens in the prompt.</param>
        /// <param name="completionTokens">The number of tokens in the completion.</param>
        /// <param name="finishReason">The reason the generation finished.</param>
        /// <param name="originalModelAlias">The original model alias requested.</param>
        /// <returns>A standardized chat completion response.</returns>
        /// <remarks>
        /// This helper method creates a standardized chat completion response in the format expected
        /// by consumers of the ILLMClient interface. It helps derived classes implement consistent
        /// response mapping from provider-specific formats.
        /// </remarks>
        protected ChatCompletionResponse CreateChatCompletionResponse(
            string content,
            string model,
            int promptTokens,
            int completionTokens,
            string? finishReason = "stop",
            string? originalModelAlias = null)
        {
            return new ChatCompletionResponse
            {
                Id = $"chatcmpl-{Guid.NewGuid():N}",
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
                        FinishReason = finishReason ?? "stop"
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    TotalTokens = promptTokens + completionTokens
                }
            };
        }

        /// <summary>
        /// Creates a chat completion chunk for streaming responses when using custom mapping.
        /// </summary>
        /// <param name="content">The content of the chunk.</param>
        /// <param name="model">The model used for generation.</param>
        /// <param name="isFirst">Whether this is the first chunk in the stream.</param>
        /// <param name="finishReason">The reason the generation finished, only for final chunks.</param>
        /// <param name="originalModelAlias">The original model alias requested.</param>
        /// <returns>A standardized chat completion chunk.</returns>
        /// <remarks>
        /// This helper method creates a standardized chat completion chunk in the format expected
        /// by consumers of the ILLMClient interface. It helps derived classes implement consistent
        /// streaming response mapping from provider-specific formats.
        /// </remarks>
        protected ChatCompletionChunk CreateChatCompletionChunk(
            string content,
            string model,
            bool isFirst = false,
            string? finishReason = null,
            string? originalModelAlias = null)
        {
            return new ChatCompletionChunk
            {
                Id = $"chatcmpl-{Guid.NewGuid():N}",
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = model,
                Choices = new List<StreamingChoice>
                {
                    new StreamingChoice
                    {
                        Index = 0,
                        Delta = new DeltaContent
                        {
                            Role = isFirst ? "assistant" : null,
                            Content = content
                        },
                        FinishReason = finishReason
                    }
                }
            };
        }
    }
}
