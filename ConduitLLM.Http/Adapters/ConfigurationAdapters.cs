using ConduitLLM.Configuration;
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
            services.AddScoped<Core.Interfaces.Configuration.IModelProviderMappingService>(provider =>
                new ModelProviderMappingServiceAdapter(provider.GetRequiredService<ConduitLLM.Configuration.IModelProviderMappingService>()));

            services.AddScoped<Core.Interfaces.Configuration.IProviderCredentialService>(provider =>
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
        private class ModelProviderMappingServiceAdapter : Core.Interfaces.Configuration.IModelProviderMappingService
        {
            private readonly ConduitLLM.Configuration.IModelProviderMappingService _innerService;

            public ModelProviderMappingServiceAdapter(ConduitLLM.Configuration.IModelProviderMappingService innerService)
            {
                _innerService = innerService;
            }

            public async Task<List<Core.Interfaces.Configuration.ModelProviderMapping>> GetAllMappingsAsync()
            {
                var mappings = await _innerService.GetAllMappingsAsync();
                return mappings.Select(m => new Core.Interfaces.Configuration.ModelProviderMapping
                {
                    ModelAlias = m.ModelAlias,
                    ProviderId = m.ProviderId,
                    ProviderType = m.ProviderType,
                    ProviderModelId = m.ProviderModelId,
                    DeploymentName = m.DeploymentName,
                    IsEnabled = true, // Default
                    MaxContextTokens = null, // Default
                    SupportsImageGeneration = m.SupportsImageGeneration,
                    SupportsEmbeddings = m.SupportsEmbeddings
                }).ToList();
            }

            public async Task<Core.Interfaces.Configuration.ModelProviderMapping?> GetMappingByModelAliasAsync(string modelAlias)
            {
                var mapping = await _innerService.GetMappingByModelAliasAsync(modelAlias);
                if (mapping == null) return null;

                return new Core.Interfaces.Configuration.ModelProviderMapping
                {
                    ModelAlias = mapping.ModelAlias,
                    ProviderId = mapping.ProviderId,
                    ProviderType = mapping.ProviderType,
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
        private class ProviderCredentialServiceAdapter : Core.Interfaces.Configuration.IProviderCredentialService
        {
            private readonly ConduitLLM.Configuration.IProviderCredentialService _innerService;

            public ProviderCredentialServiceAdapter(ConduitLLM.Configuration.IProviderCredentialService innerService)
            {
                _innerService = innerService;
            }

            
            public async Task<Core.Interfaces.Configuration.ProviderCredentials?> GetCredentialByIdAsync(int providerId)
            {
                var credential = await _innerService.GetCredentialByIdAsync(providerId);
                if (credential == null) return null;

                // Get the primary key or first enabled key
                string? effectiveApiKey = null;
                string? effectiveBaseUrl = credential.BaseUrl;
                
                if (credential.ProviderKeyCredentials?.Any() == true)
                {
                    var primaryKey = credential.ProviderKeyCredentials.FirstOrDefault(k => k.IsPrimary && k.IsEnabled) ??
                                    credential.ProviderKeyCredentials.FirstOrDefault(k => k.IsEnabled);
                    if (primaryKey != null)
                    {
                        effectiveApiKey = primaryKey.ApiKey;
                        effectiveBaseUrl = primaryKey.BaseUrl ?? credential.BaseUrl;
                    }
                }

                return new Core.Interfaces.Configuration.ProviderCredentials
                {
                    ProviderId = credential.Id,
                    ApiKey = effectiveApiKey,
                    BaseUrl = effectiveBaseUrl,
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

                // Deserialize image quality multipliers if present
                Dictionary<string, decimal>? imageQualityMultipliers = null;
                if (!string.IsNullOrEmpty(modelCost.ImageQualityMultipliers))
                {
                    try
                    {
                        imageQualityMultipliers = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(modelCost.ImageQualityMultipliers);
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
                    BatchProcessingMultiplier = modelCost.BatchProcessingMultiplier,
                    SupportsBatchProcessing = modelCost.SupportsBatchProcessing,
                    ImageQualityMultipliers = imageQualityMultipliers,
                    CachedInputTokenCost = modelCost.CachedInputTokenCost,
                    CachedInputWriteCost = modelCost.CachedInputWriteCost,
                    CostPerSearchUnit = modelCost.CostPerSearchUnit
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
