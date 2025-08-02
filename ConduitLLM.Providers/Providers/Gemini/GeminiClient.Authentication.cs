using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Providers.Helpers;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.Gemini
{
    /// <summary>
    /// GeminiClient partial class containing authentication functionality.
    /// </summary>
    public partial class GeminiClient
    {
        /// <summary>
        /// Verifies Gemini authentication by making a test request to list models.
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
                        "No API key provided for Gemini authentication");
                }

                // Create a test client
                using var client = CreateHttpClient(effectiveApiKey);
                
                // Gemini uses API key as a query parameter, not in headers
                // Remove any authorization header that might have been set
                client.DefaultRequestHeaders.Authorization = null;
                
                // Make a request to list models endpoint
                var modelsUrl = UrlBuilder.Combine(GetHealthCheckUrl(baseUrl), "models");
                modelsUrl = UrlBuilder.AppendQueryString(modelsUrl, ("key", effectiveApiKey));
                var response = await client.GetAsync(modelsUrl, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                Logger.LogInformation("Gemini auth check returned status {StatusCode}", response.StatusCode);

                // Check for authentication errors
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return AuthenticationResult.Failure(
                        "Authentication failed",
                        "Invalid API key for Google Gemini");
                }

                if (response.IsSuccessStatusCode)
                {
                    // Parse response to verify we got actual model data
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    try
                    {
                        using var doc = JsonDocument.Parse(content);
                        var root = doc.RootElement;
                        
                        // Check if we have models array
                        if (root.TryGetProperty("models", out var modelsElement) && 
                            modelsElement.GetArrayLength() > 0)
                        {
                            Logger.LogInformation("Gemini authentication successful - found {Count} models", 
                                modelsElement.GetArrayLength());
                            return AuthenticationResult.Success(
                                "Connected successfully to Google Gemini API",
                                responseTime);
                        }
                        
                        // If no models returned, auth might still be invalid
                        return AuthenticationResult.Failure(
                            "Authentication verification inconclusive",
                            "No models returned - API key may be invalid or have no permissions");
                    }
                    catch (JsonException)
                    {
                        Logger.LogWarning("Failed to parse Gemini models response");
                        return AuthenticationResult.Failure(
                            "Invalid response format",
                            "Could not parse models response");
                    }
                }

                // Other errors
                return AuthenticationResult.Failure(
                    $"Unexpected response: {response.StatusCode}",
                    await response.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error verifying Gemini authentication");
                return AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <summary>
        /// Gets the health check URL for Gemini.
        /// </summary>
        public override string GetHealthCheckUrl(string? baseUrl = null)
        {
            var effectiveBaseUrl = !string.IsNullOrWhiteSpace(baseUrl) 
                ? baseUrl.TrimEnd('/') 
                : (Provider.BaseUrl ?? DefaultBaseUrl).TrimEnd('/');
            
            // Ensure the API version is in the URL
            effectiveBaseUrl = UrlBuilder.EnsureSegment(effectiveBaseUrl, DefaultApiVersion);
            
            return effectiveBaseUrl;
        }

        /// <summary>
        /// Gets the default base URL for Gemini.
        /// </summary>
        protected override string GetDefaultBaseUrl()
        {
            return DefaultBaseUrl;
        }
    }
}