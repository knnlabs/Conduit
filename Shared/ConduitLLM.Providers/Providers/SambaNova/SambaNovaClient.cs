using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Providers.Common.Models;
using ConduitLLM.Providers.Configuration;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.SambaNova
{
    /// <summary>
    /// Client for interacting with the SambaNova Cloud API for ultra-fast LLM inference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This client implements the ILLMClient interface for interacting with SambaNova's Cloud API,
    /// which provides ultra-fast inference (250+ tokens/second) for language models including DeepSeek, Llama, and Qwen models.
    /// </para>
    /// <para>
    /// Key features:
    /// - Ultra-fast inference with up to 250+ tokens/second streaming
    /// - OpenAI-compatible API format for seamless integration
    /// - Support for streaming and non-streaming responses
    /// - Multiple model families: DeepSeek, Llama, Qwen, and multimodal models
    /// - Multimodal support for text + image inputs
    /// </para>
    /// <para>
    /// Authentication is handled via Bearer token in the Authorization header.
    /// The API key can be obtained from https://cloud.sambanova.ai
    /// </para>
    /// </remarks>
    public partial class SambaNovaClient : ConduitLLM.Providers.OpenAICompatible.OpenAICompatibleClient
    {
        /// <summary>
        /// Fallback models for SambaNova when the models endpoint is not available
        /// </summary>
        private static readonly List<ExtendedModelInfo> SambaNovaModels = new()
        {
            // DeepSeek models
            ExtendedModelInfo.Create("DeepSeek-R1", "sambanova", "DeepSeek R1 (32k context)"),
            ExtendedModelInfo.Create("DeepSeek-V3-0324", "sambanova", "DeepSeek V3 0324 (32k context)"),
            ExtendedModelInfo.Create("DeepSeek-R1-Distill-Llama-70B", "sambanova", "DeepSeek R1 Distill Llama 70B (128k context)"),

            // Meta Llama models
            ExtendedModelInfo.Create("Meta-Llama-3.3-70B-Instruct", "sambanova", "Meta Llama 3.3 70B Instruct (128k context)"),
            ExtendedModelInfo.Create("Meta-Llama-3.1-8B-Instruct", "sambanova", "Meta Llama 3.1 8B Instruct (16k context)"),
            ExtendedModelInfo.Create("Llama-3.3-Swallow-70B-Instruct-v0.4", "sambanova", "Llama 3.3 Swallow 70B Instruct v0.4 (16k context)"),

            // Qwen models
            ExtendedModelInfo.Create("Qwen3-32B", "sambanova", "Qwen3 32B (8k context)"),

            // E5 models
            ExtendedModelInfo.Create("E5-Mistral-7B-Instruct", "sambanova", "E5 Mistral 7B Instruct (4k context)"),

            // Multimodal models
            ExtendedModelInfo.Create("Llama-4-Maverick-17B-128E-Instruct", "sambanova", "Llama 4 Maverick 17B 128E Instruct (128k context, multimodal)")
        };

        /// <summary>
        /// Initializes a new instance of the SambaNovaClient class.
        /// </summary>
        /// <param name="provider">The provider configuration.</param>
        /// <param name="keyCredential">The API key credential.</param>
        /// <param name="providerModelId">The specific model ID to use with this provider.</param>
        /// <param name="logger">Logger for recording diagnostic information.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances with proper configuration.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <exception cref="ConfigurationException">Thrown when API key is missing.</exception>
        public SambaNovaClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger<SambaNovaClient> logger,
            IHttpClientFactory httpClientFactory)
            : base(
                provider,
                keyCredential,
                providerModelId,
                logger,
                httpClientFactory,
                "sambanova",
                baseUrl: ProviderConfigurationRegistry.ResolveBaseUrl(provider))
        {
        }
    }
}
