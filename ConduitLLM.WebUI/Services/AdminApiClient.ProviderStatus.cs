using ConduitLLM.WebUI.Models;
using System.Text.Json;

namespace ConduitLLM.WebUI.Services
{
    public partial class AdminApiClient
    {
        /// <inheritdoc />
        public async Task<Dictionary<string, ProviderStatus>> CheckAllProvidersStatusAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/providerhealth/status");
                response.EnsureSuccessStatusCode();
                
                var result = await response.Content.ReadFromJsonAsync<Dictionary<string, ProviderStatus>>(_jsonOptions);
                return result ?? new Dictionary<string, ProviderStatus>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking all providers status");
                return new Dictionary<string, ProviderStatus>();
            }
        }
        
        /// <inheritdoc />
        public async Task<ProviderStatus> CheckProviderStatusAsync(string providerName)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/providerhealth/status/{Uri.EscapeDataString(providerName)}");
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new ProviderStatus { Status = ProviderStatus.StatusType.Offline, StatusMessage = "Provider not found" };
                }
                
                response.EnsureSuccessStatusCode();
                
                var result = await response.Content.ReadFromJsonAsync<ProviderStatus>(_jsonOptions);
                return result ?? new ProviderStatus { Status = ProviderStatus.StatusType.Unknown, StatusMessage = "Unknown error" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking provider status for {ProviderName}", providerName);
                return new ProviderStatus { Status = ProviderStatus.StatusType.Offline, StatusMessage = ex.Message };
            }
        }
    }
}