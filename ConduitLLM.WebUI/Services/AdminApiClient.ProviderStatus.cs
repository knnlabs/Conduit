using System.Net.Http.Json;
using ConduitLLM.WebUI.Models;

namespace ConduitLLM.WebUI.Services
{
    public partial class AdminApiClient
    {
        /// <inheritdoc />
        public async Task<Dictionary<string, ProviderStatus>> CheckAllProvidersStatusAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/providerhealth/status/all");
                response.EnsureSuccessStatusCode();
                
                var result = await response.Content.ReadFromJsonAsync<Dictionary<string, ProviderStatus>>(_jsonOptions);
                return result ?? new Dictionary<string, ProviderStatus>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking status of all providers via Admin API");
                return new Dictionary<string, ProviderStatus>();
            }
        }

        /// <inheritdoc />
        public async Task<ProviderStatus> CheckProviderStatusAsync(string providerName)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/providerhealth/status/{Uri.EscapeDataString(providerName)}");
                response.EnsureSuccessStatusCode();
                
                var result = await response.Content.ReadFromJsonAsync<ProviderStatus>(_jsonOptions);
                return result ?? new ProviderStatus
                {
                    Status = ProviderStatus.StatusType.Unknown,
                    StatusMessage = "Error deserializing response from API"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking status of provider {ProviderName} via Admin API", providerName);
                return new ProviderStatus
                {
                    Status = ProviderStatus.StatusType.Unknown,
                    StatusMessage = $"Error: {ex.Message}",
                    LastCheckedUtc = DateTime.UtcNow
                };
            }
        }
    }
}