using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for managing virtual keys through the Admin API
    /// </summary>
    public class AdminVirtualKeyService : IAdminVirtualKeyService
    {
        private readonly IVirtualKeyRepository _virtualKeyRepository;
        private readonly IVirtualKeySpendHistoryRepository _spendHistoryRepository;
        private readonly ILogger<AdminVirtualKeyService> _logger;
        private const int KeyLengthBytes = 32; // Generate a 256-bit key
        
        /// <summary>
        /// Initializes a new instance of the AdminVirtualKeyService class
        /// </summary>
        /// <param name="virtualKeyRepository">The virtual key repository</param>
        /// <param name="spendHistoryRepository">The spend history repository</param>
        /// <param name="logger">The logger</param>
        public AdminVirtualKeyService(
            IVirtualKeyRepository virtualKeyRepository,
            IVirtualKeySpendHistoryRepository spendHistoryRepository,
            ILogger<AdminVirtualKeyService> logger)
        {
            _virtualKeyRepository = virtualKeyRepository ?? throw new ArgumentNullException(nameof(virtualKeyRepository));
            _spendHistoryRepository = spendHistoryRepository ?? throw new ArgumentNullException(nameof(spendHistoryRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <inheritdoc />
        public async Task<CreateVirtualKeyResponseDto> GenerateVirtualKeyAsync(CreateVirtualKeyRequestDto request)
        {
            _logger.LogInformation("Generating new virtual key with name: {KeyName}", request.KeyName);
            
            // Generate a secure random key
            var keyBytes = new byte[KeyLengthBytes];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(keyBytes);
            var apiKey = Convert.ToBase64String(keyBytes);
            
            // Hash the key for storage
            var keyHash = ComputeSha256Hash(apiKey);
            
            // Create the virtual key entity
            var virtualKey = new VirtualKey
            {
                KeyName = request.KeyName,
                KeyHash = keyHash,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ExpiresAt = request.ExpiresAt,
                MaxBudget = request.MaxBudget,
                CurrentSpend = 0,
                IsEnabled = true,
                AllowedModels = request.AllowedModels,
                Metadata = request.Metadata,
                BudgetDuration = request.BudgetDuration,
                BudgetStartDate = DateTime.UtcNow,
                RateLimitRpm = request.RateLimitRpm,
                RateLimitRpd = request.RateLimitRpd
            };
            
            // Save to database
            var id = await _virtualKeyRepository.CreateAsync(virtualKey);
            
            // The entity is saved with an ID, now retrieve it to get all properties
            virtualKey = await _virtualKeyRepository.GetByIdAsync(id);
            if (virtualKey == null)
            {
                throw new InvalidOperationException($"Failed to retrieve newly created virtual key with ID {id}");
            }
            
            // Initialize spend history
            if (request.MaxBudget.HasValue && request.MaxBudget.Value > 0)
            {
                var history = new VirtualKeySpendHistory
                {
                    VirtualKeyId = virtualKey.Id,
                    Amount = 0,
                    Date = DateTime.UtcNow,
                    Timestamp = DateTime.UtcNow
                };
                
                await _spendHistoryRepository.CreateAsync(history);
            }
            
            // Map to response DTO
            var keyDto = MapToDto(virtualKey);
            
            // Return response with the generated key
            return new CreateVirtualKeyResponseDto
            {
                VirtualKey = apiKey,
                KeyInfo = keyDto
            };
        }
        
        /// <inheritdoc />
        public async Task<VirtualKeyDto?> GetVirtualKeyInfoAsync(int id)
        {
            _logger.LogInformation("Getting virtual key info for ID: {KeyId}", id);
            
            var key = await _virtualKeyRepository.GetByIdAsync(id);
            if (key == null)
            {
                _logger.LogWarning("Virtual key with ID {KeyId} not found", id);
                return null;
            }
            
            return MapToDto(key);
        }
        
        /// <inheritdoc />
        public async Task<List<VirtualKeyDto>> ListVirtualKeysAsync()
        {
            _logger.LogInformation("Listing all virtual keys");
            
            var keys = await _virtualKeyRepository.GetAllAsync();
            
            return keys.ConvertAll(MapToDto);
        }
        
        /// <inheritdoc />
        public async Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto request)
        {
            _logger.LogInformation("Updating virtual key with ID: {KeyId}", id);
            
            var key = await _virtualKeyRepository.GetByIdAsync(id);
            if (key == null)
            {
                _logger.LogWarning("Virtual key with ID {KeyId} not found", id);
                return false;
            }
            
            // Update properties
            if (request.KeyName != null)
                key.KeyName = request.KeyName;
                
            if (request.AllowedModels != null)
                key.AllowedModels = request.AllowedModels;
                
            if (request.MaxBudget.HasValue)
                key.MaxBudget = request.MaxBudget;
                
            if (request.BudgetDuration != null)
                key.BudgetDuration = request.BudgetDuration;
                
            if (request.IsEnabled.HasValue)
                key.IsEnabled = request.IsEnabled.Value;
                
            if (request.ExpiresAt.HasValue)
                key.ExpiresAt = request.ExpiresAt;
                
            if (request.Metadata != null)
                key.Metadata = request.Metadata;
                
            if (request.RateLimitRpm.HasValue)
                key.RateLimitRpm = request.RateLimitRpm;
                
            if (request.RateLimitRpd.HasValue)
                key.RateLimitRpd = request.RateLimitRpd;
            
            key.UpdatedAt = DateTime.UtcNow;
            
            // Save changes
            var result = await _virtualKeyRepository.UpdateAsync(key);
            
            return result;
        }
        
        /// <inheritdoc />
        public async Task<bool> DeleteVirtualKeyAsync(int id)
        {
            _logger.LogInformation("Deleting virtual key with ID: {KeyId}", id);
            
            var key = await _virtualKeyRepository.GetByIdAsync(id);
            if (key == null)
            {
                _logger.LogWarning("Virtual key with ID {KeyId} not found", id);
                return false;
            }
            
            return await _virtualKeyRepository.DeleteAsync(id);
        }
        
        /// <inheritdoc />
        public async Task<bool> ResetSpendAsync(int id)
        {
            _logger.LogInformation("Resetting spend for virtual key with ID: {KeyId}", id);
            
            var key = await _virtualKeyRepository.GetByIdAsync(id);
            if (key == null)
            {
                _logger.LogWarning("Virtual key with ID {KeyId} not found", id);
                return false;
            }
            
            // Reset spend amount
            key.CurrentSpend = 0;
            var updated = await _virtualKeyRepository.UpdateAsync(key);
            
            if (!updated)
            {
                return false;
            }
            
            // Add history entry
            if (key.MaxBudget.HasValue)
            {
                var history = new VirtualKeySpendHistory
                {
                    VirtualKeyId = key.Id,
                    Amount = 0,
                    Date = DateTime.UtcNow,
                    Timestamp = DateTime.UtcNow
                };
                
                await _spendHistoryRepository.CreateAsync(history);
            }
            
            return true;
        }
        
        /// <summary>
        /// Maps a VirtualKey entity to a VirtualKeyDto
        /// </summary>
        /// <param name="key">The entity to map</param>
        /// <returns>The mapped DTO</returns>
        private static VirtualKeyDto MapToDto(VirtualKey key)
        {
            return new VirtualKeyDto
            {
                Id = key.Id,
                KeyName = key.KeyName,
                KeyPrefix = GenerateKeyPrefix(key.KeyHash),
                AllowedModels = key.AllowedModels,
                MaxBudget = key.MaxBudget,
                CurrentSpend = key.CurrentSpend,
                BudgetDuration = key.BudgetDuration,
                BudgetStartDate = key.BudgetStartDate,
                IsEnabled = key.IsEnabled,
                ExpiresAt = key.ExpiresAt,
                CreatedAt = key.CreatedAt,
                UpdatedAt = key.UpdatedAt,
                Metadata = key.Metadata,
                RateLimitRpm = key.RateLimitRpm,
                RateLimitRpd = key.RateLimitRpd
            };
        }
        
        /// <summary>
        /// Generates a key prefix for display purposes
        /// </summary>
        /// <param name="keyHash">The key hash</param>
        /// <returns>A prefix showing part of the key</returns>
        private static string GenerateKeyPrefix(string keyHash)
        {
            // Generate a prefix like "condt_abc123..." from the hash
            // This is for display purposes only
            var shortPrefix = keyHash.Substring(0, 6).ToLower();
            return $"condt_{shortPrefix}...";
        }
        
        /// <summary>
        /// Computes a SHA256 hash of the input string
        /// </summary>
        /// <param name="input">The input to hash</param>
        /// <returns>The hash as a hexadecimal string</returns>
        private static string ComputeSha256Hash(string input)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            
            var builder = new StringBuilder();
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            
            return builder.ToString();
        }
    }
}