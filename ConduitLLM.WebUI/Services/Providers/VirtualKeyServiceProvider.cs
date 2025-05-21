using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services.Providers
{
    /// <summary>
    /// Implementation of IVirtualKeyService that uses IAdminApiClient to interact with the Admin API.
    /// </summary>
    public class VirtualKeyServiceProvider : IVirtualKeyService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<VirtualKeyServiceProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualKeyServiceProvider"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public VirtualKeyServiceProvider(
            IAdminApiClient adminApiClient,
            ILogger<VirtualKeyServiceProvider> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<bool> DeleteVirtualKeyAsync(int id)
        {
            try
            {
                return await _adminApiClient.DeleteVirtualKeyAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting virtual key with ID {VirtualKeyId}", id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<CreateVirtualKeyResponseDto> GenerateVirtualKeyAsync(CreateVirtualKeyRequestDto request)
        {
            try
            {
                var response = await _adminApiClient.CreateVirtualKeyAsync(request);
                if (response == null)
                {
                    throw new InvalidOperationException("Failed to create virtual key");
                }
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating virtual key");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<VirtualKeyDto?> GetVirtualKeyInfoAsync(int id)
        {
            try
            {
                return await _adminApiClient.GetVirtualKeyByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving virtual key info for ID {VirtualKeyId}", id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<VirtualKeyValidationInfoDto?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _adminApiClient.GetVirtualKeyValidationInfoAsync(keyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving virtual key validation info for ID {VirtualKeyId}", keyId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<List<VirtualKeyDto>> ListVirtualKeysAsync()
        {
            try
            {
                var keys = await _adminApiClient.GetAllVirtualKeysAsync();
                return keys.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing virtual keys");
                return new List<VirtualKeyDto>();
            }
        }

        /// <inheritdoc />
        public async Task PerformMaintenanceAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _adminApiClient.PerformVirtualKeyMaintenanceAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing virtual key maintenance");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ResetBudgetIfExpiredAsync(int keyId, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _adminApiClient.CheckVirtualKeyBudgetAsync(keyId);
                return result?.WasReset ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking/resetting budget for virtual key with ID {VirtualKeyId}", keyId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ResetSpendAsync(int id)
        {
            try
            {
                return await _adminApiClient.ResetVirtualKeySpendAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting spend for virtual key with ID {VirtualKeyId}", id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateSpendAsync(int keyId, decimal cost)
        {
            try
            {
                return await _adminApiClient.UpdateVirtualKeySpendAsync(keyId, cost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating spend for virtual key with ID {VirtualKeyId}", keyId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto request)
        {
            try
            {
                return await _adminApiClient.UpdateVirtualKeyAsync(id, request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating virtual key with ID {VirtualKeyId}", id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<VirtualKeyValidationInfoDto?> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
        {
            try
            {
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
    }
}