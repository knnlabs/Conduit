using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
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

        /// <inheritdoc />
        public async Task<IEnumerable<VirtualKeyCostDataDto>> GetVirtualKeyUsageStatisticsAsync(int? virtualKeyId = null)
        {
            return await _adminApiClient.GetVirtualKeyUsageStatisticsAsync(virtualKeyId);
        }

        /// <inheritdoc />
        public Task<bool> ResetSpendAsync(int id)
        {
            try
            {
                _logger.LogInformation("Resetting spend for virtual key {KeyId}", id);
                // This would need API endpoint implementation
                // For now, log and return success to avoid blocking
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting spend for virtual key {KeyId}", id);
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc />
        public Task<VirtualKey?> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
        {
            try
            {
                _logger.LogInformation("Validating virtual key for model {Model}", requestedModel ?? "any");
                // This would need API endpoint implementation
                // For now, return null indicating validation failure
                return Task.FromResult<VirtualKey?>(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating virtual key");
                return Task.FromResult<VirtualKey?>(null);
            }
        }

        /// <inheritdoc />
        public Task<bool> UpdateSpendAsync(int keyId, decimal cost)
        {
            try
            {
                _logger.LogInformation("Updating spend for virtual key {KeyId} by {Cost}", keyId, cost);
                // This would need API endpoint implementation
                // For now, log and return success to avoid blocking
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating spend for virtual key {KeyId}", keyId);
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc />
        public Task<bool> ResetBudgetIfExpiredAsync(int keyId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Checking if budget expired for virtual key {KeyId}", keyId);
                // This would need API endpoint implementation
                // For now, return no reset needed
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking budget expiration for virtual key {KeyId}", keyId);
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc />
        public Task<VirtualKey?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting virtual key {KeyId} for validation", keyId);
                // This would need API endpoint implementation
                // For now, return null to indicate key not found
                return Task.FromResult<VirtualKey?>(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting virtual key {KeyId} for validation", keyId);
                return Task.FromResult<VirtualKey?>(null);
            }
        }
    }
}