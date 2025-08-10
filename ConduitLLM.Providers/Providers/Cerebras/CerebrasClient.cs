using System;
using System.Collections.Generic;
using System.Net.Http;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers.Common.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Cerebras
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
    public partial class CerebrasClient : ConduitLLM.Providers.OpenAICompatible.OpenAICompatibleClient
    {
        // API configuration constants
        private static class Constants
        {
            public static class Urls
            {
                /// <summary>
                /// Default base URL for the Cerebras Inference API
                /// </summary>
                public const string DefaultBaseUrl = "https://api.cerebras.ai/v1";
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
        /// <param name="credentials">LLMProvider credentials containing API key and endpoint configuration.</param>
        /// <param name="providerModelId">The specific model ID to use with this provider.</param>
        /// <param name="logger">Logger for recording diagnostic information.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances with proper configuration.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        /// <param name="providerName">Optional provider name override. If not specified, defaults to "cerebras".</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <exception cref="ConfigurationException">Thrown when API key is missing.</exception>
        public CerebrasClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger<CerebrasClient> logger,
            IHttpClientFactory httpClientFactory,
            ProviderDefaultModels? defaultModels = null,
            string? providerName = null)
            : base(
                provider,
                keyCredential,
                providerModelId,
                logger,
                httpClientFactory,
                providerName ?? "cerebras",
                baseUrl: null,
                defaultModels: defaultModels)
        {
            if (string.IsNullOrWhiteSpace(keyCredential.ApiKey))
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
    }
}
