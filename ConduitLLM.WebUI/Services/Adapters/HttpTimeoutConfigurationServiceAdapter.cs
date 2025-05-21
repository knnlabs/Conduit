using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.Providers.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Service adapter for HTTP timeout configuration using Admin API
    /// </summary>
    public class HttpTimeoutConfigurationServiceAdapter : IHttpTimeoutConfigurationService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly IOptionsMonitor<TimeoutOptions> _options;
        private readonly ILogger<HttpTimeoutConfigurationServiceAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the HttpTimeoutConfigurationServiceAdapter class
        /// </summary>
        /// <param name="adminApiClient">Admin API client for accessing configuration settings</param>
        /// <param name="options">Options monitor for timeout configuration</param>
        /// <param name="logger">Logger for tracking service operations</param>
        public HttpTimeoutConfigurationServiceAdapter(
            IAdminApiClient adminApiClient,
            IOptionsMonitor<TimeoutOptions> options,
            ILogger<HttpTimeoutConfigurationServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task UpdateTimeoutConfigurationAsync(TimeoutOptions timeoutOptions)
        {
            try
            {
                await _adminApiClient.SetSettingAsync("HttpTimeout:TimeoutSeconds", timeoutOptions.TimeoutSeconds.ToString());
                await _adminApiClient.SetSettingAsync("HttpTimeout:EnableTimeoutLogging", timeoutOptions.EnableTimeoutLogging.ToString());
                
                _logger.LogInformation("HTTP timeout configuration updated via Admin API: Timeout={Timeout}s, EnableLogging={EnableLogging}",
                    timeoutOptions.TimeoutSeconds, timeoutOptions.EnableTimeoutLogging);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating HTTP timeout configuration");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task LoadSettingsFromDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("Loading HTTP timeout configuration from Admin API");
                
                var timeoutStr = await _adminApiClient.GetSettingAsync("HttpTimeout:TimeoutSeconds");
                var enableLoggingStr = await _adminApiClient.GetSettingAsync("HttpTimeout:EnableTimeoutLogging");
                
                var options = new TimeoutOptions();
                
                if (int.TryParse(timeoutStr, out int timeout))
                {
                    options.TimeoutSeconds = timeout;
                }
                
                if (bool.TryParse(enableLoggingStr, out bool enableLogging))
                {
                    options.EnableTimeoutLogging = enableLogging;
                }
                
                // Update current value of options
                UpdateOptionsValue(_options.CurrentValue, options);
                
                _logger.LogInformation("HTTP timeout configuration loaded: Timeout={Timeout}s, EnableLogging={EnableLogging}",
                    options.TimeoutSeconds, options.EnableTimeoutLogging);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading HTTP timeout configuration from Admin API");
            }
        }

        /// <summary>
        /// Gets the current timeout configuration
        /// </summary>
        /// <returns>The current timeout configuration</returns>
        public TimeoutOptions GetTimeoutConfiguration()
        {
            return _options.CurrentValue;
        }

        /// <summary>
        /// Updates the options instance with new values
        /// </summary>
        /// <param name="currentOptions">The current options instance</param>
        /// <param name="newOptions">The new options values</param>
        private void UpdateOptionsValue(TimeoutOptions currentOptions, TimeoutOptions newOptions)
        {
            // Use reflection to update properties
            var properties = typeof(TimeoutOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (property.CanWrite)
                {
                    var newValue = property.GetValue(newOptions);
                    property.SetValue(currentOptions, newValue);
                }
            }
        }
    }
}