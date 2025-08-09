using System.Net.Http;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.Groq
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
    public partial class GroqClient : ConduitLLM.Providers.Providers.OpenAICompatible.OpenAICompatibleClient
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
        /// <param name="provider">The provider entity.</param>
        /// <param name="keyCredential">The key credential to use.</param>
        /// <param name="providerModelId">The model ID to use.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public GroqClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger<GroqClient> logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                provider,
                keyCredential,
                providerModelId,
                logger,
                httpClientFactory,
                "groq",
                baseUrl: !string.IsNullOrWhiteSpace(provider.BaseUrl) 
                    ? provider.BaseUrl 
                    : Constants.Urls.DefaultBaseUrl,
                defaultModels: defaultModels)
        {
        }
    }
}
