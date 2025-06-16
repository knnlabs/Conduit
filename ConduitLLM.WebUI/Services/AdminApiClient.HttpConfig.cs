namespace ConduitLLM.WebUI.Services
{
    public partial class AdminApiClient
    {
        /// <inheritdoc />
        public async Task<string?> GetSettingAsync(string key)
        {
            try
            {
                var setting = await GetGlobalSettingByKeyAsync(key);
                return setting?.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving setting {Key}", key);
                return null;
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
                await SetSettingAsync("HttpTimeout:TimeoutSeconds", defaultOptions.TimeoutSeconds.ToString());
                await SetSettingAsync("HttpTimeout:EnableTimeoutLogging", defaultOptions.EnableTimeoutLogging.ToString());

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
                await SetSettingAsync("HttpRetry:MaxRetries", defaultOptions.MaxRetries.ToString());
                await SetSettingAsync("HttpRetry:InitialDelaySeconds", defaultOptions.InitialDelaySeconds.ToString());
                await SetSettingAsync("HttpRetry:MaxDelaySeconds", defaultOptions.MaxDelaySeconds.ToString());
                await SetSettingAsync("HttpRetry:EnableRetryLogging", defaultOptions.EnableRetryLogging.ToString());

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing HTTP retry configuration");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<string?> GetMasterKeyHashAsync()
        {
            try
            {
                var setting = await GetGlobalSettingByKeyAsync("MasterKeyHash");
                return setting?.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving master key hash");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<string?> GetMasterKeyHashAlgorithmAsync()
        {
            try
            {
                var setting = await GetGlobalSettingByKeyAsync("MasterKeyHashAlgorithm");
                return setting?.Value ?? "SHA256"; // Default algorithm
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving master key hash algorithm");
                return "SHA256"; // Default algorithm
            }
        }

        /// <inheritdoc />
        public async Task SetMasterKeyAsync(string masterKey)
        {
            if (string.IsNullOrEmpty(masterKey))
            {
                throw new ArgumentException("Master key cannot be null or empty", nameof(masterKey));
            }

            try
            {
                // Get the algorithm
                var algorithm = await GetMasterKeyHashAlgorithmAsync() ?? "SHA256";

                // Hash the key
                string hash;
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(masterKey);
                    var hashBytes = sha.ComputeHash(bytes);
                    hash = Convert.ToBase64String(hashBytes);
                }

                // Save the hash
                await SetSettingAsync("MasterKeyHash", hash);
                await SetSettingAsync("MasterKeyHashAlgorithm", algorithm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting master key");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ConduitLLM.Configuration.Options.ProviderHealthOptions?> GetProviderHealthOptionsAsync()
        {
            try
            {
                var checkIntervalStr = await GetSettingAsync("Conduit:ProviderHealth:DefaultCheckIntervalMinutes");
                var retentionDaysStr = await GetSettingAsync("Conduit:ProviderHealth:DetailedRecordRetentionDays");
                var enabledStr = await GetSettingAsync("Conduit:ProviderHealth:Enabled");

                var options = new ConduitLLM.Configuration.Options.ProviderHealthOptions();

                if (!string.IsNullOrEmpty(checkIntervalStr) && int.TryParse(checkIntervalStr, out var checkInterval))
                {
                    options.DefaultCheckIntervalMinutes = checkInterval;
                }

                if (!string.IsNullOrEmpty(retentionDaysStr) && int.TryParse(retentionDaysStr, out var retentionDays))
                {
                    options.DetailedRecordRetentionDays = retentionDays;
                }

                if (!string.IsNullOrEmpty(enabledStr) && bool.TryParse(enabledStr, out var enabled))
                {
                    options.Enabled = enabled;
                }

                return options;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider health options");
                return new ConduitLLM.Configuration.Options.ProviderHealthOptions();
            }
        }

        /// <inheritdoc />
        public async Task SaveProviderHealthOptionsAsync(ConduitLLM.Configuration.Options.ProviderHealthOptions options)
        {
            try
            {
                await SetSettingAsync("Conduit:ProviderHealth:DefaultCheckIntervalMinutes", options.DefaultCheckIntervalMinutes.ToString());
                await SetSettingAsync("Conduit:ProviderHealth:DetailedRecordRetentionDays", options.DetailedRecordRetentionDays.ToString());
                await SetSettingAsync("Conduit:ProviderHealth:Enabled", options.Enabled.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving provider health options");
                throw;
            }
        }
    }
}
