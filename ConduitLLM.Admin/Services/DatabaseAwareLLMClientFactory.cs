using System;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Database-aware implementation of ILLMClientFactory that uses provider credentials from the database.
    /// </summary>
    public class DatabaseAwareLLMClientFactory : ILLMClientFactory
    {
        private readonly IProviderCredentialService _credentialService;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DatabaseAwareLLMClientFactory> _logger;
        private readonly IOptionsMonitor<ConduitSettings> _settingsMonitor;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseAwareLLMClientFactory"/> class.
        /// </summary>
        public DatabaseAwareLLMClientFactory(
            IProviderCredentialService credentialService,
            IOptionsMonitor<ConduitSettings> settingsMonitor,
            ILoggerFactory loggerFactory,
            IHttpClientFactory httpClientFactory,
            ILogger<DatabaseAwareLLMClientFactory> logger)
        {
            _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settingsMonitor = settingsMonitor ?? throw new ArgumentNullException(nameof(settingsMonitor));
        }

        /// <inheritdoc />
        public ILLMClient GetClient(string modelName)
        {
            // Get current settings from the monitor to ensure we have the latest
            var currentSettings = _settingsMonitor.CurrentValue;
            
            _logger.LogDebug("DatabaseAwareLLMClientFactory.GetClient called for model: {ModelName}", modelName);
            _logger.LogDebug("Current settings - ModelMappings count: {Count}", currentSettings.ModelMappings?.Count ?? 0);
            
            if (currentSettings.ModelMappings != null && currentSettings.ModelMappings.Any())
            {
                foreach (var mapping in currentSettings.ModelMappings)
                {
                    _logger.LogDebug("Settings contain mapping: {ModelAlias} -> {ProviderType}/{ProviderModelId}", 
                        mapping.ModelAlias, mapping.ProviderType, mapping.ProviderModelId);
                }
            }
            else
            {
                _logger.LogWarning("No model mappings found in settings!");
            }
            
            // For model-based lookup, use the base factory with current settings
            var baseFactory = new LLMClientFactory(
                Microsoft.Extensions.Options.Options.Create(currentSettings),
                _loggerFactory,
                _httpClientFactory);
            
            return baseFactory.GetClient(modelName);
        }

        
        /// <inheritdoc />
        public ILLMClient GetClientByProviderId(int providerId)
        {
            _logger.LogDebug("Getting client for provider ID {ProviderId} using database credentials", providerId);

            // Get credentials from database synchronously (not ideal but matches interface)
            var credentials = Task.Run(async () => 
                await _credentialService.GetCredentialByIdAsync(providerId)).Result;

            if (credentials == null || !credentials.IsEnabled)
            {
                _logger.LogWarning("No enabled credentials found for provider ID {ProviderId} in database", providerId);
                throw new ConfigurationException($"No provider credentials found for provider ID '{providerId}'. Please check your Conduit configuration.");
            }

            // Get current settings and create a temporary ConduitSettings with the database credentials
            var currentSettings = _settingsMonitor.CurrentValue;
            var tempSettings = new ConduitSettings
            {
                ProviderCredentials = new System.Collections.Generic.List<ProviderCredentials>
                {
                    new ProviderCredentials
                    {
                        ProviderType = credentials.ProviderType,
                        ApiKey = credentials.ProviderKeyCredentials?.FirstOrDefault(k => k.IsPrimary && k.IsEnabled)?.ApiKey ??
                                credentials.ProviderKeyCredentials?.FirstOrDefault(k => k.IsEnabled)?.ApiKey,
                        BaseUrl = credentials.BaseUrl
                    }
                },
                // Copy other relevant settings from current settings
                ModelMappings = currentSettings.ModelMappings,
                DefaultModels = currentSettings.DefaultModels,
                PerformanceTracking = currentSettings.PerformanceTracking
            };

            // Create a new factory with the database credentials
            var tempFactory = new LLMClientFactory(
                Microsoft.Extensions.Options.Options.Create(tempSettings),
                _loggerFactory,
                _httpClientFactory);

            return tempFactory.GetClientByProviderId(providerId);
        }

        /// <inheritdoc />
        public IProviderMetadata? GetProviderMetadata(ConduitLLM.Configuration.ProviderType providerType)
        {
            // This factory doesn't have access to provider metadata
            // Return null to indicate metadata is not available through this factory
            return null;
        }
    }
}