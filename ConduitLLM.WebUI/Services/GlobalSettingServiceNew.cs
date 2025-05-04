using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Repository-based implementation of the global settings service
    /// </summary>
    public class GlobalSettingServiceNew : IGlobalSettingService
    {
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly ILogger<GlobalSettingServiceNew> _logger;

        private const string MASTER_KEY_HASH_KEY = "MasterKeyHash";
        private const string MASTER_KEY_HASH_ALGORITHM_KEY = "MasterKeyHashAlgorithm";

        /// <summary>
        /// Creates a new instance of the GlobalSettingServiceNew
        /// </summary>
        public GlobalSettingServiceNew(
            IGlobalSettingRepository globalSettingRepository,
            ILogger<GlobalSettingServiceNew> logger)
        {
            _globalSettingRepository = globalSettingRepository ?? throw new ArgumentNullException(nameof(globalSettingRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<string?> GetSettingAsync(string key)
        {
            try
            {
                var setting = await _globalSettingRepository.GetByKeyAsync(key);
                return setting?.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting setting for key {Key}", key);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task SetSettingAsync(string key, string value)
        {
            try
            {
                // Use the repository's Upsert method to create or update the setting
                await _globalSettingRepository.UpsertAsync(key, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting value for key {Key}", key);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<string?> GetMasterKeyHashAsync()
        {
            return await GetSettingAsync(MASTER_KEY_HASH_KEY);
        }

        /// <inheritdoc/>
        public async Task<string?> GetMasterKeyHashAlgorithmAsync()
        {
            return await GetSettingAsync(MASTER_KEY_HASH_ALGORITHM_KEY) ?? "SHA256";
        }

        /// <inheritdoc/>
        public async Task SetMasterKeyAsync(string masterKey)
        {
            if (string.IsNullOrEmpty(masterKey))
            {
                throw new ArgumentException("Master key cannot be empty", nameof(masterKey));
            }

            try
            {
                // Use SHA256 as the default algorithm
                string algorithm = "SHA256";
                
                // Hash the master key
                string hashedKey = HashMasterKey(masterKey, algorithm);
                
                // Save the hash and algorithm
                await SetSettingAsync(MASTER_KEY_HASH_KEY, hashedKey);
                await SetSettingAsync(MASTER_KEY_HASH_ALGORITHM_KEY, algorithm);
                
                _logger.LogInformation("Master key hash updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting master key");
                throw;
            }
        }

        /// <summary>
        /// Hashes the master key using the specified algorithm
        /// </summary>
        private string HashMasterKey(string key, string algorithm)
        {
            // Create the appropriate hash algorithm instance
            using var hasher = GetHashAlgorithmInstance(algorithm);
            
            // Convert the key to bytes and compute the hash
            var bytes = Encoding.UTF8.GetBytes(key);
            var hashBytes = hasher.ComputeHash(bytes);
            
            // Convert the hash bytes to a lowercase hexadecimal string
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Gets a hash algorithm instance based on the algorithm name
        /// </summary>
        private HashAlgorithm GetHashAlgorithmInstance(string algorithm)
        {
            return algorithm.ToUpperInvariant() switch
            {
                "SHA256" => SHA256.Create(),
                "SHA384" => SHA384.Create(),
                "SHA512" => SHA512.Create(),
                _ => SHA256.Create() // Default to SHA256
            };
        }
    }
}