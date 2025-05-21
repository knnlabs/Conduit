using ConduitLLM.Providers.Configuration;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services.Providers
{
    /// <summary>
    /// Service provider for managing HTTP timeout configuration using the Admin API.
    /// </summary>
    public class HttpTimeoutConfigurationServiceProvider : IHttpTimeoutConfigurationService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<HttpTimeoutConfigurationServiceProvider> _logger;
        private TimeoutOptions _currentTimeoutOptions;

        private const string TimeoutOptionsKey = "HttpTimeoutOptions";

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpTimeoutConfigurationServiceProvider"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public HttpTimeoutConfigurationServiceProvider(
            IAdminApiClient adminApiClient,
            ILogger<HttpTimeoutConfigurationServiceProvider> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Initialize with default values
            _currentTimeoutOptions = new TimeoutOptions
            {
                TimeoutSeconds = 100,
                EnableTimeoutLogging = true
            };
        }

        /// <inheritdoc />
        public TimeoutOptions GetTimeoutConfiguration()
        {
            return _currentTimeoutOptions;
        }

        /// <inheritdoc />
        public async Task LoadSettingsFromDatabaseAsync()
        {
            try
            {
                var serializedOptions = await _adminApiClient.GetSettingAsync(TimeoutOptionsKey);
                
                if (!string.IsNullOrWhiteSpace(serializedOptions))
                {
                    var options = JsonSerializer.Deserialize<TimeoutOptions>(serializedOptions);
                    if (options != null)
                    {
                        _currentTimeoutOptions = options;
                        _logger.LogInformation("Loaded HTTP timeout configuration from Admin API: {Options}", serializedOptions);
                    }
                }
                else
                {
                    _logger.LogInformation("No HTTP timeout configuration found in Admin API, using defaults");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading HTTP timeout configuration from Admin API");
            }
        }

        /// <inheritdoc />
        public async Task UpdateTimeoutConfigurationAsync(TimeoutOptions timeoutOptions)
        {
            try
            {
                if (timeoutOptions == null)
                {
                    throw new ArgumentNullException(nameof(timeoutOptions));
                }

                var serializedOptions = JsonSerializer.Serialize(timeoutOptions);
                await _adminApiClient.SetSettingAsync(TimeoutOptionsKey, serializedOptions);
                
                // Update the current options
                _currentTimeoutOptions = timeoutOptions;
                
                _logger.LogInformation("Updated HTTP timeout configuration via Admin API: {Options}", serializedOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating HTTP timeout configuration via Admin API");
                throw;
            }
        }
    }
}