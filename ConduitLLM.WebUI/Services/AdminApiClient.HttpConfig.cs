namespace ConduitLLM.WebUI.Services
{
    public partial class AdminApiClient
    {
        /// <inheritdoc />
        public async Task<string> GetSettingAsync(string key)
        {
            try
            {
                var setting = await GetGlobalSettingByKeyAsync(key);
                return setting?.Value ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving setting {Key}", key);
                return string.Empty;
            }
        }
        
        /// <inheritdoc />
        public async Task SetSettingAsync(string key, string value)
        {
            try
            {
                await UpsertGlobalSettingAsync(new ConduitLLM.Configuration.DTOs.GlobalSettingDto
                {
                    Key = key,
                    Value = value
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting {Key}", key);
                throw;
            }
        }
        
        /// <inheritdoc />
        public async Task<bool> InitializeHttpTimeoutConfigurationAsync()
        {
            try
            {
                // We'll initialize with default values
                var defaultOptions = new ConduitLLM.Providers.Configuration.TimeoutOptions();
                
                // Save to global settings via by-key endpoint
                await SetSettingAsync("Conduit:HttpTimeout:TimeoutSeconds", defaultOptions.TimeoutSeconds.ToString());
                await SetSettingAsync("Conduit:HttpTimeout:EnableTimeoutLogging", defaultOptions.EnableTimeoutLogging.ToString());
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing HTTP timeout configuration");
                return false;
            }
        }
        
        /// <inheritdoc />
        public async Task<bool> InitializeHttpRetryConfigurationAsync()
        {
            try
            {
                // We'll initialize with default values
                var defaultOptions = new ConduitLLM.Providers.Configuration.RetryOptions();
                
                // Save to global settings via by-key endpoint
                await SetSettingAsync("Conduit:HttpRetry:MaxRetries", defaultOptions.MaxRetries.ToString());
                await SetSettingAsync("Conduit:HttpRetry:InitialDelaySeconds", defaultOptions.InitialDelaySeconds.ToString());
                await SetSettingAsync("Conduit:HttpRetry:MaxDelaySeconds", defaultOptions.MaxDelaySeconds.ToString());
                await SetSettingAsync("Conduit:HttpRetry:EnableRetryLogging", defaultOptions.EnableRetryLogging.ToString());
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing HTTP retry configuration");
                return false;
            }
        }
    }
}