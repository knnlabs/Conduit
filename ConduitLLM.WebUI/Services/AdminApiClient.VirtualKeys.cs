using System.Net.Http.Json;
using ConduitLLM.Configuration.DTOs.VirtualKey;

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
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<VirtualKeyValidationResult>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating virtual key through Admin API");
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
                _logger.LogError(ex, "Error updating virtual key spend for key ID {KeyId} through Admin API", id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<BudgetCheckResult?> CheckVirtualKeyBudgetAsync(int id)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/virtualkeys/{id}/check-budget", new StringContent(string.Empty));

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<BudgetCheckResult>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking virtual key budget for key ID {KeyId} through Admin API", id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<VirtualKeyValidationInfoDto?> GetVirtualKeyValidationInfoAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/virtualkeys/{id}/validation-info");

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<VirtualKeyValidationInfoDto>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting virtual key validation info for key ID {KeyId} through Admin API", id);
                return null;
            }
        }
        
        /// <inheritdoc />
        public async Task PerformVirtualKeyMaintenanceAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync("api/virtualkeys/maintenance", new StringContent(string.Empty));
                response.EnsureSuccessStatusCode();
                
                _logger.LogInformation("Successfully triggered virtual key maintenance via Admin API");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing virtual key maintenance through Admin API");
                throw;
            }
        }
    }
}