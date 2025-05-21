using System.Net.Http.Json;
using System.Text.Json;

namespace ConduitLLM.WebUI.Services
{
    public partial class AdminApiClient
    {
        /// <inheritdoc />
        public async Task<string> GetSettingAsync(string key)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/globalsettings/{Uri.EscapeDataString(key)}");
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                // The content is a JSON string, so we need to deserialize it
                return JsonSerializer.Deserialize<string>(content, _jsonOptions) ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting setting {Key} from Admin API", key);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SetSettingAsync(string key, string value)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"/api/globalsettings", new { Key = key, Value = value });
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting {Key} to {Value} via Admin API", key, value);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> InitializeHttpTimeoutConfigurationAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/globalsettings/initialize/timeout", null);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<bool>(content, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing HTTP timeout configuration via Admin API");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> InitializeHttpRetryConfigurationAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/globalsettings/initialize/retry", null);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<bool>(content, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing HTTP retry configuration via Admin API");
                return false;
            }
        }
    }
}