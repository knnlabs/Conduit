using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.WebUI.DTOs;
using System.Text.Json;
using System.Text;

namespace ConduitLLM.WebUI.Services
{
    public partial class AdminApiClient
    {
        /// <inheritdoc />
        public async Task<VirtualKeyValidationResult?> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
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
    }
}