using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that implements <see cref="IVirtualKeyService"/> using the Admin API client.
    /// </summary>
    public class VirtualKeyServiceAdapter : IVirtualKeyService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<VirtualKeyServiceAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualKeyServiceAdapter"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public VirtualKeyServiceAdapter(
            IAdminApiClient adminApiClient,
            ILogger<VirtualKeyServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<List<Configuration.DTOs.VirtualKey.VirtualKeyDto>> ListVirtualKeysAsync()
        {
            var result = await _adminApiClient.GetAllVirtualKeysAsync();
            return new List<Configuration.DTOs.VirtualKey.VirtualKeyDto>(result);
        }

        /// <inheritdoc />
        public async Task<Configuration.DTOs.VirtualKey.VirtualKeyDto?> GetVirtualKeyInfoAsync(int id)
        {
            return await _adminApiClient.GetVirtualKeyByIdAsync(id);
        }

        /// <inheritdoc />
        public async Task<Configuration.DTOs.VirtualKey.CreateVirtualKeyResponseDto> GenerateVirtualKeyAsync(
            Configuration.DTOs.VirtualKey.CreateVirtualKeyRequestDto request)
        {
            var result = await _adminApiClient.CreateVirtualKeyAsync(request);
            if (result == null)
            {
                throw new InvalidOperationException("Failed to create virtual key");
            }
            return result;
        }

        /// <inheritdoc />
        public async Task<bool> UpdateVirtualKeyAsync(
            int id,
            Configuration.DTOs.VirtualKey.UpdateVirtualKeyRequestDto request)
        {
            return await _adminApiClient.UpdateVirtualKeyAsync(id, request);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteVirtualKeyAsync(int id)
        {
            return await _adminApiClient.DeleteVirtualKeyAsync(id);
        }

        /// <summary>
        /// Gets virtual key usage statistics for the specified virtual key ID.
        /// </summary>
        public async Task<IEnumerable<ConduitLLM.Configuration.DTOs.VirtualKeyCostDataDto>> GetVirtualKeyUsageStatisticsAsync(int? virtualKeyId = null)
        {
            var webUiDtos = await _adminApiClient.GetVirtualKeyUsageStatisticsAsync(virtualKeyId);
            
            // Convert WebUI DTOs to Configuration DTOs
            var result = new List<ConduitLLM.Configuration.DTOs.VirtualKeyCostDataDto>();
            
            foreach (var dto in webUiDtos)
            {
                result.Add(new ConduitLLM.Configuration.DTOs.VirtualKeyCostDataDto
                {
                    VirtualKeyId = dto.VirtualKeyId,
                    KeyName = dto.KeyName,
                    Cost = dto.Cost,
                    RequestCount = dto.RequestCount
                });
            }
            
            return result;
        }

        /// <inheritdoc />
        public async Task<bool> ResetSpendAsync(int id)
        {
            try
            {
                _logger.LogInformation("Resetting spend for virtual key {KeyId}", id);
                return await _adminApiClient.ResetVirtualKeySpendAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting spend for virtual key {KeyId}", id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<VirtualKeyValidationInfoDto?> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
        {
            try
            {
                _logger.LogInformation("Validating virtual key for model {Model}", requestedModel ?? "any");
                
                // Call the Admin API endpoint with string parameters
                var validationResult = await _adminApiClient.ValidateVirtualKeyAsync(key, requestedModel);
                
                if (validationResult == null || !validationResult.IsValid)
                {
                    return null;
                }
                
                // If validation succeeds, get the full validation info
                if (validationResult.VirtualKeyId.HasValue)
                {
                    return await _adminApiClient.GetVirtualKeyValidationInfoAsync(validationResult.VirtualKeyId.Value);
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating virtual key");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateSpendAsync(int keyId, decimal cost)
        {
            try
            {
                _logger.LogInformation("Updating spend for virtual key {KeyId} by {Cost}", keyId, cost);
                
                if (cost <= 0)
                {
                    return true; // No cost to add, consider it successful
                }
                
                return await _adminApiClient.UpdateVirtualKeySpendAsync(keyId, cost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating spend for virtual key {KeyId}", keyId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ResetBudgetIfExpiredAsync(int keyId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Checking if budget expired for virtual key {KeyId}", keyId);
                
                var result = await _adminApiClient.CheckVirtualKeyBudgetAsync(keyId);
                return result?.WasReset ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking budget expiration for virtual key {KeyId}", keyId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<VirtualKeyValidationInfoDto?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting virtual key {KeyId} for validation", keyId);
                
                return await _adminApiClient.GetVirtualKeyValidationInfoAsync(keyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting virtual key {KeyId} for validation", keyId);
                return null;
            }
        }
        
        /// <inheritdoc />
        public async Task PerformMaintenanceAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Performing virtual key maintenance via Admin API");
                
                // Call the Admin API endpoint for performing maintenance
                await _adminApiClient.PerformVirtualKeyMaintenanceAsync();
                
                _logger.LogInformation("Virtual key maintenance completed successfully via Admin API");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing virtual key maintenance via Admin API");
                throw;
            }
        }
    }
}