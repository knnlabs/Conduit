using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.WebUI.Interfaces;

using Microsoft.Extensions.Logging;

using BudgetCheckResult = ConduitLLM.Configuration.DTOs.VirtualKey.BudgetCheckResult;
using ConfigVKDto = ConduitLLM.Configuration.DTOs.VirtualKey;
using UpdateSpendRequest = ConduitLLM.Configuration.DTOs.VirtualKey.UpdateSpendRequest;
using ValidateVirtualKeyRequest = ConduitLLM.Configuration.DTOs.VirtualKey.ValidateVirtualKeyRequest;
using VirtualKeyValidationInfoDto = ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyValidationInfoDto;
using VirtualKeyValidationResult = ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyValidationResult;

namespace ConduitLLM.WebUI.Services
{
    public partial class AdminApiClient : IVirtualKeyService
    {
        #region IAdminApiClient Methods

        /// <inheritdoc />
        public async Task<VirtualKeyValidationResult?> GetVirtualKeyValidationResultAsync(string key, string? requestedModel = null)
        {
            try
            {
                var request = new ValidateVirtualKeyRequest
                {
                    Key = key,
                    RequestedModel = requestedModel
                };

                var response = await _httpClient.PostAsJsonAsync("api/virtualkeys/validate", request);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<VirtualKeyValidationResult>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating virtual key");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateVirtualKeySpendAsync(int id, decimal cost)
        {
            try
            {
                var request = new UpdateSpendRequest
                {
                    Cost = cost
                };

                var response = await _httpClient.PostAsJsonAsync($"api/virtualkeys/{id}/spend", request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating virtual key spend for ID {VirtualKeyId}", id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<BudgetCheckResult?> CheckVirtualKeyBudgetAsync(int id)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/virtualkeys/{id}/check-budget", new StringContent(string.Empty, Encoding.UTF8, "application/json"));
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<BudgetCheckResult>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking budget for virtual key with ID {VirtualKeyId}", id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<VirtualKeyValidationInfoDto?> GetVirtualKeyValidationInfoAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/virtualkeys/{id}/validation-info");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<VirtualKeyValidationInfoDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving validation info for virtual key with ID {VirtualKeyId}", id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task PerformVirtualKeyMaintenanceAsync()
        {
            try
            {
                await _httpClient.PostAsync("api/virtualkeys/maintenance", new StringContent(string.Empty, Encoding.UTF8, "application/json"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing virtual key maintenance");
            }
        }

        #endregion

        #region IVirtualKeyService Implementation

        /// <inheritdoc />
        async Task<ConfigVKDto.CreateVirtualKeyResponseDto> IVirtualKeyService.GenerateVirtualKeyAsync(ConfigVKDto.CreateVirtualKeyRequestDto request)
        {
            var response = await CreateVirtualKeyAsync(request);

            if (response == null)
            {
                _logger.LogError("Failed to create virtual key with name: {Name}", request.KeyName);
                throw new InvalidOperationException($"Failed to create virtual key: {request.KeyName}");
            }

            return response;
        }

        /// <inheritdoc />
        async Task<ConfigVKDto.VirtualKeyDto?> IVirtualKeyService.GetVirtualKeyInfoAsync(int id)
        {
            return await GetVirtualKeyByIdAsync(id);
        }

        /// <inheritdoc />
        async Task<List<ConfigVKDto.VirtualKeyDto>> IVirtualKeyService.ListVirtualKeysAsync()
        {
            var keys = await GetAllVirtualKeysAsync();
            return new List<ConfigVKDto.VirtualKeyDto>(keys);
        }

        /// <inheritdoc />
        async Task<bool> IVirtualKeyService.UpdateVirtualKeyAsync(int id, ConfigVKDto.UpdateVirtualKeyRequestDto request)
        {
            // Call the AdminApiClient method
            return await UpdateVirtualKeyAsync(id, request);
        }

        /// <inheritdoc />
        async Task<bool> IVirtualKeyService.DeleteVirtualKeyAsync(int id)
        {
            // Call the AdminApiClient method
            return await DeleteVirtualKeyAsync(id);
        }

        /// <inheritdoc />
        public async Task<bool> ResetSpendAsync(int id)
        {
            return await ResetVirtualKeySpendAsync(id);
        }

        /// <inheritdoc />
        async Task<VirtualKeyValidationInfoDto?> IVirtualKeyService.ValidateVirtualKeyAsync(string key, string? requestedModel)
        {
            try
            {
                var result = await GetVirtualKeyValidationResultAsync(key, requestedModel);

                if (result == null)
                {
                    return null;
                }

                // Create VirtualKeyValidationInfoDto from the validation result
                return new VirtualKeyValidationInfoDto
                {
                    Id = result.VirtualKeyId ?? 0,
                    KeyName = result.KeyName ?? string.Empty,
                    AllowedModels = result.AllowedModels,
                    MaxBudget = result.MaxBudget,
                    CurrentSpend = result.CurrentSpend
                    // Other properties would be set from more complete validation result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating virtual key");
                return null;
            }
        }

        /// <inheritdoc />
        async Task<bool> IVirtualKeyService.UpdateSpendAsync(int keyId, decimal cost)
        {
            return await UpdateVirtualKeySpendAsync(keyId, cost);
        }

        /// <inheritdoc />
        public async Task<bool> ResetBudgetIfExpiredAsync(int keyId, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await CheckVirtualKeyBudgetAsync(keyId);
                return result != null && result.WasReset;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking budget for virtual key {KeyId}", keyId);
                return false;
            }
        }

        /// <inheritdoc />
        async Task<VirtualKeyValidationInfoDto?> IVirtualKeyService.GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken)
        {
            return await GetVirtualKeyValidationInfoAsync(keyId);
        }

        /// <inheritdoc />
        async Task IVirtualKeyService.PerformMaintenanceAsync(CancellationToken cancellationToken)
        {
            await PerformVirtualKeyMaintenanceAsync();
        }

        #endregion
    }
}
