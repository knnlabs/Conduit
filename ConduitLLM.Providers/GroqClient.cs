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
    /// Client for interacting with Groq's LLM API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Groq uses the OpenAI-compatible API format but with much faster inference speeds.
    /// It provides optimized inference for popular open-source models like Llama, Mixtral, and Gemma.
    /// </para>
    /// <para>
    /// This client leverages the OpenAI-compatible base implementation and adds 
    /// Groq-specific error handling and fallback mechanisms.
    /// </para>
    /// </remarks>
    public class GroqClient : OpenAICompatibleClient
    {
        // API configuration constants
        private static class Constants
        {
            public static class Urls
            {
                public const string DefaultBaseUrl = "https://api.groq.com/openai/v1";
            }

            public static class Endpoints
            {
                public const string ChatCompletions = "/chat/completions";
                public const string Models = "/models";
                public const string Completions = "/completions";
            }

            public static class ErrorMessages
            {
                public const string ModelNotFound = "Model not found. Available Groq models include: llama3-8b-8192, llama3-70b-8192, llama2-70b-4096, mixtral-8x7b-32768, gemma-7b-it";
                public const string RateLimitExceeded = "Groq API rate limit exceeded. Please try again later or reduce your request frequency.";
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GroqClient"/> class.
        /// </summary>
        /// <param name="credentials">The credentials for the Groq API.</param>
        /// <param name="providerModelId">The model ID to use.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public GroqClient(
            ProviderCredentials credentials,
            string providerModelId,
            ILogger<GroqClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                EnsureGroqCredentials(credentials),
                providerModelId,
                logger,
                httpClientFactory,
                "groq",
                DetermineBaseUrl(credentials),
                defaultModels)
        {
        }

        /// <summary>
        /// Determines the base URL to use for the Groq API.
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
        /// Ensures the credentials are valid for Groq and sets defaults if needed.
        /// </summary>
        /// <param name="credentials">The provider credentials to validate and normalize.</param>
        /// <returns>A normalized copy of the credentials with Groq-specific defaults.</returns>
        /// <exception cref="ArgumentNullException">Thrown when credentials are null.</exception>
        /// <exception cref="ConfigurationException">Thrown when API key is missing.</exception>
        private static ProviderCredentials EnsureGroqCredentials(ProviderCredentials credentials)
        {
            if (credentials == null)
            {
                throw new ArgumentNullException(nameof(credentials));
            }

            if (string.IsNullOrWhiteSpace(credentials.ApiKey))
            {
                throw new ConfigurationException("API key is missing for Groq provider.");
            }

            // Create a copy of the credentials to avoid modifying the original
            var groqCredentials = new ProviderCredentials
            {
                ApiKey = credentials.ApiKey,
                BaseUrl = string.IsNullOrWhiteSpace(credentials.BaseUrl) ? Constants.Urls.DefaultBaseUrl : credentials.BaseUrl,
                ProviderType = ProviderType.Groq
            };

            return groqCredentials;
        }

        /// <summary>
        /// Creates a chat completion with enhanced error handling specific to Groq.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A chat completion response from Groq.</returns>
        /// <exception cref="LLMCommunicationException">Thrown when there is a communication error with Groq.</exception>
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
                // Enhance error message handling for Groq and re-throw
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

                Logger.LogError(ex, "Groq API error: {Message}", errorMessage);
                throw new LLMCommunicationException($"Groq API error: {errorMessage}", ex);
            }
        }

        /// <summary>
        /// Streams a chat completion with enhanced error handling specific to Groq.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable of chat completion chunks.</returns>
        /// <exception cref="LLMCommunicationException">Thrown when there is a communication error with Groq.</exception>
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
                // Enhance error message handling for Groq
                var enhancedErrorMessage = ExtractEnhancedErrorMessage(ex);
                Logger.LogError(ex, "Error initializing streaming chat completion from Groq: {Message}", enhancedErrorMessage);
                throw new LLMCommunicationException(enhancedErrorMessage, ex);
            }

            // Process the stream outside of try/catch
            await foreach (var chunk in baseStream.WithCancellation(cancellationToken))
            {
                yield return chunk;
            }
        }

        /// <summary>
        /// Gets available models from the Groq API or falls back to a predefined list.
        /// </summary>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A list of available models from Groq.</returns>
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
                Logger.LogWarning(ex, "Failed to retrieve models from Groq API. Returning known models.");
                return GetFallbackModels();
            }
        }

        /// <summary>
        /// Gets a fallback list of models for Groq when the API is unavailable.
        /// </summary>
        /// <returns>A list of known Groq models.</returns>
        protected override List<InternalModels.ExtendedModelInfo> GetFallbackModels()
        {
            // Use the default fallback models from the base class
            if (ProviderFallbackModels.TryGetValue("groq", out var models))
            {
                return models;
            }

            // If for some reason the base class doesn't have Groq models, provide them here
            return new List<InternalModels.ExtendedModelInfo>
            {
                InternalModels.ExtendedModelInfo.Create("llama3-8b-8192", "groq", "llama3-8b-8192"),
                InternalModels.ExtendedModelInfo.Create("llama3-70b-8192", "groq", "llama3-70b-8192"),
                InternalModels.ExtendedModelInfo.Create("llama2-70b-4096", "groq", "llama2-70b-4096"),
                InternalModels.ExtendedModelInfo.Create("mixtral-8x7b-32768", "groq", "mixtral-8x7b-32768"),
                InternalModels.ExtendedModelInfo.Create("gemma-7b-it", "groq", "gemma-7b-it")
            };
        }

        /// <summary>
        /// Extracts a more helpful error message from exception details for Groq errors.
        /// </summary>
        /// <param name="ex">The exception to extract information from.</param>
        /// <returns>An enhanced error message specific to Groq errors.</returns>
        /// <remarks>
        /// This overrides the base implementation to provide more specific error extraction for Groq.
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

            // Groq-specific error extraction
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

            // Look for Body data
            if (ex.Data.Contains("Body") && ex.Data["Body"] is string body && !string.IsNullOrEmpty(body))
            {
                return $"Groq API error: {body}";
            }

            // Try inner exception
            if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
            {
                return $"Groq API error: {ex.InnerException.Message}";
            }

            // Fallback to original message
            return $"Groq API error: {msg}";
        }

        #region Authentication Verification

        /// <summary>
        /// Verifies Groq authentication by making a test request to the models endpoint.
        /// </summary>
        public override async Task<Core.Interfaces.AuthenticationResult> VerifyAuthenticationAsync(
            string? apiKey = null,
            string? baseUrl = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : Credentials.ApiKey;
                
                if (string.IsNullOrWhiteSpace(effectiveApiKey))
                {
                    return Core.Interfaces.AuthenticationResult.Failure(
                        "API key is required",
                        "No API key provided for Groq authentication");
                }

                // Create a test client
                using var client = CreateHttpClient(effectiveApiKey);
                
                // Make a request to the models endpoint
                var modelsUrl = $"{GetHealthCheckUrl(baseUrl)}/models";
                var response = await client.GetAsync(modelsUrl, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                Logger.LogInformation("Groq auth check returned status {StatusCode}", response.StatusCode);

                // Check for authentication errors
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return Core.Interfaces.AuthenticationResult.Failure(
                        "Authentication failed",
                        "Invalid API key - Groq requires a valid API key");
                }
                
                if (response.IsSuccessStatusCode)
                {
                    return Core.Interfaces.AuthenticationResult.Success(
                        "Connected successfully to Groq API",
                        responseTime);
                }

                // Other errors
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Unexpected response: {response.StatusCode}",
                    await response.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error verifying Groq authentication");
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <summary>
        /// Gets the health check URL for Groq.
        /// </summary>
        public override string GetHealthCheckUrl(string? baseUrl = null)
        {
            var effectiveBaseUrl = !string.IsNullOrWhiteSpace(baseUrl) 
                ? baseUrl.TrimEnd('/') 
                : (!string.IsNullOrWhiteSpace(Credentials.BaseUrl) 
                    ? Credentials.BaseUrl.TrimEnd('/') 
                    : Constants.Urls.DefaultBaseUrl.TrimEnd('/'));
            
            return effectiveBaseUrl;
        }

        #endregion
    }
}
