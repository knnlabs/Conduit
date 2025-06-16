using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.WebUI.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that bridges the IVirtualKeyService interface with the Admin API client
    /// </summary>
    public class VirtualKeyServiceAdapter : IVirtualKeyService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<VirtualKeyServiceAdapter> _logger;

        public VirtualKeyServiceAdapter(IAdminApiClient adminApiClient, ILogger<VirtualKeyServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<CreateVirtualKeyResponseDto> GenerateVirtualKeyAsync(CreateVirtualKeyRequestDto request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var response = await _adminApiClient.CreateVirtualKeyAsync(request);
            if (response == null)
            {
                throw new InvalidOperationException("Failed to create virtual key");
            }

            return response;
        }

        /// <inheritdoc />
        public async Task<VirtualKeyDto?> GetVirtualKeyInfoAsync(int id)
        {
            return await _adminApiClient.GetVirtualKeyByIdAsync(id);
        }

        /// <inheritdoc />
        public async Task<List<VirtualKeyDto>> ListVirtualKeysAsync()
        {
            var keys = await _adminApiClient.GetAllVirtualKeysAsync();
            return keys?.ToList() ?? new List<VirtualKeyDto>();
        }

        /// <inheritdoc />
        public async Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            return await _adminApiClient.UpdateVirtualKeyAsync(id, request);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteVirtualKeyAsync(int id)
        {
            return await _adminApiClient.DeleteVirtualKeyAsync(id);
        }

        /// <inheritdoc />
        public async Task<bool> ResetSpendAsync(int id)
        {
            return await _adminApiClient.ResetVirtualKeySpendAsync(id);
        }

        /// <inheritdoc />
        public async Task<VirtualKeyValidationInfoDto?> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var validationResult = await _adminApiClient.ValidateVirtualKeyAsync(key, requestedModel);
            if (validationResult == null || !validationResult.IsValid)
            {
                return null;
            }

            // Map from VirtualKeyValidationResult to VirtualKeyValidationInfoDto
            return new VirtualKeyValidationInfoDto
            {
                Id = validationResult.VirtualKeyId ?? 0,
                KeyName = validationResult.KeyName ?? string.Empty,
                IsEnabled = validationResult.IsValid,
                MaxBudget = validationResult.MaxBudget,
                CurrentSpend = validationResult.CurrentSpend,
                AllowedModels = validationResult.AllowedModels
            };
        }

        /// <inheritdoc />
        public async Task<bool> UpdateSpendAsync(int keyId, decimal cost)
        {
            return await _adminApiClient.UpdateVirtualKeySpendAsync(keyId, cost);
        }

        /// <inheritdoc />
        public async Task<bool> ResetBudgetIfExpiredAsync(int keyId, CancellationToken cancellationToken = default)
        {
            var result = await _adminApiClient.CheckVirtualKeyBudgetAsync(keyId);
            return result?.WasReset ?? false;
        }

        /// <inheritdoc />
        public async Task<VirtualKeyValidationInfoDto?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default)
        {
            return await _adminApiClient.GetVirtualKeyValidationInfoAsync(keyId);
        }

        /// <inheritdoc />
        public async Task PerformMaintenanceAsync(CancellationToken cancellationToken = default)
        {
            await _adminApiClient.PerformVirtualKeyMaintenanceAsync();
        }

        /// <summary>
        /// Gets virtual key usage statistics for the specified key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key</param>
        /// <returns>Usage statistics for the key</returns>
        public async Task<IEnumerable<ConduitLLM.WebUI.DTOs.VirtualKeyCostDataDto>> GetVirtualKeyUsageStatisticsAsync(int virtualKeyId)
        {
            var stats = await _adminApiClient.GetVirtualKeyUsageStatisticsAsync(virtualKeyId);
            return stats ?? Enumerable.Empty<ConduitLLM.WebUI.DTOs.VirtualKeyCostDataDto>();
        }
    }
}