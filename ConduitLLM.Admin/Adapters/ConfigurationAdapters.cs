using System.Collections.Generic;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Admin.Adapters
{
    /// <summary>
    /// Provides adapters that map Configuration services to Core interfaces for the Admin API.
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
                new ProviderServiceAdapter(provider.GetRequiredService<ConduitLLM.Configuration.IProviderService>()));

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

            public async Task<List<ConduitLLM.Configuration.Entities.ModelProviderMapping>> GetAllMappingsAsync()
            {
                return await _innerService.GetAllMappingsAsync();
            }

            public async Task<ConduitLLM.Configuration.Entities.ModelProviderMapping?> GetMappingByModelAliasAsync(string modelAlias)
            {
                return await _innerService.GetMappingByModelAliasAsync(modelAlias);
            }
        }

        /// <summary>
        /// Adapter that wraps Configuration's IProviderService to implement Core's interface.
        /// </summary>
        private class ProviderServiceAdapter : Core.Interfaces.Configuration.IProviderCredentialService
        {
            private readonly ConduitLLM.Configuration.IProviderService _innerService;

            public ProviderServiceAdapter(ConduitLLM.Configuration.IProviderService innerService)
            {
                _innerService = innerService;
            }

            
            public async Task<Provider?> GetCredentialByIdAsync(int providerId)
            {
                // Use GetProviderByIdAsync since that's what the Configuration service has
                return await _innerService.GetProviderByIdAsync(providerId);
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
                    ModelIdPattern = modelCost.CostName, // Using CostName as the identifier
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
                    CostPerSearchUnit = modelCost.CostPerSearchUnit,
                    CostPerInferenceStep = modelCost.CostPerInferenceStep,
                    DefaultInferenceSteps = modelCost.DefaultInferenceSteps
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
