using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Configuration;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.OpenAI
{
    /// <summary>
    /// OpenAIClient partial class containing authentication verification functionality.
    /// </summary>
    public partial class OpenAIClient
    {
        /// <summary>
        /// Verifies authentication with OpenAI or Azure OpenAI.
        /// </summary>
        public override async Task<AuthenticationResult> VerifyAuthenticationAsync(
            string? apiKey = null,
            string? baseUrl = null,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : PrimaryKeyCredential.ApiKey;

            if (string.IsNullOrWhiteSpace(effectiveApiKey))
            {
                return AuthenticationResult.Failure(
                    "API key is required",
                    "No API key provided for OpenAI authentication");
            }

            try
            {
                using var client = CreateHttpClient(effectiveApiKey);

                // Override base URL if provided
                if (!string.IsNullOrWhiteSpace(baseUrl))
                {
                    client.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
                }

                string endpoint;
                var effectiveBaseUrl = client.BaseAddress?.ToString() ?? GetDefaultBaseUrl();
                if (_isAzure)
                {
                    // For Azure, test with deployments endpoint
                    var url = UrlBuilder.Combine(effectiveBaseUrl, "openai", "deployments");
                    endpoint = UrlBuilder.AppendQueryString(url, ("api-version", AzureApiVersion));
                }
                else
                {
                    // For OpenAI, test with models endpoint
                    endpoint = UrlBuilder.Combine(effectiveBaseUrl, Constants.Endpoints.Models);
                }

                Logger.LogDebug("Testing authentication with endpoint: {Endpoint}", endpoint);

                using var response = await client.GetAsync(endpoint, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                Logger.LogInformation("{Provider} auth check returned status {StatusCode}", ProviderName, response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    return AuthenticationResult.Success(
                        $"Connected successfully to {ProviderName}",
                        responseTime);
                }

                // Handle specific error cases
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Logger.LogWarning("{Provider} authentication failed: {Response}", ProviderName, responseContent);
                    return AuthenticationResult.Failure(
                        "Authentication failed",
                        $"Invalid API key for {ProviderName}");
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return AuthenticationResult.Failure(
                        "Access forbidden",
                        $"API key does not have sufficient permissions for {ProviderName}");
                }

                return AuthenticationResult.Failure(
                    $"Unexpected response: {response.StatusCode}",
                    responseContent);
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "Network error verifying {Provider} authentication", ProviderName);
                return AuthenticationResult.Failure(
                    $"Network error: {ex.Message}",
                    ex.ToString());
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                Logger.LogError(ex, "Timeout verifying {Provider} authentication", ProviderName);
                return AuthenticationResult.Failure(
                    "Request timeout",
                    "Authentication request timed out");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error verifying {Provider} authentication", ProviderName);
                return AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <summary>
        /// Gets the health check URL for OpenAI or Azure OpenAI.
        /// </summary>
        public override string GetHealthCheckUrl(string? baseUrl = null)
        {
            // Resolve through the registry so structured settings (Azure's {resource_name}) are
            // substituted; a raw Provider.BaseUrl read would leave the placeholder in the URL.
            var effectiveBaseUrl = !string.IsNullOrWhiteSpace(baseUrl)
                ? baseUrl.TrimEnd('/')
                : ProviderConfigurationRegistry.ResolveBaseUrl(Provider);

            if (_isAzure)
            {
                var url = UrlBuilder.Combine(effectiveBaseUrl, "openai", "deployments");
                return UrlBuilder.AppendQueryString(url, ("api-version", AzureApiVersion));
            }

            return UrlBuilder.Combine(effectiveBaseUrl, Constants.Endpoints.Models);
        }

        /// <summary>
        /// Gets the default base URL for the configured provider type from the registry.
        /// </summary>
        protected override string GetDefaultBaseUrl()
        {
            return ProviderConfigurationRegistry.GetDefaultBaseUrl(Provider.ProviderType)
                ?? ProviderConfigurationRegistry.GetDefaultBaseUrl(ProviderType.OpenAI)!;
        }
    }
}
