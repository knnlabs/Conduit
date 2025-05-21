using ConduitLLM.Providers.Configuration;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service that retrieves HTTP retry configuration from the Admin API.
    /// </summary>
    public class HttpRetryConfigurationService : IHttpRetryConfigurationService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<HttpRetryConfigurationService> _logger;
        private RetryOptions _currentRetryOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpRetryConfigurationService"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public HttpRetryConfigurationService(
            IAdminApiClient adminApiClient,
            ILogger<HttpRetryConfigurationService> logger)
        {
            _adminApiClient = adminApiClient;
            _logger = logger;
            _currentRetryOptions = new RetryOptions(); // Initialize with defaults
        }

        /// <inheritdoc />
        public async Task<bool> InitializeAsync()
        {
            _logger.LogInformation("Initializing HTTP retry configuration from Admin API");
            
            try
            {
                var result = await _adminApiClient.InitializeHttpRetryConfigurationAsync();
                
                if (result)
                {
                    _logger.LogInformation("HTTP retry configuration initialized successfully");
                    await LoadSettingsFromDatabaseAsync();
                }
                else
                {
                    _logger.LogWarning("Failed to initialize HTTP retry configuration");
                }
                
                return result;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error initializing HTTP retry configuration");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task UpdateRetryConfigurationAsync(RetryOptions retryOptions)
        {
            _logger.LogInformation("Updating HTTP retry configuration");
            
            try
            {
                // Save to global settings via admin API
                await _adminApiClient.SetSettingAsync("Conduit:HttpRetry:MaxRetries", retryOptions.MaxRetries.ToString());
                await _adminApiClient.SetSettingAsync("Conduit:HttpRetry:InitialDelaySeconds", retryOptions.InitialDelaySeconds.ToString());
                await _adminApiClient.SetSettingAsync("Conduit:HttpRetry:MaxDelaySeconds", retryOptions.MaxDelaySeconds.ToString());
                await _adminApiClient.SetSettingAsync("Conduit:HttpRetry:EnableRetryLogging", retryOptions.EnableRetryLogging.ToString());
                
                // Update the current options
                _currentRetryOptions = retryOptions;
                
                _logger.LogInformation("HTTP retry configuration updated successfully");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error updating HTTP retry configuration");
                throw;
            }
        }
        
        /// <inheritdoc />
        public async Task LoadSettingsFromDatabaseAsync()
        {
            _logger.LogInformation("Loading HTTP retry settings from database");
            
            try
            {
                var maxRetries = await _adminApiClient.GetSettingAsync("Conduit:HttpRetry:MaxRetries");
                var initialDelaySeconds = await _adminApiClient.GetSettingAsync("Conduit:HttpRetry:InitialDelaySeconds");
                var maxDelaySeconds = await _adminApiClient.GetSettingAsync("Conduit:HttpRetry:MaxDelaySeconds");
                var enableRetryLogging = await _adminApiClient.GetSettingAsync("Conduit:HttpRetry:EnableRetryLogging");
                
                var options = new RetryOptions();
                
                if (!string.IsNullOrEmpty(maxRetries) && int.TryParse(maxRetries, out var maxRetriesValue))
                {
                    options.MaxRetries = maxRetriesValue;
                }
                
                if (!string.IsNullOrEmpty(initialDelaySeconds) && int.TryParse(initialDelaySeconds, out var initialDelayValue))
                {
                    options.InitialDelaySeconds = initialDelayValue;
                }
                
                if (!string.IsNullOrEmpty(maxDelaySeconds) && int.TryParse(maxDelaySeconds, out var maxDelayValue))
                {
                    options.MaxDelaySeconds = maxDelayValue;
                }
                
                if (!string.IsNullOrEmpty(enableRetryLogging) && bool.TryParse(enableRetryLogging, out var enableLoggingValue))
                {
                    options.EnableRetryLogging = enableLoggingValue;
                }
                
                _currentRetryOptions = options;
                _logger.LogInformation("HTTP retry settings loaded from database");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error loading HTTP retry settings from database");
            }
        }
        
        /// <inheritdoc />
        public RetryOptions GetRetryConfiguration()
        {
            return _currentRetryOptions;
        }
    }
}