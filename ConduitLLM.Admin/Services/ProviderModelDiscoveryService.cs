using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Implementation of provider-specific model discovery using sister classes.
    /// </summary>
    public class ProviderModelDiscoveryService : IProviderModelDiscovery
    {
        private readonly ILogger<ProviderModelDiscoveryService> _logger;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderModelDiscoveryService"/> class.
        /// </summary>
        /// <param name="logger">The logger for diagnostics.</param>
        public ProviderModelDiscoveryService(ILogger<ProviderModelDiscoveryService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <inheritdoc/>
        public async Task<List<DiscoveredModel>> DiscoverModelsAsync(
            Provider Provider, 
            HttpClient httpClient,
            string? apiKey = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Starting model discovery for provider '{ProviderName}' (ID: {ProviderId}, Type: {ProviderType})", 
                    Provider.ProviderName, Provider.Id, Provider.ProviderType);
                
                // If no API key provided, try to get it from the credential's keys
                if (string.IsNullOrEmpty(apiKey) && Provider.ProviderKeyCredentials?.Any() == true)
                {
                    var primaryKey = Provider.ProviderKeyCredentials
                        .FirstOrDefault(k => k.IsPrimary && k.IsEnabled) ?? 
                        Provider.ProviderKeyCredentials
                        .FirstOrDefault(k => k.IsEnabled);
                    
                    if (primaryKey != null)
                    {
                        apiKey = primaryKey.ApiKey;
                        _logger.LogDebug("Using API key from provider credential");
                    }
                }
                
                // If provider has a custom base URL, configure the HTTP client
                if (!string.IsNullOrEmpty(Provider.BaseUrl))
                {
                    _logger.LogDebug("Provider has custom base URL: {BaseUrl}", Provider.BaseUrl);
                    // Note: The individual discovery methods should handle custom base URLs
                }
                
                switch (Provider.ProviderType)
                {
                    case ProviderType.OpenAI:
                        _logger.LogDebug("Calling OpenAIModelDiscovery.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var openAIModels = await ConduitLLM.Providers.OpenAIModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("OpenAIModelDiscovery.DiscoverAsync returned {Count} models", openAIModels.Count);
                        // Ensure provider field is set correctly
                        foreach (var model in openAIModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return openAIModels;
                        
                    case ProviderType.Groq:
                        _logger.LogDebug("Calling GroqModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var groqModels = await ConduitLLM.Providers.GroqModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("GroqModels.DiscoverAsync returned {Count} models", groqModels.Count);
                        // Ensure provider field is set correctly
                        foreach (var model in groqModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return groqModels;
                        
                    case ProviderType.Cerebras:
                        _logger.LogDebug("Calling CerebrasModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var cerebrasModels = await ConduitLLM.Providers.CerebrasModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("CerebrasModels.DiscoverAsync returned {Count} models", cerebrasModels.Count);
                        // Ensure provider field is set correctly
                        foreach (var model in cerebrasModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return cerebrasModels;
                    
                    case ProviderType.MiniMax:
                        _logger.LogDebug("Calling MiniMaxModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var miniMaxModels = await ConduitLLM.Providers.MiniMaxModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("MiniMaxModels.DiscoverAsync returned {Count} models", miniMaxModels.Count);
                        // Ensure provider field is set correctly
                        foreach (var model in miniMaxModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return miniMaxModels;
                    
                    case ProviderType.Replicate:
                        _logger.LogDebug("Calling ReplicateModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var replicateModels = await ConduitLLM.Providers.ReplicateModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("ReplicateModels.DiscoverAsync returned {Count} models", replicateModels.Count);
                        foreach (var model in replicateModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return replicateModels;
                    
                    case ProviderType.Fireworks:
                        _logger.LogDebug("Calling FireworksModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var fireworksModels = await ConduitLLM.Providers.FireworksModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("FireworksModels.DiscoverAsync returned {Count} models", fireworksModels.Count);
                        foreach (var model in fireworksModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return fireworksModels;
                    
                    case ProviderType.OpenAICompatible:
                        _logger.LogDebug("Calling OpenAICompatibleModelDiscovery.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var openAICompatibleModels = await ConduitLLM.Providers.OpenAICompatibleModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("OpenAICompatibleModelDiscovery.DiscoverAsync returned {Count} models", openAICompatibleModels.Count);
                        foreach (var model in openAICompatibleModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return openAICompatibleModels;
                    
                    case ProviderType.SambaNova:
                        _logger.LogDebug("Calling SambaNovaModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var sambaNovaModels = await ConduitLLM.Providers.SambaNovaModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("SambaNovaModels.DiscoverAsync returned {Count} models", sambaNovaModels.Count);
                        foreach (var model in sambaNovaModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return sambaNovaModels;
                    
                    case ProviderType.DeepInfra:
                        _logger.LogDebug("Calling OpenAICompatibleModelDiscovery.DiscoverAsync for DeepInfra with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var deepInfraModels = await ConduitLLM.Providers.OpenAICompatibleModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("OpenAICompatibleModelDiscovery.DiscoverAsync returned {Count} models for DeepInfra", deepInfraModels.Count);
                        foreach (var model in deepInfraModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return deepInfraModels;
                    
                    default:
                        _logger.LogDebug("No provider-specific discovery available for {ProviderType}", Provider.ProviderType);
                        return new List<DiscoveredModel>();
                }
            }
            catch (NotSupportedException)
            {
                // Rethrow NotSupportedException so it can be handled by the controller
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering models for provider '{ProviderName}' (Type: {ProviderType})", 
                    Provider.ProviderName, Provider.ProviderType);
                return new List<DiscoveredModel>();
            }
        }
        
        /// <inheritdoc/>
        public bool SupportsDiscovery(ProviderType providerType)
        {
            // Check if this provider type has discovery support
            var supportedTypes = new[]
            {
                ProviderType.OpenAI,
                ProviderType.Groq,
                ProviderType.Cerebras,
                ProviderType.MiniMax,
                ProviderType.Replicate,
                ProviderType.Fireworks,
                ProviderType.OpenAICompatible,
                ProviderType.SambaNova,
                ProviderType.DeepInfra
            };
            
            var supported = supportedTypes.Contains(providerType);
            _logger.LogInformation("SupportsDiscovery called for provider type {ProviderType}: {Supported}", 
                providerType, supported);
            return supported;
        }
    }
}