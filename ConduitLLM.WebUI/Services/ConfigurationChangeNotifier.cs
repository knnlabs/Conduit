using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Service to notify the Conduit HTTP proxy server about configuration changes
/// </summary>
public class ConfigurationChangeNotifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ConfigurationChangeNotifier> _logger;
    
    public ConfigurationChangeNotifier(
        IHttpClientFactory httpClientFactory,
        ILogger<ConfigurationChangeNotifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }
    
    /// <summary>
    /// Notifies the Conduit HTTP proxy server that configuration has changed
    /// </summary>
    /// <param name="proxyBaseUrl">The base URL for the Conduit proxy server</param>
    /// <returns>True if the notification was successful, false otherwise</returns>
    public async Task<bool> NotifyConfigurationChangedAsync(string proxyBaseUrl)
    {
        try
        {
            _logger.LogInformation("Notifying Conduit proxy server about configuration changes...");
            
            // Create a clean URL without trailing slash
            var baseUrl = proxyBaseUrl.TrimEnd('/');
            var refreshUrl = $"{baseUrl}/admin/refresh-configuration";
            
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.PostAsync(refreshUrl, null);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully notified Conduit proxy server about configuration changes");
                return true;
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to notify Conduit proxy server. Status code: {StatusCode}, Message: {Message}",
                    response.StatusCode, content);
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error connecting to Conduit proxy server to notify about configuration changes");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while notifying Conduit proxy server about configuration changes");
            return false;
        }
    }
}
