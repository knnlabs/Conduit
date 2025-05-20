using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Options;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that implements <see cref="IGlobalSettingService"/> using the Admin API client.
    /// </summary>
    public class GlobalSettingServiceAdapter : IGlobalSettingService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<GlobalSettingServiceAdapter> _logger;

        // Constants for settings keys
        private const string MasterKeyHashKey = "MasterKeyHash";
        private const string MasterKeyHashAlgorithmKey = "MasterKeyHashAlgorithm";
        private const string ProviderHealthOptionsKey = "ProviderHealthOptions";
        private const string DefaultHashAlgorithm = "SHA256";

        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalSettingServiceAdapter"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public GlobalSettingServiceAdapter(
            IAdminApiClient adminApiClient,
            ILogger<GlobalSettingServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<GlobalSettingDto>> GetAllGlobalSettingsAsync()
        {
            return await _adminApiClient.GetAllGlobalSettingsAsync();
        }

        /// <inheritdoc />
        public async Task<GlobalSettingDto?> GetGlobalSettingByKeyAsync(string key)
        {
            return await _adminApiClient.GetGlobalSettingByKeyAsync(key);
        }

        /// <inheritdoc />
        public async Task<GlobalSettingDto?> UpsertGlobalSettingAsync(GlobalSettingDto setting)
        {
            return await _adminApiClient.UpsertGlobalSettingAsync(setting);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteGlobalSettingAsync(string key)
        {
            return await _adminApiClient.DeleteGlobalSettingAsync(key);
        }

        /// <inheritdoc />
        public async Task<string?> GetSettingValueAsync(string key)
        {
            var setting = await _adminApiClient.GetGlobalSettingByKeyAsync(key);
            return setting?.Value;
        }

        /// <inheritdoc />
        public async Task<string?> GetSettingAsync(string key)
        {
            return await GetSettingValueAsync(key);
        }

        /// <inheritdoc />
        public async Task SetSettingAsync(string key, string value)
        {
            var setting = await _adminApiClient.GetGlobalSettingByKeyAsync(key);
            
            if (setting == null)
            {
                setting = new GlobalSettingDto
                {
                    Key = key,
                    Value = value
                };
            }
            else
            {
                setting.Value = value;
            }

            await _adminApiClient.UpsertGlobalSettingAsync(setting);
        }

        /// <inheritdoc />
        public async Task<bool> SetSettingValueAsync(string key, string value)
        {
            var setting = await _adminApiClient.GetGlobalSettingByKeyAsync(key);
            
            if (setting == null)
            {
                setting = new GlobalSettingDto
                {
                    Key = key,
                    Value = value
                };
            }
            else
            {
                setting.Value = value;
            }

            var result = await _adminApiClient.UpsertGlobalSettingAsync(setting);
            return result != null;
        }

        /// <inheritdoc />
        public async Task<string?> GetMasterKeyHashAsync()
        {
            return await GetSettingAsync(MasterKeyHashKey);
        }

        /// <inheritdoc />
        public async Task<string?> GetMasterKeyHashAlgorithmAsync()
        {
            var algorithm = await GetSettingAsync(MasterKeyHashAlgorithmKey);
            return string.IsNullOrEmpty(algorithm) ? DefaultHashAlgorithm : algorithm;
        }

        /// <inheritdoc />
        public async Task SetMasterKeyAsync(string masterKey)
        {
            if (string.IsNullOrEmpty(masterKey))
            {
                throw new ArgumentException("Master key cannot be null or empty", nameof(masterKey));
            }

            var algorithm = await GetMasterKeyHashAlgorithmAsync() ?? DefaultHashAlgorithm;
            var hash = ComputeHash(masterKey, algorithm);
            
            await SetSettingAsync(MasterKeyHashKey, hash);
            await SetSettingAsync(MasterKeyHashAlgorithmKey, algorithm);
        }

        /// <inheritdoc />
        public async Task<ProviderHealthOptions?> GetProviderHealthOptionsAsync()
        {
            var optionsJson = await GetSettingAsync(ProviderHealthOptionsKey);
            if (string.IsNullOrEmpty(optionsJson))
            {
                return new ProviderHealthOptions(); // Return default options
            }

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<ProviderHealthOptions>(optionsJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing provider health options");
                return new ProviderHealthOptions(); // Return default options on error
            }
        }

        /// <inheritdoc />
        public async Task SaveProviderHealthOptionsAsync(ProviderHealthOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var optionsJson = System.Text.Json.JsonSerializer.Serialize(options);
            await SetSettingAsync(ProviderHealthOptionsKey, optionsJson);
        }

        /// <summary>
        /// Computes a hash of the specified input using the specified algorithm.
        /// </summary>
        /// <param name="input">The input to hash.</param>
        /// <param name="algorithm">The hashing algorithm to use.</param>
        /// <returns>The computed hash as a hexadecimal string.</returns>
        private string ComputeHash(string input, string algorithm)
        {
            using System.Security.Cryptography.HashAlgorithm hashAlgorithm = algorithm.ToUpperInvariant() switch
            {
                "SHA256" => SHA256.Create(),
                "SHA384" => SHA384.Create(),
                "SHA512" => SHA512.Create(),
                "MD5" => MD5.Create(),
                _ => SHA256.Create() // Default to SHA256
            };

            var inputBytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = hashAlgorithm.ComputeHash(inputBytes);
            
            var sb = new StringBuilder();
            foreach (var b in hashBytes)
            {
                sb.Append(b.ToString("x2"));
            }
            
            return sb.ToString();
        }
    }
}