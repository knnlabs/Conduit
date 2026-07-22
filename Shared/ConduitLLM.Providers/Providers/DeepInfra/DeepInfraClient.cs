using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Providers.Configuration;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.DeepInfra
{
    /// <summary>
    /// Client for interacting with DeepInfra's OpenAI-compatible API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// DeepInfra provides a fully OpenAI-compatible API with access to cutting-edge models
    /// including advanced reasoning and coding specialists. This client extends OpenAICompatibleClient
    /// to provide DeepInfra-specific configuration and behavior.
    /// </para>
    /// <para>
    /// DeepInfra supports multiple model families including Qwen, MoonshotAI, and GLM models
    /// with extensive context windows (up to 262,144 tokens) and multimodal capabilities.
    /// </para>
    /// <para>
    /// Key features:
    /// - Full OpenAI API compatibility for seamless integration
    /// - Support for streaming and non-streaming responses
    /// - Multimodal support (text + image inputs)
    /// - Advanced reasoning and coding models
    /// - Model versioning support (MODEL_NAME:VERSION format)
    /// </para>
    /// </remarks>
    public class DeepInfraClient : ConduitLLM.Providers.OpenAICompatible.OpenAICompatibleClient
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeepInfraClient"/> class.
        /// </summary>
        /// <param name="provider">The provider configuration.</param>
        /// <param name="keyCredential">The API key credential.</param>
        /// <param name="providerModelId">The model identifier to use (e.g., Qwen/Qwen3-235B-A22B-Thinking-2507).</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory for advanced usage scenarios.</param>
        public DeepInfraClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger logger,
            IHttpClientFactory? httpClientFactory = null)
            : base(
                provider,
                keyCredential,
                providerModelId,
                logger,
                httpClientFactory,
                "DeepInfra",
                baseUrl: ProviderConfigurationRegistry.ResolveBaseUrl(provider))
        {
        }
    }
}
