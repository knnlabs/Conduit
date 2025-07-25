using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.InternalModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with Mistral AI's API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mistral AI uses the OpenAI-compatible API format with their own suite of language models
    /// ranging from smaller, efficient models to larger, more capable ones.
    /// </para>
    /// <para>
    /// This client leverages the OpenAI-compatible base implementation and adds
    /// Mistral-specific error handling and fallback mechanisms.
    /// </para>
    /// </remarks>
    public class MistralClient : OpenAICompatibleClient
    {
        // API configuration constants
        private static class Constants
        {
            public static class Urls
            {
                public const string DefaultBaseUrl = "https://api.mistral.ai/v1";
            }

            public static class Endpoints
            {
                public const string ChatCompletions = "/chat/completions";
                public const string Models = "/models";
                public const string Embeddings = "/embeddings";
            }

            public static class ErrorMessages
            {
                public const string ModelNotFound = "Model not found. Available Mistral models include: mistral-tiny, mistral-small, mistral-medium, mistral-large-latest, mistral-embed";
                public const string RateLimitExceeded = "Mistral API rate limit exceeded. Please try again later or reduce your request frequency.";
                public const string InvalidApiKey = "Invalid Mistral API key. Please check your credentials.";
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MistralClient"/> class.
        /// </summary>
        /// <param name="credentials">The credentials for the Mistral AI API.</param>
        /// <param name="providerModelId">The model ID to use.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public MistralClient(
            ProviderCredentials credentials,
            string providerModelId,
            ILogger<MistralClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                EnsureMistralCredentials(credentials),
                providerModelId,
                logger,
                httpClientFactory,
                "mistral",
                DetermineBaseUrl(credentials),
                defaultModels)
        {
        }

        /// <summary>
        /// Determines the base URL to use for the Mistral API.
        /// </summary>
        /// <param name="credentials">The provider credentials.</param>
        /// <returns>The API base URL with any trailing slashes removed.</returns>
        private static string DetermineBaseUrl(ProviderCredentials credentials)
        {
            return string.IsNullOrWhiteSpace(credentials.BaseUrl)
                ? Constants.Urls.DefaultBaseUrl
                : credentials.BaseUrl.TrimEnd('/');
        }

        /// <summary>
        /// Ensures the credentials are valid for Mistral AI and sets defaults if needed.
        /// </summary>
        /// <param name="credentials">The provider credentials to validate and normalize.</param>
        /// <returns>A normalized copy of the credentials with Mistral-specific defaults.</returns>
        /// <exception cref="ArgumentNullException">Thrown when credentials are null.</exception>
        /// <exception cref="ConfigurationException">Thrown when API key is missing.</exception>
        private static ProviderCredentials EnsureMistralCredentials(ProviderCredentials credentials)
        {
            if (credentials == null)
            {
                throw new ArgumentNullException(nameof(credentials));
            }

            if (string.IsNullOrWhiteSpace(credentials.ApiKey))
            {
                throw new ConfigurationException("API key is missing for Mistral AI provider.");
            }

            // Create a copy of the credentials to avoid modifying the original
            var mistralCredentials = new ProviderCredentials
            {
                ApiKey = credentials.ApiKey,
                BaseUrl = string.IsNullOrWhiteSpace(credentials.BaseUrl) ? Constants.Urls.DefaultBaseUrl : credentials.BaseUrl,
                ProviderName = "mistral"
            };

            return mistralCredentials;
        }

        /// <summary>
        /// Creates a chat completion with enhanced error handling specific to Mistral.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A chat completion response from Mistral.</returns>
        /// <exception cref="LLMCommunicationException">Thrown when there is a communication error with Mistral.</exception>
        public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await base.CreateChatCompletionAsync(request, apiKey, cancellationToken);
            }
            catch (LLMCommunicationException ex)
            {
                // Enhance error message handling for Mistral and re-throw
                var enhancedErrorMessage = ExtractEnhancedErrorMessage(ex);
                throw new LLMCommunicationException(enhancedErrorMessage, ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Handle other exceptions not caught by the base class
                var errorMessage = ex.Message;
                if (ex is HttpRequestException httpEx && httpEx.Data["Body"] is string body)
                {
                    errorMessage = body;
                }

                Logger.LogError(ex, "Mistral API error: {Message}", errorMessage);
                throw new LLMCommunicationException($"Mistral API error: {errorMessage}", ex);
            }
        }

        /// <summary>
        /// Streams a chat completion with enhanced error handling specific to Mistral.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable of chat completion chunks.</returns>
        /// <exception cref="LLMCommunicationException">Thrown when there is a communication error with Mistral.</exception>
        public override async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Create a wrapped stream to avoid yielding in try/catch
            IAsyncEnumerable<ChatCompletionChunk> baseStream;

            try
            {
                // Get the base implementation's stream
                baseStream = base.StreamChatCompletionAsync(request, apiKey, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Enhance error message handling for Mistral
                var enhancedErrorMessage = ExtractEnhancedErrorMessage(ex);
                Logger.LogError(ex, "Error initializing streaming chat completion from Mistral: {Message}", enhancedErrorMessage);
                throw new LLMCommunicationException(enhancedErrorMessage, ex);
            }

            // Process the stream outside of try/catch
            await foreach (var chunk in baseStream.WithCancellation(cancellationToken))
            {
                yield return chunk;
            }
        }

        /// <summary>
        /// Gets available models from the Mistral AI API or falls back to a predefined list.
        /// </summary>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A list of available models from Mistral.</returns>
        public override async Task<List<InternalModels.ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Attempt to use the generic OpenAI-compatible /models endpoint
                return await base.GetModelsAsync(apiKey, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to retrieve models from Mistral AI API. Returning known models.");
                return GetFallbackModels();
            }
        }

        /// <summary>
        /// Gets a fallback list of models for Mistral when the API is unavailable.
        /// </summary>
        /// <returns>A list of known Mistral models.</returns>
        protected override List<InternalModels.ExtendedModelInfo> GetFallbackModels()
        {
            // Use the default fallback models from the base class
            if (ProviderFallbackModels.TryGetValue("mistral", out var models))
            {
                return models;
            }

            // If for some reason the base class doesn't have Mistral models, provide them here
            return new List<InternalModels.ExtendedModelInfo>
            {
                InternalModels.ExtendedModelInfo.Create("mistral-tiny", "mistral", "mistral-tiny"),
                InternalModels.ExtendedModelInfo.Create("mistral-small", "mistral", "mistral-small"),
                InternalModels.ExtendedModelInfo.Create("mistral-medium", "mistral", "mistral-medium"),
                InternalModels.ExtendedModelInfo.Create("mistral-large-latest", "mistral", "mistral-large-latest"),
                InternalModels.ExtendedModelInfo.Create("mistral-embed", "mistral", "mistral-embed")
            };
        }

        /// <summary>
        /// Extracts a more helpful error message from exception details for Mistral errors.
        /// </summary>
        /// <param name="ex">The exception to extract information from.</param>
        /// <returns>An enhanced error message specific to Mistral errors.</returns>
        /// <remarks>
        /// This overrides the base implementation to provide more specific error extraction for Mistral.
        /// </remarks>
        protected override string ExtractEnhancedErrorMessage(Exception ex)
        {
            // Use the base implementation first
            var baseErrorMessage = base.ExtractEnhancedErrorMessage(ex);

            // If the base implementation found a useful message, return it
            if (!string.IsNullOrEmpty(baseErrorMessage) &&
                !baseErrorMessage.Equals(ex.Message) &&
                !baseErrorMessage.Contains("Exception of type"))
            {
                return baseErrorMessage;
            }

            // Mistral-specific error extraction
            var msg = ex.Message;

            // If we find "model not found" in the message, provide a more helpful message
            if (msg.Contains("model not found", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("The model", StringComparison.OrdinalIgnoreCase) &&
                msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
            {
                return Constants.ErrorMessages.ModelNotFound;
            }

            // For rate limit errors, provide a clearer message
            if (msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
            {
                return Constants.ErrorMessages.RateLimitExceeded;
            }

            // For common authentication errors
            if (msg.Contains("invalid api key", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("authentication", StringComparison.OrdinalIgnoreCase))
            {
                return Constants.ErrorMessages.InvalidApiKey;
            }

            // Look for Body data
            if (ex.Data.Contains("Body") && ex.Data["Body"] is string body && !string.IsNullOrEmpty(body))
            {
                return $"Mistral API error: {body}";
            }

            // Try inner exception
            if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
            {
                return $"Mistral API error: {ex.InnerException.Message}";
            }

            // Fallback to original message
            return $"Mistral API error: {msg}";
        }

        /// <summary>
        /// Configures the HTTP client with Mistral-specific headers.
        /// </summary>
        /// <param name="client">The HTTP client to configure.</param>
        /// <param name="apiKey">The API key to use for authentication.</param>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            base.ConfigureHttpClient(client, apiKey);

            // Add any Mistral-specific headers here if needed
            // No additional headers required for Mistral, but method is available for future needs
        }
    }
}
