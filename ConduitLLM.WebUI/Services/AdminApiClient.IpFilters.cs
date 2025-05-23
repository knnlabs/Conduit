using ConduitLLM.Configuration.DTOs.IpFilter;
using System.Text.Json;

namespace ConduitLLM.WebUI.Services
{
    public partial class AdminApiClient
    {
        /// <inheritdoc />
        public async Task<IpCheckResult?> CheckIpAddressAsync(string ipAddress)
        {
            try
            {
                // Use singular "ipfilter" to match the controller's route
                var response = await _httpClient.GetAsync($"api/ipfilter/check/{Uri.EscapeDataString(ipAddress)}");
                
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                
                return await response.Content.ReadFromJsonAsync<IpCheckResult>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking IP address {IpAddress}", ipAddress);
                return null;
            }
        }
    }
}