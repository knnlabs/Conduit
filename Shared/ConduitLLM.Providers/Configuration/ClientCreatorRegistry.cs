using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Providers.Cerebras;
using ConduitLLM.Providers.DeepInfra;
using ConduitLLM.Providers.Fireworks;
using ConduitLLM.Providers.Groq;
using ConduitLLM.Providers.MiniMax;
using ConduitLLM.Providers.OpenAI;
using ConduitLLM.Providers.Replicate;
using ConduitLLM.Providers.Cloudflare;
using ConduitLLM.Providers.Meta;
using ConduitLLM.Providers.OpenRouter;
using ConduitLLM.Providers.SambaNova;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Configuration
{
    /// <summary>
    /// Delegate for creating LLM client instances.
    /// </summary>
    /// <param name="provider">The provider configuration.</param>
    /// <param name="keyCredential">The API key credential.</param>
    /// <param name="modelId">The model ID to use.</param>
    /// <param name="context">The creation context with dependencies.</param>
    /// <returns>The created LLM client instance.</returns>
    public delegate ILLMClient ClientCreatorDelegate(
        Provider provider,
        ProviderKeyCredential keyCredential,
        string modelId,
        ClientCreationContext context);

    /// <summary>
    /// Context containing dependencies needed for client creation.
    /// </summary>
    public record ClientCreationContext
    {
        /// <summary>
        /// The logger factory for creating typed loggers.
        /// </summary>
        public required ILoggerFactory LoggerFactory { get; init; }

        /// <summary>
        /// The HTTP client factory for creating HTTP clients.
        /// </summary>
        public required IHttpClientFactory HttpClientFactory { get; init; }

        /// <summary>
        /// Optional model capability service for capability detection.
        /// </summary>
        public IModelCapabilityService? CapabilityService { get; init; }

        /// <summary>
        /// Optional default models configuration.
        /// </summary>
        public ProviderDefaultModels? DefaultModels { get; init; }
    }

    /// <summary>
    /// Registry for creating LLM clients based on provider type.
    /// Eliminates the need for switch statements in client factories.
    /// </summary>
    public static class ClientCreatorRegistry
    {
        /// <summary>
        /// Registry of client creators keyed by ProviderType.
        /// </summary>
        private static readonly Dictionary<ProviderType, ClientCreatorDelegate> Creators = new()
        {
            [ProviderType.OpenAI] = CreateOpenAIClient,
            [ProviderType.Groq] = CreateGroqClient,
            [ProviderType.Replicate] = CreateReplicateClient,
            [ProviderType.Fireworks] = CreateFireworksClient,
            [ProviderType.OpenAICompatible] = CreateOpenAICompatibleClient,
            [ProviderType.MiniMax] = CreateMiniMaxClient,
            [ProviderType.Cerebras] = CreateCerebrasClient,
            [ProviderType.SambaNova] = CreateSambaNovaClient,
            [ProviderType.DeepInfra] = CreateDeepInfraClient,
            [ProviderType.Cloudflare] = CreateCloudflareClient,
            [ProviderType.OpenRouter] = CreateOpenRouterClient,
            [ProviderType.Meta] = CreateMetaClient
        };

        /// <summary>
        /// Gets the client creator for a provider type.
        /// </summary>
        /// <param name="providerType">The provider type.</param>
        /// <returns>The client creator delegate, or null if not found.</returns>
        public static ClientCreatorDelegate? GetCreator(ProviderType providerType)
        {
            return Creators.TryGetValue(providerType, out var creator) ? creator : null;
        }

        /// <summary>
        /// Tries to get the client creator for a provider type.
        /// </summary>
        /// <param name="providerType">The provider type.</param>
        /// <param name="creator">The creator if found.</param>
        /// <returns>True if found, false otherwise.</returns>
        public static bool TryGetCreator(ProviderType providerType, out ClientCreatorDelegate? creator)
        {
            return Creators.TryGetValue(providerType, out creator);
        }

        /// <summary>
        /// Creates a client for the specified provider type.
        /// </summary>
        /// <param name="providerType">The provider type.</param>
        /// <param name="provider">The provider configuration.</param>
        /// <param name="keyCredential">The API key credential.</param>
        /// <param name="modelId">The model ID to use.</param>
        /// <param name="context">The creation context with dependencies.</param>
        /// <returns>The created LLM client instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the provider type is not supported.</exception>
        public static ILLMClient CreateClient(
            ProviderType providerType,
            Provider provider,
            ProviderKeyCredential keyCredential,
            string modelId,
            ClientCreationContext context)
        {
            if (!TryGetCreator(providerType, out var creator) || creator == null)
            {
                throw new ArgumentException($"Unsupported provider type: {providerType}", nameof(providerType));
            }

            return creator(provider, keyCredential, modelId, context);
        }

        /// <summary>
        /// Checks if a provider type is supported.
        /// </summary>
        /// <param name="providerType">The provider type to check.</param>
        /// <returns>True if supported, false otherwise.</returns>
        public static bool IsSupported(ProviderType providerType)
        {
            return Creators.ContainsKey(providerType);
        }

        /// <summary>
        /// Gets all supported provider types.
        /// </summary>
        /// <returns>Collection of supported provider types.</returns>
        public static IEnumerable<ProviderType> GetSupportedProviderTypes()
        {
            return Creators.Keys;
        }

        private static ILLMClient CreateOpenAIClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string modelId,
            ClientCreationContext context)
        {
            var logger = context.LoggerFactory.CreateLogger<OpenAIClient>();
            return new OpenAIClient(
                provider,
                keyCredential,
                modelId,
                logger,
                context.HttpClientFactory,
                context.CapabilityService,
                context.DefaultModels);
        }

        private static ILLMClient CreateGroqClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string modelId,
            ClientCreationContext context)
        {
            var logger = context.LoggerFactory.CreateLogger<GroqClient>();
            return new GroqClient(
                provider,
                keyCredential,
                modelId,
                logger,
                context.HttpClientFactory,
                context.DefaultModels);
        }

        private static ILLMClient CreateReplicateClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string modelId,
            ClientCreationContext context)
        {
            var logger = context.LoggerFactory.CreateLogger<ReplicateClient>();
            return new ReplicateClient(
                provider,
                keyCredential,
                modelId,
                logger,
                context.HttpClientFactory,
                context.DefaultModels);
        }

        private static ILLMClient CreateFireworksClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string modelId,
            ClientCreationContext context)
        {
            var logger = context.LoggerFactory.CreateLogger<FireworksClient>();
            return new FireworksClient(
                provider,
                keyCredential,
                modelId,
                logger,
                context.HttpClientFactory,
                context.DefaultModels);
        }

        private static ILLMClient CreateOpenAICompatibleClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string modelId,
            ClientCreationContext context)
        {
            var logger = context.LoggerFactory.CreateLogger<OpenAICompatibleGenericClient>();
            return new OpenAICompatibleGenericClient(
                provider,
                keyCredential,
                modelId,
                logger,
                context.HttpClientFactory,
                context.DefaultModels);
        }

        private static ILLMClient CreateMiniMaxClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string modelId,
            ClientCreationContext context)
        {
            var logger = context.LoggerFactory.CreateLogger<MiniMaxClient>();
            return new MiniMaxClient(
                provider,
                keyCredential,
                modelId,
                logger,
                context.HttpClientFactory,
                context.DefaultModels);
        }

        private static ILLMClient CreateCerebrasClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string modelId,
            ClientCreationContext context)
        {
            var logger = context.LoggerFactory.CreateLogger<CerebrasClient>();
            return new CerebrasClient(
                provider,
                keyCredential,
                modelId,
                logger,
                context.HttpClientFactory,
                context.DefaultModels);
        }

        private static ILLMClient CreateSambaNovaClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string modelId,
            ClientCreationContext context)
        {
            var logger = context.LoggerFactory.CreateLogger<SambaNovaClient>();
            return new SambaNovaClient(
                provider,
                keyCredential,
                modelId,
                logger,
                context.HttpClientFactory,
                context.DefaultModels);
        }

        private static ILLMClient CreateDeepInfraClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string modelId,
            ClientCreationContext context)
        {
            var logger = context.LoggerFactory.CreateLogger<DeepInfraClient>();
            return new DeepInfraClient(
                provider,
                keyCredential,
                modelId,
                logger,
                context.HttpClientFactory,
                context.DefaultModels);
        }

        private static ILLMClient CreateCloudflareClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string modelId,
            ClientCreationContext context)
        {
            var logger = context.LoggerFactory.CreateLogger<CloudflareClient>();
            return new CloudflareClient(
                provider,
                keyCredential,
                modelId,
                logger,
                context.HttpClientFactory,
                context.DefaultModels);
        }

        private static ILLMClient CreateOpenRouterClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string modelId,
            ClientCreationContext context)
        {
            var logger = context.LoggerFactory.CreateLogger<OpenRouterClient>();
            return new OpenRouterClient(
                provider,
                keyCredential,
                modelId,
                logger,
                context.HttpClientFactory,
                context.DefaultModels);
        }

        private static ILLMClient CreateMetaClient(
            Provider provider,
            ProviderKeyCredential keyCredential,
            string modelId,
            ClientCreationContext context)
        {
            var logger = context.LoggerFactory.CreateLogger<MetaClient>();
            return new MetaClient(
                provider,
                keyCredential,
                modelId,
                logger,
                context.HttpClientFactory,
                context.DefaultModels);
        }
    }
}
