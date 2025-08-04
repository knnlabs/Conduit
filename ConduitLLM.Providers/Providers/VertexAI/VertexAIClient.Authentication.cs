using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.VertexAI
{
    /// <summary>
    /// VertexAIClient partial class containing authentication functionality.
    /// </summary>
    public partial class VertexAIClient
    {
        /// <summary>
        /// Verifies Google Vertex AI authentication by listing available models.
        /// This is a free API call that validates credentials and project access.
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
                    return AuthenticationResult.Failure("API key is required");
                }

                if (string.IsNullOrWhiteSpace(_projectId))
                {
                    return AuthenticationResult.Failure("Google Cloud project ID is required");
                }

                using var client = CreateHttpClient(effectiveApiKey);
                
                // Use the models list endpoint which is free
                var request = new HttpRequestMessage(HttpMethod.Get, 
                    $"https://{DefaultRegion}-aiplatform.googleapis.com/v1/projects/{_projectId}/locations/{DefaultRegion}/publishers/google/models");
                
                // Add authentication header
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", effectiveApiKey);
                request.Headers.Add("User-Agent", "ConduitLLM");
                
                var response = await client.SendAsync(request, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                if (response.IsSuccessStatusCode)
                {
                    return AuthenticationResult.Success(
                        $"Project '{_projectId}' verified. Response time: {responseTime:F0}ms");
                }
                
                // Check for specific error codes
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return AuthenticationResult.Failure("Invalid or expired API key");
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return AuthenticationResult.Failure(
                        $"Access denied. Verify that the API key has access to project '{_projectId}' and Vertex AI API is enabled");
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return AuthenticationResult.Failure(
                        $"Project '{_projectId}' not found or Vertex AI API not enabled");
                }
                
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return AuthenticationResult.Failure(
                    $"Vertex AI authentication failed: {response.StatusCode}",
                    errorContent);
            }
            catch (HttpRequestException ex)
            {
                return AuthenticationResult.Failure(
                    $"Network error during authentication: {ex.Message}",
                    ex.ToString());
            }
            catch (TaskCanceledException)
            {
                return AuthenticationResult.Failure("Authentication request timed out");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error during Vertex AI authentication verification");
                return AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }
    }
}