using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Providers.Configuration;

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
        /// <summary>
        /// Initializes a new instance of the CerebrasClient class.
        /// </summary>
        /// <param name="provider">The provider configuration.</param>
        /// <param name="keyCredential">The API key credential.</param>
        /// <param name="providerModelId">The specific model ID to use with this provider.</param>
        /// <param name="logger">Logger for recording diagnostic information.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances with proper configuration.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <exception cref="ConfigurationException">Thrown when API key is missing.</exception>
        public CerebrasClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger<CerebrasClient> logger,
            IHttpClientFactory httpClientFactory)
            : base(
                provider,
                keyCredential,
                providerModelId,
                logger,
                httpClientFactory,
                "cerebras",
                baseUrl: ProviderConfigurationRegistry.ResolveBaseUrl(provider))
        {
        }
    }
}
