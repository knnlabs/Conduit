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
        private readonly ConduitSettings _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseAwareLLMClientFactory"/> class.
        /// </summary>
        public DatabaseAwareLLMClientFactory(
            IProviderCredentialService credentialService,
            IOptions<ConduitSettings> settingsOptions,
            ILoggerFactory loggerFactory,
            IHttpClientFactory httpClientFactory,
            ILogger<DatabaseAwareLLMClientFactory> logger)
        {
            _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settingsOptions?.Value ?? new ConduitSettings();
        }

        /// <inheritdoc />
        public ILLMClient GetClient(string modelName)
        {
            // For model-based lookup, use the base factory with existing settings
            var baseFactory = new LLMClientFactory(
                Microsoft.Extensions.Options.Options.Create(_settings),
                _loggerFactory,
                _httpClientFactory);
            
            return baseFactory.GetClient(modelName);
        }

        /// <inheritdoc />
        public ILLMClient GetClientByProvider(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or whitespace.", nameof(providerName));
            }

            _logger.LogDebug("Getting client for provider {ProviderName} using database credentials", providerName);

            // Get credentials from database synchronously (not ideal but matches interface)
            var credentials = Task.Run(async () => 
                await _credentialService.GetCredentialByProviderNameAsync(providerName)).Result;

            if (credentials == null || !credentials.IsEnabled)
            {
                _logger.LogWarning("No enabled credentials found for provider {ProviderName} in database", providerName);
                throw new ConfigurationException($"No provider credentials found for provider '{providerName}'. Please check your Conduit configuration.");
            }

            // Create a temporary ConduitSettings with the database credentials
            var tempSettings = new ConduitSettings
            {
                ProviderCredentials = new System.Collections.Generic.List<ProviderCredentials>
                {
                    new ProviderCredentials
                    {
                        ProviderName = credentials.ProviderName,
                        ApiKey = credentials.ApiKey
                    }
                }
            };

            // Create a new factory with the database credentials
            var tempFactory = new LLMClientFactory(
                Microsoft.Extensions.Options.Options.Create(tempSettings),
                _loggerFactory,
                _httpClientFactory);

            return tempFactory.GetClientByProvider(providerName);
        }
    }
}