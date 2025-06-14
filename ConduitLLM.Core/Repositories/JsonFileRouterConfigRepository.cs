using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Routing;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Repositories
{
    /// <summary>
    /// A simple file-based implementation of the router config repository that stores data in a JSON file
    /// </summary>
    public class JsonFileRouterConfigRepository : IRouterConfigRepository
    {
        private readonly string _configFilePath;
        private readonly ILogger<JsonFileRouterConfigRepository> _logger;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true
        };

        /// <summary>
        /// Creates a new JsonFileRouterConfigRepository
        /// </summary>
        /// <param name="configFilePath">Path to the JSON file for storing router configuration</param>
        /// <param name="logger">Logger instance</param>
        public JsonFileRouterConfigRepository(string configFilePath, ILogger<JsonFileRouterConfigRepository> logger)
        {
            _configFilePath = configFilePath ?? throw new ArgumentNullException(nameof(configFilePath));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<RouterConfig?> GetRouterConfigAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(_configFilePath))
            {
                _logger.LogInformation("Router configuration file not found at {FilePath}", _configFilePath);
                return null;
            }

            try
            {
                using var fileStream = new FileStream(_configFilePath, FileMode.Open, FileAccess.Read);
                var config = await JsonSerializer.DeserializeAsync<RouterConfig>(
                    fileStream, _jsonOptions, cancellationToken);

                _logger.LogInformation("Loaded router configuration with {ModelCount} model deployments",
                    config?.ModelDeployments?.Count ?? 0);

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading router configuration from {FilePath}", _configFilePath);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task SaveRouterConfigAsync(RouterConfig config, CancellationToken cancellationToken = default)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            try
            {
                // Ensure the directory exists
                var directory = Path.GetDirectoryName(_configFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var fileStream = new FileStream(_configFilePath, FileMode.Create, FileAccess.Write);
                await JsonSerializer.SerializeAsync(fileStream, config, _jsonOptions, cancellationToken);

                _logger.LogInformation("Saved router configuration with {ModelCount} model deployments",
                    config.ModelDeployments?.Count ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving router configuration to {FilePath}", _configFilePath);
                throw;
            }
        }
    }
}
