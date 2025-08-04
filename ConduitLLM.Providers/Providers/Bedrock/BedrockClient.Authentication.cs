using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Providers.Helpers;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.Bedrock
{
    /// <summary>
    /// BedrockClient partial class containing authentication functionality.
    /// </summary>
    public partial class BedrockClient
    {
        /// <summary>
        /// Verifies AWS Bedrock authentication by listing foundation models.
        /// This is a free API call that validates AWS credentials without incurring charges.
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
                var effectiveSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "dummy-secret-key"; // Fallback for backward compatibility
                var effectiveRegion = !string.IsNullOrWhiteSpace(baseUrl) ? baseUrl : _region;
                
                if (string.IsNullOrWhiteSpace(effectiveApiKey))
                {
                    return Core.Interfaces.AuthenticationResult.Failure("API key is required");
                }

                using var client = CreateHttpClient(effectiveApiKey);
                
                // Use the foundation-models endpoint which is free and doesn't invoke any models
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://bedrock.{effectiveRegion}.amazonaws.com/foundation-models");
                
                // Add required headers
                request.Headers.Add("User-Agent", "ConduitLLM");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                
                // Sign the request with AWS Signature V4
                AwsSignatureV4.SignRequest(request, effectiveApiKey, effectiveSecretKey, effectiveRegion, "bedrock");
                
                var response = await client.SendAsync(request, cancellationToken);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                if (response.IsSuccessStatusCode)
                {
                    return Core.Interfaces.AuthenticationResult.Success($"Response time: {responseTime:F0}ms");
                }
                
                // Check for specific error codes
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return Core.Interfaces.AuthenticationResult.Failure("Invalid AWS credentials or insufficient permissions");
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return Core.Interfaces.AuthenticationResult.Failure("Invalid AWS signature or credentials");
                }
                
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"AWS Bedrock authentication failed: {response.StatusCode}",
                    errorContent);
            }
            catch (HttpRequestException ex)
            {
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Network error during authentication: {ex.Message}",
                    ex.ToString());
            }
            catch (TaskCanceledException)
            {
                return Core.Interfaces.AuthenticationResult.Failure("Authentication request timed out");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error during Bedrock authentication verification");
                return Core.Interfaces.AuthenticationResult.Failure(
                    $"Authentication verification failed: {ex.Message}",
                    ex.ToString());
            }
        }

        /// <summary>
        /// Creates headers for AWS authentication.
        /// </summary>
        /// <param name="path">The API path.</param>
        /// <param name="body">The request body.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <returns>A dictionary containing headers for AWS authentication.</returns>
        /// <remarks>
        /// In a real implementation, this would create AWS Signature V4 headers.
        /// For simplicity, this implementation returns placeholder headers.
        /// </remarks>
        private async Task<HttpResponseMessage> SendBedrockRequestAsync(
            HttpClient client,
            HttpMethod method,
            string path,
            object? requestBody,
            CancellationToken cancellationToken)
        {
            string effectiveApiKey = PrimaryKeyCredential.ApiKey!;
            string effectiveSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "dummy-secret-key"; // Fallback for backward compatibility

            // Create absolute URI by combining with client base address
            var absoluteUri = new Uri(client.BaseAddress!, path);
            var request = new HttpRequestMessage(method, absoluteUri);
            
            if (requestBody != null)
            {
                var json = JsonSerializer.Serialize(requestBody, JsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            
            // Add required headers before signing
            request.Headers.Add("User-Agent", "ConduitLLM");
            
            // Sign the request with AWS Signature V4
            AwsSignatureV4.SignRequest(request, effectiveApiKey, effectiveSecretKey, _region, _service);
            
            return await client.SendAsync(request, cancellationToken);
        }
    }
}