using ConduitLLM.Configuration.DTOs.IpFilter;

namespace ConduitLLM.WebUI.Services
{
    public partial class AdminApiClient
    {
        /// <inheritdoc />
        public async Task<IpCheckResult?> CheckIpAddressAsync(string ipAddress)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ipAddress))
                {
                    return new IpCheckResult { IsAllowed = false, DeniedReason = "IP address cannot be empty" };
                }
                
                var response = await _httpClient.GetAsync($"api/ipfilters/check/{Uri.EscapeDataString(ipAddress)}");
                response.EnsureSuccessStatusCode();
                
                return await response.Content.ReadFromJsonAsync<IpCheckResult>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking IP address {IpAddress} through Admin API", ipAddress);
                
                // In case of error, default to allowing the IP to prevent blocking legitimate requests
                // This is a fail-open approach for this specific function
                return new IpCheckResult 
                { 
                    IsAllowed = true, 
                    DeniedReason = "Error checking IP, defaulting to allowed" 
                };
            }
        }
    }
}