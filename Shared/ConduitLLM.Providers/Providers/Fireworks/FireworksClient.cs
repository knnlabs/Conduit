using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Configuration;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Fireworks
{
    /// <summary>
    /// Client for interacting with Fireworks AI's API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fireworks AI provides a fully OpenAI-compatible API with similar endpoints
    /// and request/response formats. This client extends OpenAICompatibleClient to
    /// provide Fireworks-specific configuration and behavior.
    /// </para>
    /// <para>
    /// Fireworks supports multiple model families including Llama, Mixtral, and more.
    /// </para>
    /// </remarks>
    public class FireworksClient : ConduitLLM.Providers.OpenAICompatible.OpenAICompatibleClient
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FireworksClient"/> class.
        /// </summary>
        /// <param name="provider">The provider configuration.</param>
        /// <param name="keyCredential">The API key credential.</param>
        /// <param name="providerModelId">The model identifier to use (e.g., accounts/fireworks/models/llama-v3-8b-instruct).</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="httpClientFactory">Optional HTTP client factory for advanced usage scenarios.</param>
        /// <param name="defaultModels">Optional default model configuration for the provider.</param>
        public FireworksClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string providerModelId,
            ILogger logger,
            IHttpClientFactory? httpClientFactory = null,
            ProviderDefaultModels? defaultModels = null)
            : base(
                provider,
                keyCredential,
                providerModelId,
                logger,
                httpClientFactory,
                "Fireworks",
                baseUrl: ProviderConfigurationRegistry.GetDefaultBaseUrl(ProviderType.Fireworks),
                defaultModels: defaultModels)
        {
        }

        /// <summary>
        /// Creates images using Fireworks API.
        /// </summary>
        /// <param name="request">The image generation request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An image generation response.</returns>
        /// <exception cref="NotSupportedException">Thrown because Fireworks does not currently support image generation.</exception>
        /// <remarks>
        /// Fireworks does not currently support image generation, so this method throws <see cref="NotSupportedException"/>.
        /// </remarks>
        public override Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            Logger.LogWarning("Image generation is not supported by Fireworks");
            return Task.FromException<ImageGenerationResponse>(
                new NotSupportedException("Image generation is not supported by Fireworks"));
        }
    }
}
