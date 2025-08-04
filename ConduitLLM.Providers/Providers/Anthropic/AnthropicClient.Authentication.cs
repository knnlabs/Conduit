using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Helpers;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.Anthropic
{
    /// <summary>
    /// AnthropicClient partial class containing authentication verification functionality.
    /// </summary>
    public partial class AnthropicClient
    {
        /// <summary>
        /// Verifies Anthropic authentication by making a test request to the messages endpoint.
        /// </summary>
        public override async Task<Core.Interfaces.AuthenticationResult> VerifyAuthenticationAsync(
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
                    return Core.Interfaces.AuthenticationResult.Failure(
                        "API key is required",
                        "No API key provided for Anthropic authentication");
                }

                // Create a test client with Anthropic-specific headers
                using var client = CreateHttpClient(effectiveApiKey);
                
                // Remove default Bearer auth and add Anthropic headers
                client.DefaultRequestHeaders.Authorization = null;
                client.DefaultRequestHeaders.Add("x-api-key", effectiveApiKey);
                client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                
                // Anthropic doesn't have a models endpoint, so we'll make a minimal messages request
                // that will fail immediately if auth is invalid
                var testRequest = new
                {
                    model = "claude-3-haiku-20240307", // Use the cheapest model
                    messages = new[]
                    {
                        new { role = "user", content = "Hi" }
                    },
                    max_tokens = 1, // Minimal tokens to reduce cost
                    // Add an invalid parameter to make the request fail after auth check
                    temperature = 2.0 // Invalid temperature (max is 1.0)
                };

                var json = JsonSerializer.Serialize(testRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var messagesUrl = UrlBuilder.Combine(GetHealthCheckUrl(baseUrl), "messages");
                var response = await client.PostAsync(messagesUrl, content, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                Logger.LogInformation("Anthropic auth check returned status {StatusCode}", response.StatusCode);

                // Check for authentication-specific errors
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    Logger.LogWarning("Anthropic authentication failed: {Response}", responseContent);
                    return Core.Interfaces.AuthenticationResult.Failure(
                        "Authentication failed",
                        "Invalid API key - Anthropic requires a valid x-api-key header");
                }

                // If we get a BadRequest (likely due to invalid temperature), auth is valid
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    // Check if it's a parameter validation error (which means auth passed)
                    if (responseContent.Contains("temperature") || responseContent.Contains("invalid_request_error"))
                    {
                        Logger.LogInformation("Anthropic authentication successful (request failed on validation as expected)");
                        return Core.Interfaces.AuthenticationResult.Success(
                            "Connected successfully to Anthropic API",
                            responseTime);
                    }
                }

                // Any other 2xx response also indicates valid auth
                if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
                {
                    return Core.Interfaces.AuthenticationResult.Success(
                        "Connected successfully to Anthropic API",
                        responseTime);
                }

                // For any other status, log and consider auth invalid
                Logger.LogWarning("Unexpected response from Anthropic: {StatusCode}", response.StatusCode);
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Unexpected response: {response.StatusCode}",
                    await response.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error verifying Anthropic authentication");
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <summary>
        /// Gets the health check URL for Anthropic.
        /// </summary>
        public override string GetHealthCheckUrl(string? baseUrl = null)
        {
            var effectiveBaseUrl = !string.IsNullOrWhiteSpace(baseUrl) 
                ? baseUrl.TrimEnd('/') 
                : (!string.IsNullOrWhiteSpace(Provider.BaseUrl) 
                    ? Provider.BaseUrl.TrimEnd('/') 
                    : Constants.Urls.DefaultBaseUrl.TrimEnd('/'));
            
            // Ensure /v1 is in the URL
            effectiveBaseUrl = UrlBuilder.EnsureSegment(effectiveBaseUrl, "v1");
            
            // Anthropic doesn't have a models endpoint, return base URL
            return effectiveBaseUrl;
        }
    }
}