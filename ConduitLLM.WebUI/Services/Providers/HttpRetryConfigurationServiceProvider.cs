using ConduitLLM.Providers.Configuration;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services.Providers
{
    /// <summary>
    /// Service provider for managing HTTP retry configuration using the Admin API.
    /// </summary>
    public class HttpRetryConfigurationServiceProvider : IHttpRetryConfigurationService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<HttpRetryConfigurationServiceProvider> _logger;
        private RetryOptions _currentRetryOptions;

        private const string RetryOptionsKey = "HttpRetryOptions";

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpRetryConfigurationServiceProvider"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public HttpRetryConfigurationServiceProvider(
            IAdminApiClient adminApiClient,
            ILogger<HttpRetryConfigurationServiceProvider> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Initialize with default values
            _currentRetryOptions = new RetryOptions
            {
                MaxRetries = 3,
                InitialDelaySeconds = 1,
                MaxDelaySeconds = 30,
                EnableRetryLogging = true
            };
        }

        /// <inheritdoc />
        public RetryOptions GetRetryConfiguration()
        {
            return _currentRetryOptions;
        }

        /// <inheritdoc />
        public async Task LoadSettingsFromDatabaseAsync()
        {
            try
            {
                var serializedOptions = await _adminApiClient.GetSettingAsync(RetryOptionsKey);
                
                if (!string.IsNullOrWhiteSpace(serializedOptions))
                {
                    var options = JsonSerializer.Deserialize<RetryOptions>(serializedOptions);
                    if (options != null)
                    {
                        _currentRetryOptions = options;
                        _logger.LogInformation("Loaded HTTP retry configuration from Admin API: {Options}", serializedOptions);
                    }
                }
                else
                {
                    _logger.LogInformation("No HTTP retry configuration found in Admin API, using defaults");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading HTTP retry configuration from Admin API");
            }
        }

        /// <inheritdoc />
        public async Task UpdateRetryConfigurationAsync(RetryOptions retryOptions)
        {
            try
            {
                if (retryOptions == null)
                {
                    throw new ArgumentNullException(nameof(retryOptions));
                }

                var serializedOptions = JsonSerializer.Serialize(retryOptions);
                await _adminApiClient.SetSettingAsync(RetryOptionsKey, serializedOptions);
                
                // Update the current options
                _currentRetryOptions = retryOptions;
                
                _logger.LogInformation("Updated HTTP retry configuration via Admin API: {Options}", serializedOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating HTTP retry configuration via Admin API");
                throw;
            }
        }
    }
}