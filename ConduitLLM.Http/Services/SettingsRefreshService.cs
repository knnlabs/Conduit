using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Http.Interfaces;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for refreshing in-memory settings from the database.
    /// Since provider configuration is now entirely database-driven,
    /// this service is mostly a placeholder for any future settings that might need refreshing.
    /// </summary>
    public class SettingsRefreshService : ISettingsRefreshService
    {
        private readonly ILogger<SettingsRefreshService> _logger;

        public SettingsRefreshService(ILogger<SettingsRefreshService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Task RefreshModelMappingsAsync()
        {
            // Model mappings are now accessed directly from the database through services
            _logger.LogInformation("Model mappings refresh requested - mappings are now accessed directly from database");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task RefreshProvidersAsync()
        {
            // Provider configuration is now entirely database-driven
            _logger.LogInformation("Provider refresh requested - providers are now accessed directly from database");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task RefreshAllSettingsAsync()
        {
            // Since all configuration is database-driven, nothing to refresh
            _logger.LogInformation("Settings refresh requested - all settings are now accessed directly from database");
            return Task.CompletedTask;
        }
    }
}