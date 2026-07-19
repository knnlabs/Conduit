using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Providers.Common.Models;
using ConduitLLM.Providers.Configuration;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Cloudflare
{
    /// <summary>
    /// Client for interacting with Cloudflare Workers AI via its OpenAI-compatible API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cloudflare Workers AI provides serverless AI inference on Cloudflare's global GPU network.
    /// This client uses the OpenAI-compatible endpoints at /ai/v1/ for chat completions and embeddings.
    /// </para>
    /// <para>
    /// Key features:
    /// - OpenAI-compatible API for chat completions and embeddings
    /// - Support for streaming and non-streaming responses
    /// - Function calling via tools array
    /// - Access to Llama, Qwen, Gemma, and other open-source models
    /// </para>
    /// <para>
    /// Configuration requires a Cloudflare API token and the base URL must include the account ID:
    /// https://api.cloudflare.com/client/v4/accounts/{ACCOUNT_ID}/ai/v1
    /// </para>
    /// <para>
    /// Authentication is handled via Bearer token in the Authorization header.
    /// API tokens can be created at https://dash.cloudflare.com/profile/api-tokens
    /// </para>
    /// </remarks>
    public partial class CloudflareClient : ConduitLLM.Providers.OpenAICompatible.OpenAICompatibleClient
    {
        /// <summary>
        /// Gets the Cloudflare-specific error messages from the configuration registry.
        /// </summary>
        private static ProviderErrorMessages CloudflareErrorMessages =>
            ProviderConfigurationRegistry.GetErrorMessages(ProviderType.Cloudflare);

        /// <summary>
        /// Fallback models for Cloudflare Workers AI when the models endpoint is not available.
        /// </summary>
        private static readonly List<ExtendedModelInfo> CloudflareModels = new()
        {
            // Llama models
            ExtendedModelInfo.Create("@cf/meta/llama-3.3-70b-instruct-fp8-fast", "cloudflare", "Llama 3.3 70B Instruct"),
            ExtendedModelInfo.Create("@cf/meta/llama-3.1-8b-instruct", "cloudflare", "Llama 3.1 8B Instruct"),
            ExtendedModelInfo.Create("@cf/meta/llama-3.1-70b-instruct", "cloudflare", "Llama 3.1 70B Instruct"),

            // Qwen models
            ExtendedModelInfo.Create("@cf/qwen/qwen3-30b-a3b", "cloudflare", "Qwen 3 30B"),
            ExtendedModelInfo.Create("@cf/qwen/qwen3-8b", "cloudflare", "Qwen 3 8B"),

            // Gemma models
            ExtendedModelInfo.Create("@cf/google/gemma-3-12b-it", "cloudflare", "Gemma 3 12B IT"),

            // Embedding models
            ExtendedModelInfo.Create("@cf/baai/bge-m3", "cloudflare", "BGE-M3 Embedding"),

            // Image generation models
            ExtendedModelInfo.Create("@cf/black-forest-labs/flux-1-schnell", "cloudflare", "Flux 1 Schnell"),
            ExtendedModelInfo.Create("@cf/stabilityai/stable-diffusion-xl-base-1.0", "cloudflare", "Stable Diffusion XL Base"),
            ExtendedModelInfo.Create("@cf/bytedance/stable-diffusion-xl-lightning", "cloudflare", "SDXL Lightning"),
            ExtendedModelInfo.Create("@cf/lykon/dreamshaper-8-lcm", "cloudflare", "DreamShaper 8 LCM"),
            ExtendedModelInfo.Create("@cf/runwayml/stable-diffusion-v1-5-img2img", "cloudflare", "SD 1.5 Img2Img"),
            ExtendedModelInfo.Create("@cf/runwayml/stable-diffusion-v1-5-inpainting", "cloudflare", "SD 1.5 Inpainting"),
            ExtendedModelInfo.Create("@cf/leonardo/phoenix-1.0", "cloudflare", "Leonardo Phoenix 1.0"),
            ExtendedModelInfo.Create("@cf/leonardo/lucid-origin", "cloudflare", "Leonardo Lucid Origin"),
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="CloudflareClient"/> class.
        /// </summary>
        /// <param name="provider">The provider configuration.</param>
        /// <param name="keyCredential">The API key credential (Cloudflare API token).</param>
        /// <param name="providerModelId">The model identifier (e.g., @cf/meta/llama-3.3-70b-instruct-fp8-fast).</param>
        /// <param name="logger">Logger for recording diagnostic information.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        /// <exception cref="ConfigurationException">Thrown when API key is missing.</exception>
        public CloudflareClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger<CloudflareClient> logger,
            IHttpClientFactory httpClientFactory,
            ProviderDefaultModels? defaultModels = null)
            : base(
                provider,
                keyCredential,
                providerModelId,
                logger,
                httpClientFactory,
                "Cloudflare",
                baseUrl: ProviderConfigurationRegistry.GetDefaultBaseUrl(ProviderType.Cloudflare),
                defaultModels: defaultModels)
        {
            if (string.IsNullOrWhiteSpace(keyCredential.ApiKey))
            {
                throw new ConfigurationException(CloudflareErrorMessages.MissingApiKey);
            }
        }

        /// <summary>
        /// Configures the HTTP client for Cloudflare Workers AI requests.
        /// </summary>
        /// <param name="client">The HTTP client to configure.</param>
        /// <param name="apiKey">The API key to configure authentication with.</param>
        protected override void ConfigureHttpClient(HttpClient client, string apiKey)
        {
            base.ConfigureHttpClient(client, apiKey);

            // Set User-Agent for better API analytics
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ConduitLLM-CloudflareClient/1.0");
        }
    }
}
