using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Groq
{
    /// <summary>
    /// GroqClient partial class containing authentication methods.
    /// </summary>
    public partial class GroqClient
    {
        /// <summary>
        /// Verifies Groq authentication by making a test request to the models endpoint.
        /// </summary>
        public override async Task<ConduitLLM.Core.Interfaces.AuthenticationResult> VerifyAuthenticationAsync(
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
                    return ConduitLLM.Core.Interfaces.AuthenticationResult.Failure(
                        "API key is required",
                        "No API key provided for Groq authentication");
                }

                // Create a test client
                using var client = CreateHttpClient(effectiveApiKey);
                
                // Make a request to the models endpoint
                var modelsUrl = $"{GetHealthCheckUrl(baseUrl)}/models";
                var response = await client.GetAsync(modelsUrl, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                Logger.LogInformation("Groq auth check returned status {StatusCode}", response.StatusCode);

                // Check for authentication errors
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return ConduitLLM.Core.Interfaces.AuthenticationResult.Failure(
                        "Authentication failed",
                        "Invalid API key - Groq requires a valid API key");
                }
                
                if (response.IsSuccessStatusCode)
                {
                    return ConduitLLM.Core.Interfaces.AuthenticationResult.Success(
                        "Connected successfully to Groq API",
                        responseTime);
                }

                // Other errors
                return ConduitLLM.Core.Interfaces.AuthenticationResult.Failure(
                    $"Unexpected response: {response.StatusCode}",
                    await response.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error verifying Groq authentication");
                return ConduitLLM.Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <summary>
        /// Gets the health check URL for Groq.
        /// </summary>
        public override string GetHealthCheckUrl(string? baseUrl = null)
        {
            var effectiveBaseUrl = !string.IsNullOrWhiteSpace(baseUrl) 
                ? baseUrl.TrimEnd('/') 
                : (!string.IsNullOrWhiteSpace(Provider.BaseUrl) 
                    ? Provider.BaseUrl.TrimEnd('/') 
                    : Constants.Urls.DefaultBaseUrl.TrimEnd('/'));
            
            return effectiveBaseUrl;
        }
    }
}
