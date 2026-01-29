using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Providers.Configuration;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Groq
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
    /// Groq-specific error handling and streaming usage extraction.
    /// </para>
    /// </remarks>
    public partial class GroqClient : ConduitLLM.Providers.OpenAICompatible.OpenAICompatibleClient
    {
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
                    : ProviderConfigurationRegistry.GetDefaultBaseUrl(ProviderType.Groq),
                defaultModels: defaultModels)
        {
        }
    }
}
