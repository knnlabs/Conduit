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
    /// Service adapter for HTTP retry configuration using Admin API
    /// </summary>
    public class HttpRetryConfigurationServiceAdapter : IHttpRetryConfigurationService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly IOptionsMonitor<RetryOptions> _options;
        private readonly ILogger<HttpRetryConfigurationServiceAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the HttpRetryConfigurationServiceAdapter class
        /// </summary>
        /// <param name="adminApiClient">Admin API client for accessing configuration settings</param>
        /// <param name="options">Options monitor for retry configuration</param>
        /// <param name="logger">Logger for tracking service operations</param>
        public HttpRetryConfigurationServiceAdapter(
            IAdminApiClient adminApiClient,
            IOptionsMonitor<RetryOptions> options,
            ILogger<HttpRetryConfigurationServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task UpdateRetryConfigurationAsync(RetryOptions retryOptions)
        {
            try
            {
                await _adminApiClient.SetSettingAsync("HttpRetry:MaxRetries", retryOptions.MaxRetries.ToString());
                await _adminApiClient.SetSettingAsync("HttpRetry:InitialDelaySeconds", retryOptions.InitialDelaySeconds.ToString());
                await _adminApiClient.SetSettingAsync("HttpRetry:MaxDelaySeconds", retryOptions.MaxDelaySeconds.ToString());
                await _adminApiClient.SetSettingAsync("HttpRetry:EnableRetryLogging", retryOptions.EnableRetryLogging.ToString());
                
                _logger.LogInformation("HTTP retry configuration updated via Admin API: MaxRetries={MaxRetries}, InitialDelay={InitialDelay}s, MaxDelay={MaxDelay}s, Logging={Logging}",
                    retryOptions.MaxRetries, retryOptions.InitialDelaySeconds, retryOptions.MaxDelaySeconds, retryOptions.EnableRetryLogging);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating HTTP retry configuration");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task LoadSettingsFromDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("Loading HTTP retry configuration from Admin API");
                
                var maxRetryStr = await _adminApiClient.GetSettingAsync("HttpRetry:MaxRetries");
                var initialDelayStr = await _adminApiClient.GetSettingAsync("HttpRetry:InitialDelaySeconds");
                var maxDelayStr = await _adminApiClient.GetSettingAsync("HttpRetry:MaxDelaySeconds");
                var enableLoggingStr = await _adminApiClient.GetSettingAsync("HttpRetry:EnableRetryLogging");
                
                var options = new RetryOptions();
                
                if (int.TryParse(maxRetryStr, out int maxRetries))
                {
                    options.MaxRetries = maxRetries;
                }
                
                if (int.TryParse(initialDelayStr, out int initialDelay))
                {
                    options.InitialDelaySeconds = initialDelay;
                }
                
                if (int.TryParse(maxDelayStr, out int maxDelay))
                {
                    options.MaxDelaySeconds = maxDelay;
                }
                
                if (bool.TryParse(enableLoggingStr, out bool enableLogging))
                {
                    options.EnableRetryLogging = enableLogging;
                }
                
                // Update current value of options
                UpdateOptionsValue(_options.CurrentValue, options);
                
                _logger.LogInformation("HTTP retry configuration loaded: MaxRetries={MaxRetries}, InitialDelay={InitialDelay}s, MaxDelay={MaxDelay}s, Logging={Logging}",
                    options.MaxRetries, options.InitialDelaySeconds, options.MaxDelaySeconds, options.EnableRetryLogging);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading HTTP retry configuration from Admin API");
            }
        }

        /// <summary>
        /// Gets the current retry configuration
        /// </summary>
        /// <returns>The current retry configuration</returns>
        public RetryOptions GetRetryConfiguration()
        {
            return _options.CurrentValue;
        }
        
        /// <summary>
        /// Updates the options instance with new values
        /// </summary>
        /// <param name="currentOptions">The current options instance</param>
        /// <param name="newOptions">The new options values</param>
        private void UpdateOptionsValue(RetryOptions currentOptions, RetryOptions newOptions)
        {
            // Use reflection to update properties
            var properties = typeof(RetryOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance);
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