using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Providers.Configuration;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Replicate
{
    /// <summary>
    /// ReplicateClient partial class containing authentication verification functionality.
    /// </summary>
    public partial class ReplicateClient
    {
        /// <summary>
        /// Verifies Replicate authentication by making a test request to the account endpoint.
        /// </summary>
        public override async Task<AuthenticationResult> VerifyAuthenticationAsync(
            string? apiKey = null,
            string? baseUrl = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : PrimaryKeyCredential.ApiKey;

                if (string.IsNullOrWhiteSpace(effectiveApiKey))
                {
                    return AuthenticationResult.Failure(
                        "API key is required",
                        "No API token provided for Replicate authentication");
                }

                // Create a test client
                using var client = CreateHttpClient(effectiveApiKey);

                // Make a request to the account endpoint
                var accountUrl = GetHealthCheckUrl(baseUrl);
                using var response = await client.GetAsync(accountUrl, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                Logger.LogInformation("Replicate auth check returned status {StatusCode}", response.StatusCode);

                // Check for authentication errors
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return AuthenticationResult.Failure(
                        "Authentication failed",
                        ProviderConfigurationRegistry.GetErrorMessages(ProviderType.Replicate).InvalidApiKey);
                }

                if (response.IsSuccessStatusCode)
                {
                    return AuthenticationResult.Success(
                        "Connected successfully to Replicate API",
                        responseTime);
                }

                // Other errors
                return AuthenticationResult.Failure(
                    $"Unexpected response: {response.StatusCode}",
                    await response.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error verifying Replicate authentication");
                return AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <summary>
        /// Gets the health check URL for Replicate.
        /// Replicate uses the /account endpoint for authentication verification.
        /// </summary>
        public override string GetHealthCheckUrl(string? baseUrl = null)
        {
            var defaultBaseUrl = ProviderConfigurationRegistry.GetDefaultBaseUrl(ProviderType.Replicate)
                ?? "https://api.replicate.com/v1";

            var effectiveBaseUrl = !string.IsNullOrWhiteSpace(baseUrl)
                ? baseUrl.TrimEnd('/')
                : (Provider.BaseUrl ?? defaultBaseUrl).TrimEnd('/');

            // Ensure v1 is in the URL
            if (!effectiveBaseUrl.EndsWith("/v1"))
            {
                effectiveBaseUrl = $"{effectiveBaseUrl}/v1";
            }

            // Use the health check endpoint from registry (/account for Replicate)
            var healthCheckEndpoint = ProviderConfigurationRegistry.GetHealthCheckEndpoint(ProviderType.Replicate);
            return $"{effectiveBaseUrl}{healthCheckEndpoint}";
        }
    }
}
