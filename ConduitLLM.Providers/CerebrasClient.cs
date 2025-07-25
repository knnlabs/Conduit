using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.InternalModels;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Client for interacting with the Cerebras Inference API for high-performance LLM inference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This client implements the ILLMClient interface for interacting with Cerebras's inference API,
    /// which provides accelerated inference for language models including Llama, Qwen, and other models.
    /// </para>
    /// <para>
    /// Key features:
    /// - High-performance inference with Cerebras hardware acceleration
    /// - OpenAI-compatible API format for seamless integration
    /// - Support for streaming and non-streaming responses
    /// - Multiple model families: Llama 3.1, Llama 4 Scout, Qwen 3, and DeepSeek
    /// </para>
    /// <para>
    /// Authentication is handled via Bearer token in the Authorization header.
    /// The API key can be obtained from https://cloud.cerebras.ai
    /// </para>
    /// </remarks>
    public class CerebrasClient : OpenAICompatibleClient
    {
        // API configuration constants
        private static class Constants
        {
            public static class Urls
            {
                /// <summary>
                /// Default base URL for the Cerebras Inference API
                /// </summary>
                public const string DefaultApiBase = "https://api.cerebras.ai/v1";
            }

            public static class Headers
            {
                /// <summary>
                /// Authorization header for API key authentication
                /// </summary>
                public const string Authorization = "Authorization";
            }

            public static class Endpoints
            {
                public const string ChatCompletions = "/chat/completions";
                public const string Models = "/models";
            }

            public static class ErrorMessages
            {
                public const string MissingApiKey = "API key is missing for provider 'cerebras'";
                public const string RateLimitExceeded = "Cerebras API rate limit exceeded. Please try again later or reduce your request frequency.";
                public const string InvalidApiKey = "Invalid Cerebras API key. Please check your credentials.";
                public const string ModelNotFound = "The specified model is not available. Please check the model name and try again.";
                public const string QuotaExceeded = "API quota exceeded. Please check your usage limits or upgrade your plan.";
            }
        }

        /// <summary>
        /// Fallback models for Cerebras when the models endpoint is not available
        /// </summary>
        private static readonly List<ExtendedModelInfo> CerebrasModels = new()
        {
            // Llama 3.1 models
            ExtendedModelInfo.Create("llama3.1-8b", "cerebras", "Llama 3.1 8B"),
            ExtendedModelInfo.Create("llama3.1-70b", "cerebras", "Llama 3.1 70B"),
            
            // Llama 3.3 models
            ExtendedModelInfo.Create("llama-3.3-70b", "cerebras", "Llama 3.3 70B"),
            
            // Llama 4 Scout models
            ExtendedModelInfo.Create("llama-4-scout-17b-16e-instruct", "cerebras", "Llama 4 Scout 17B Instruct"),
            
            // Qwen 3 models
            ExtendedModelInfo.Create("qwen-3-32b", "cerebras", "Qwen 3 32B"),
            ExtendedModelInfo.Create("qwen-3-235b-a22b", "cerebras", "Qwen 3 235B"),
            
            // DeepSeek models (private preview)
            ExtendedModelInfo.Create("deepseek-r1-distill-llama-70b", "cerebras", "DeepSeek R1 Distill Llama 70B")
        };

        /// <summary>
        /// Initializes a new instance of the CerebrasClient class.
        /// </summary>
        /// <param name="credentials">Provider credentials containing API key and endpoint configuration.</param>
        /// <param name="providerModelId">The specific model ID to use with this provider.</param>
        /// <param name="logger">Logger for recording diagnostic information.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances with proper configuration.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        /// <param name="providerName">Optional provider name override. If not specified, defaults to "cerebras".</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <exception cref="ConfigurationException">Thrown when API key is missing.</exception>
        public CerebrasClient(
            ProviderCredentials credentials,
            string providerModelId,
            ILogger<CerebrasClient> logger,
            IHttpClientFactory httpClientFactory,
            ProviderDefaultModels? defaultModels = null,
            string? providerName = null)
            : base(
                credentials,
                providerModelId,
                logger,
                httpClientFactory,
                providerName ?? "cerebras",
                Constants.Urls.DefaultApiBase,
                defaultModels)
        {
            if (string.IsNullOrWhiteSpace(credentials.ApiKey))
            {
                throw new ConfigurationException(Constants.ErrorMessages.MissingApiKey);
            }
        }

        /// <summary>
        /// Configures the HTTP client for Cerebras API requests.
        /// </summary>
        /// <param name="client">The HTTP client to configure.</param>
        /// <param name="apiKey">The API key to configure authentication with.</param>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            base.ConfigureHttpClient(client, apiKey);

            // Set User-Agent for better API analytics
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ConduitLLM-CerebrasClient/1.0");
        }

        /// <summary>
        /// Gets the fallback models for Cerebras when the models endpoint is not available.
        /// </summary>
        /// <returns>A list of Cerebras models.</returns>
        protected override List<ExtendedModelInfo> GetFallbackModels()
        {
            return new List<ExtendedModelInfo>(CerebrasModels);
        }

        /// <summary>
        /// Processes HTTP errors and converts them to appropriate exceptions.
        /// </summary>
        /// <param name="statusCode">The HTTP status code.</param>
        /// <param name="responseContent">The response content.</param>
        /// <param name="requestId">Optional request ID for tracking.</param>
        /// <returns>An appropriate exception for the error.</returns>
        private Exception ProcessHttpError(System.Net.HttpStatusCode statusCode, string responseContent, string? requestId = null)
        {
            Logger.LogError("Cerebras API error - Status: {StatusCode}, Content: {Content}, RequestId: {RequestId}",
                statusCode, responseContent, requestId);

            return statusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => new ConfigurationException(Constants.ErrorMessages.InvalidApiKey),
                System.Net.HttpStatusCode.TooManyRequests => new LLMCommunicationException(Constants.ErrorMessages.RateLimitExceeded),
                System.Net.HttpStatusCode.NotFound => new ModelUnavailableException(Constants.ErrorMessages.ModelNotFound),
                System.Net.HttpStatusCode.PaymentRequired => new LLMCommunicationException(Constants.ErrorMessages.QuotaExceeded),
                System.Net.HttpStatusCode.BadRequest => ParseBadRequestError(responseContent),
                System.Net.HttpStatusCode.InternalServerError => new LLMCommunicationException($"Cerebras API internal error: {responseContent}"),
                System.Net.HttpStatusCode.ServiceUnavailable => new LLMCommunicationException("Cerebras API is temporarily unavailable. Please try again later."),
                _ => new LLMCommunicationException($"Cerebras API error ({statusCode}): {responseContent}")
            };
        }

        /// <summary>
        /// Parses bad request errors to provide more specific error information.
        /// </summary>
        /// <param name="responseContent">The response content containing error details.</param>
        /// <returns>An appropriate exception for the bad request error.</returns>
        private Exception ParseBadRequestError(string responseContent)
        {
            try
            {
                using var document = JsonDocument.Parse(responseContent);
                if (document.RootElement.TryGetProperty("error", out var errorElement))
                {
                    if (errorElement.TryGetProperty("message", out var messageElement))
                    {
                        var errorMessage = messageElement.GetString();
                        
                        // Check for specific error patterns
                        if (errorMessage?.Contains("model", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            return new ModelUnavailableException($"Model error: {errorMessage}");
                        }
                        
                        if (errorMessage?.Contains("token", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            return new ValidationException($"Token limit error: {errorMessage}");
                        }
                        
                        return new ValidationException($"Request error: {errorMessage}");
                    }
                }
            }
            catch (JsonException)
            {
                // Fall through to generic error if JSON parsing fails
            }

            return new ValidationException($"Bad request: {responseContent}");
        }

        /// <summary>
        /// Validates the model ID for Cerebras-specific requirements.
        /// </summary>
        /// <param name="modelId">The model ID to validate.</param>
        /// <returns>True if the model ID is valid, false otherwise.</returns>
        private bool IsValidModelId(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
                return false;

            // Cerebras model IDs follow specific patterns
            var validPrefixes = new[]
            {
                "llama3.1-",
                "llama-3.3-",
                "llama-4-scout-",
                "qwen-3-",
                "deepseek-r1-"
            };

            foreach (var prefix in validPrefixes)
            {
                if (modelId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

    }
}