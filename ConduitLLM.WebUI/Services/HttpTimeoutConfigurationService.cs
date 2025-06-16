using System.Threading.Tasks;

using ConduitLLM.Providers.Configuration;
using ConduitLLM.WebUI.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service that retrieves HTTP timeout configuration from the Admin API.
    /// </summary>
    public class HttpTimeoutConfigurationService : IHttpTimeoutConfigurationService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<HttpTimeoutConfigurationService> _logger;
        private TimeoutOptions _currentTimeoutOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpTimeoutConfigurationService"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public HttpTimeoutConfigurationService(
            IAdminApiClient adminApiClient,
            ILogger<HttpTimeoutConfigurationService> logger)
        {
            _adminApiClient = adminApiClient;
            _logger = logger;
            _currentTimeoutOptions = new TimeoutOptions(); // Initialize with defaults
        }

        /// <inheritdoc />
        public async Task<bool> InitializeAsync()
        {
            _logger.LogInformation("Initializing HTTP timeout configuration from Admin API");

            try
            {
                var result = await _adminApiClient.InitializeHttpTimeoutConfigurationAsync();

                if (result)
                {
                    _logger.LogInformation("HTTP timeout configuration initialized successfully");
                    await LoadSettingsFromDatabaseAsync();
                }
                else
                {
                    _logger.LogWarning("Failed to initialize HTTP timeout configuration");
                }

                return result;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error initializing HTTP timeout configuration");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task UpdateTimeoutConfigurationAsync(TimeoutOptions timeoutOptions)
        {
            _logger.LogInformation("Updating HTTP timeout configuration");

            try
            {
                // Save to global settings via admin API
                await _adminApiClient.SetSettingAsync("HttpTimeout:TimeoutSeconds", timeoutOptions.TimeoutSeconds.ToString());
                await _adminApiClient.SetSettingAsync("HttpTimeout:EnableTimeoutLogging", timeoutOptions.EnableTimeoutLogging.ToString());

                // Update the current options
                _currentTimeoutOptions = timeoutOptions;

                _logger.LogInformation("HTTP timeout configuration updated successfully");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error updating HTTP timeout configuration");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task LoadSettingsFromDatabaseAsync()
        {
            _logger.LogInformation("Loading HTTP timeout settings from database");

            try
            {
                var timeoutSeconds = await _adminApiClient.GetSettingAsync("HttpTimeout:TimeoutSeconds");
                var enableTimeoutLogging = await _adminApiClient.GetSettingAsync("HttpTimeout:EnableTimeoutLogging");

                var options = new TimeoutOptions();

                if (!string.IsNullOrEmpty(timeoutSeconds) && int.TryParse(timeoutSeconds, out var timeoutValue))
                {
                    options.TimeoutSeconds = timeoutValue;
                }

                if (!string.IsNullOrEmpty(enableTimeoutLogging) && bool.TryParse(enableTimeoutLogging, out var enableLoggingValue))
                {
                    options.EnableTimeoutLogging = enableLoggingValue;
                }

                _currentTimeoutOptions = options;
                _logger.LogInformation("HTTP timeout settings loaded from database");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error loading HTTP timeout settings from database");
            }
        }

        /// <inheritdoc />
        public TimeoutOptions GetTimeoutConfiguration()
        {
            return _currentTimeoutOptions;
        }
    }
}
