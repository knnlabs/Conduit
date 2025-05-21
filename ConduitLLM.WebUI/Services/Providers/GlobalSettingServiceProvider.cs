using ConduitLLM.Configuration.Options;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services.Providers
{
    /// <summary>
    /// Implementation of IGlobalSettingService that uses IAdminApiClient to interact with the Admin API.
    /// </summary>
    public class GlobalSettingServiceProvider : IGlobalSettingService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<GlobalSettingServiceProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalSettingServiceProvider"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public GlobalSettingServiceProvider(
            IAdminApiClient adminApiClient,
            ILogger<GlobalSettingServiceProvider> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<string?> GetSettingAsync(string key)
        {
            try
            {
                var setting = await _adminApiClient.GetGlobalSettingByKeyAsync(key);
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
                await _adminApiClient.UpsertGlobalSettingAsync(new ConduitLLM.Configuration.DTOs.GlobalSettingDto
                {
                    Key = key,
                    Value = value
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting value for {Key}", key);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<string?> GetMasterKeyHashAsync()
        {
            try
            {
                var setting = await _adminApiClient.GetGlobalSettingByKeyAsync("MasterKeyHash");
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
                var setting = await _adminApiClient.GetGlobalSettingByKeyAsync("MasterKeyHashAlgorithm");
                return setting?.Value ?? "SHA256"; // Default to SHA256 if not specified
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving master key hash algorithm");
                return "SHA256"; // Default to SHA256 on error
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
                // Get the hashing algorithm (default to SHA256)
                string hashAlgorithm = await GetMasterKeyHashAlgorithmAsync() ?? "SHA256";

                // Hash the master key
                string hashedKey = HashMasterKey(masterKey, hashAlgorithm);

                // Store the hash
                await _adminApiClient.UpsertGlobalSettingAsync(new ConduitLLM.Configuration.DTOs.GlobalSettingDto
                {
                    Key = "MasterKeyHash",
                    Value = hashedKey
                });

                // Store the algorithm used
                await _adminApiClient.UpsertGlobalSettingAsync(new ConduitLLM.Configuration.DTOs.GlobalSettingDto
                {
                    Key = "MasterKeyHashAlgorithm",
                    Value = hashAlgorithm
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting master key");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderHealthOptions?> GetProviderHealthOptionsAsync()
        {
            try
            {
                var enabledSetting = await _adminApiClient.GetGlobalSettingByKeyAsync("ProviderHealthMonitoring:Enabled");
                var intervalSetting = await _adminApiClient.GetGlobalSettingByKeyAsync("ProviderHealthMonitoring:IntervalMinutes");
                var retryCountSetting = await _adminApiClient.GetGlobalSettingByKeyAsync("ProviderHealthMonitoring:RetryCount");
                var retryIntervalSetting = await _adminApiClient.GetGlobalSettingByKeyAsync("ProviderHealthMonitoring:RetryIntervalSeconds");

                bool isEnabled = enabledSetting?.Value != null && bool.TryParse(enabledSetting.Value, out bool enabled) && enabled;
                int interval = intervalSetting?.Value != null && int.TryParse(intervalSetting.Value, out int minutes) ? minutes : 5;
                int retryCount = retryCountSetting?.Value != null && int.TryParse(retryCountSetting.Value, out int count) ? count : 3;
                int retryInterval = retryIntervalSetting?.Value != null && int.TryParse(retryIntervalSetting.Value, out int seconds) ? seconds : 5;

                return new ProviderHealthOptions
                {
                    Enabled = isEnabled,
                    DefaultCheckIntervalMinutes = interval,
                    DefaultRetryAttempts = retryCount,
                    DefaultTimeoutSeconds = retryInterval
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider health options");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task SaveProviderHealthOptionsAsync(ProviderHealthOptions options)
        {
            try
            {
                await _adminApiClient.UpsertGlobalSettingAsync(new ConduitLLM.Configuration.DTOs.GlobalSettingDto
                {
                    Key = "ProviderHealthMonitoring:Enabled",
                    Value = options.Enabled.ToString()
                });

                await _adminApiClient.UpsertGlobalSettingAsync(new ConduitLLM.Configuration.DTOs.GlobalSettingDto
                {
                    Key = "ProviderHealthMonitoring:IntervalMinutes",
                    Value = options.DefaultCheckIntervalMinutes.ToString()
                });

                await _adminApiClient.UpsertGlobalSettingAsync(new ConduitLLM.Configuration.DTOs.GlobalSettingDto
                {
                    Key = "ProviderHealthMonitoring:RetryCount",
                    Value = options.DefaultRetryAttempts.ToString()
                });

                await _adminApiClient.UpsertGlobalSettingAsync(new ConduitLLM.Configuration.DTOs.GlobalSettingDto
                {
                    Key = "ProviderHealthMonitoring:RetryIntervalSeconds",
                    Value = options.DefaultTimeoutSeconds.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving provider health options");
                throw;
            }
        }

        private string HashMasterKey(string masterKey, string algorithm)
        {
            // Use specific hash algorithm implementations instead of the generic factory
            using System.Security.Cryptography.HashAlgorithm hashAlgorithm = algorithm.ToUpperInvariant() switch
            {
                "SHA256" => System.Security.Cryptography.SHA256.Create(),
                "SHA384" => System.Security.Cryptography.SHA384.Create(),
                "SHA512" => System.Security.Cryptography.SHA512.Create(),
                "MD5" => System.Security.Cryptography.MD5.Create(),
                _ => throw new InvalidOperationException($"Hash algorithm {algorithm} is not supported")
            };
            
            byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(masterKey);
            byte[] hashBytes = hashAlgorithm.ComputeHash(inputBytes);
            
            return Convert.ToBase64String(hashBytes);
        }
    }
}