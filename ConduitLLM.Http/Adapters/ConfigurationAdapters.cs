using ConduitLLM.Core.Interfaces.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Http.Adapters
{
    /// <summary>
    /// Provides adapters that map Configuration services to Core interfaces for the HTTP API.
    /// </summary>
    internal static class ConfigurationAdapters
    {
        /// <summary>
        /// Registers Core configuration service interfaces with their Configuration implementations.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddConfigurationAdapters(this IServiceCollection services)
        {
            // Register adapters that map Configuration services to Core interfaces
            services.AddScoped<IModelProviderMappingService>(provider =>
                new ModelProviderMappingServiceAdapter(provider.GetRequiredService<ConduitLLM.Configuration.IModelProviderMappingService>()));

            services.AddScoped<IProviderCredentialService>(provider =>
                new ProviderCredentialServiceAdapter(provider.GetRequiredService<ConduitLLM.Configuration.IProviderCredentialService>()));

            services.AddScoped<IModelCostService>(provider =>
                new ModelCostServiceAdapter(provider.GetRequiredService<ConduitLLM.Configuration.Services.IModelCostService>()));

            services.AddSingleton<ICacheService>(provider =>
                new CacheServiceAdapter(provider.GetRequiredService<ConduitLLM.Configuration.Services.ICacheService>()));

            return services;
        }

        /// <summary>
        /// Adapter that wraps Configuration's IModelProviderMappingService to implement Core's interface.
        /// </summary>
        private class ModelProviderMappingServiceAdapter : IModelProviderMappingService
        {
            private readonly ConduitLLM.Configuration.IModelProviderMappingService _innerService;

            public ModelProviderMappingServiceAdapter(ConduitLLM.Configuration.IModelProviderMappingService innerService)
            {
                _innerService = innerService;
            }

            public async Task<List<ModelProviderMapping>> GetAllMappingsAsync()
            {
                var mappings = await _innerService.GetAllMappingsAsync();
                return mappings.Select(m => new ModelProviderMapping
                {
                    ModelAlias = m.ModelAlias,
                    ProviderName = m.ProviderName,
                    ProviderModelId = m.ProviderModelId,
                    DeploymentName = m.DeploymentName,
                    IsEnabled = true, // Default
                    MaxContextTokens = null, // Default
                    SupportsImageGeneration = m.SupportsImageGeneration,
                    SupportsEmbeddings = m.SupportsEmbeddings
                }).ToList();
            }

            public async Task<ModelProviderMapping?> GetMappingByModelAliasAsync(string modelAlias)
            {
                var mapping = await _innerService.GetMappingByModelAliasAsync(modelAlias);
                if (mapping == null) return null;

                return new ModelProviderMapping
                {
                    ModelAlias = mapping.ModelAlias,
                    ProviderName = mapping.ProviderName,
                    ProviderModelId = mapping.ProviderModelId,
                    DeploymentName = mapping.DeploymentName,
                    IsEnabled = true, // Default
                    MaxContextTokens = null, // Default
                    SupportsImageGeneration = mapping.SupportsImageGeneration,
                    SupportsEmbeddings = mapping.SupportsEmbeddings
                };
            }
        }

        /// <summary>
        /// Adapter that wraps Configuration's IProviderCredentialService to implement Core's interface.
        /// </summary>
        private class ProviderCredentialServiceAdapter : IProviderCredentialService
        {
            private readonly ConduitLLM.Configuration.IProviderCredentialService _innerService;

            public ProviderCredentialServiceAdapter(ConduitLLM.Configuration.IProviderCredentialService innerService)
            {
                _innerService = innerService;
            }

            public async Task<ProviderCredentials?> GetCredentialByProviderNameAsync(string providerName)
            {
                var credential = await _innerService.GetCredentialByProviderNameAsync(providerName);
                if (credential == null) return null;

                return new ProviderCredentials
                {
                    ProviderName = credential.ProviderName,
                    ApiKey = credential.ApiKey,
                    BaseUrl = credential.BaseUrl,
                    ApiVersion = credential.ApiVersion,
                    IsEnabled = credential.IsEnabled
                };
            }
        }

        /// <summary>
        /// Adapter that wraps Configuration's IModelCostService to implement Core's interface.
        /// </summary>
        private class ModelCostServiceAdapter : IModelCostService
        {
            private readonly ConduitLLM.Configuration.Services.IModelCostService _innerService;

            public ModelCostServiceAdapter(ConduitLLM.Configuration.Services.IModelCostService innerService)
            {
                _innerService = innerService;
            }

            public async Task<ModelCostInfo?> GetCostForModelAsync(string modelId, CancellationToken cancellationToken = default)
            {
                var modelCost = await _innerService.GetCostForModelAsync(modelId, cancellationToken);
                if (modelCost == null) return null;

                // Deserialize video resolution multipliers if present
                Dictionary<string, decimal>? videoResolutionMultipliers = null;
                if (!string.IsNullOrEmpty(modelCost.VideoResolutionMultipliers))
                {
                    try
                    {
                        videoResolutionMultipliers = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(modelCost.VideoResolutionMultipliers);
                    }
                    catch
                    {
                        // If deserialization fails, leave as null
                    }
                }

                return new ModelCostInfo
                {
                    ModelIdPattern = modelCost.ModelIdPattern,
                    InputTokenCost = modelCost.InputTokenCost,
                    OutputTokenCost = modelCost.OutputTokenCost,
                    EmbeddingTokenCost = modelCost.EmbeddingTokenCost,
                    ImageCostPerImage = modelCost.ImageCostPerImage,
                    VideoCostPerSecond = modelCost.VideoCostPerSecond,
                    VideoResolutionMultipliers = videoResolutionMultipliers,
                    BatchProcessingMultiplier = modelCost.BatchProcessingMultiplier
                };
            }
        }

        /// <summary>
        /// Adapter that wraps Configuration's ICacheService to implement Core's interface.
        /// </summary>
        private class CacheServiceAdapter : ICacheService
        {
            private readonly ConduitLLM.Configuration.Services.ICacheService _innerService;

            public CacheServiceAdapter(ConduitLLM.Configuration.Services.ICacheService innerService)
            {
                _innerService = innerService;
            }

            public T? Get<T>(string key) => _innerService.Get<T>(key);

            public void Set<T>(string key, T value, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
                => _innerService.Set(key, value, absoluteExpiration, slidingExpiration);

            public void Remove(string key) => _innerService.Remove(key);

            public Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
                => _innerService.GetOrCreateAsync(key, factory, absoluteExpiration, slidingExpiration);

            public void RemoveByPrefix(string prefix) => _innerService.RemoveByPrefix(prefix);
        }
    }
}
