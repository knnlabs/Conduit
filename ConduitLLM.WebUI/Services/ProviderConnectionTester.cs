using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.Data;
using ConduitLLM.WebUI.Services;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Provides helper methods for testing provider connections.
    /// </summary>
    public static class ProviderConnectionTester
    {
        /// <summary>
        /// Test a provider connection and return a ProviderStatus with user-friendly error handling.
        /// </summary>
        /// <param name="providerStatusService">The provider status service</param>
        /// <param name="credentials">The provider credentials</param>
        /// <param name="timeoutSeconds">Optional timeout in seconds</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The provider status</returns>
        public static async Task<ProviderStatus> TestConnectionAsync(
            ProviderStatusService providerStatusService,
            ProviderCredential credentials,
            int timeoutSeconds = 10,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(credentials.ApiKey))
            {
                return new ProviderStatus
                {
                    IsOnline = false,
                    StatusMessage = "API key is required",
                    LastCheckedUtc = DateTime.UtcNow,
                    ErrorCategory = "Configuration"
                };
            }

            try
            {
                return await providerStatusService.CheckProviderStatusAsync(credentials, timeoutSeconds, cancellationToken);
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message;
                string errorCategory = "Unknown";
                
                if (ex is TaskCanceledException)
                {
                    errorMsg = "Connection timeout";
                    errorCategory = "Timeout";
                }
                else if (ex is HttpRequestException httpEx)
                {
                    errorCategory = "Network";
                    
                    if (httpEx.Data.Contains("Body"))
                    {
                        var body = httpEx.Data["Body"]?.ToString();
                        if (!string.IsNullOrEmpty(body))
                            errorMsg = ExtractUserFriendlyError(body);
                    }
                }
                else if (ex.InnerException is HttpRequestException innerHttpEx)
                {
                    errorCategory = "Network";
                    
                    if (innerHttpEx.Data.Contains("Body"))
                    {
                        var body = innerHttpEx.Data["Body"]?.ToString();
                        if (!string.IsNullOrEmpty(body))
                            errorMsg = ExtractUserFriendlyError(body);
                    }
                }
                
                return new ProviderStatus
                {
                    IsOnline = false,
                    StatusMessage = $"Error: {errorMsg}",
                    LastCheckedUtc = DateTime.UtcNow,
                    ErrorCategory = errorCategory,
                    ResponseTimeMs = 0
                };
            }
        }

        /// <summary>
        /// Extracts a user-friendly error message from a JSON error response.
        /// </summary>
        /// <param name="errorContent">The error content to parse</param>
        /// <returns>A user-friendly error message</returns>
        public static string ExtractUserFriendlyError(string errorContent)
        {
            try
            {
                using var doc = JsonDocument.Parse(errorContent);
                
                // Common OpenAI/Azure OpenAI pattern
                if (doc.RootElement.TryGetProperty("error", out var errorObj))
                {
                    if (errorObj.TryGetProperty("message", out var messageProp))
                    {
                        return messageProp.GetString() ?? errorContent;
                    }
                    
                    // If it's a string, use it directly
                    if (errorObj.ValueKind == JsonValueKind.String)
                    {
                        return errorObj.GetString() ?? errorContent;
                    }
                }
                
                // Anthropic pattern
                if (doc.RootElement.TryGetProperty("type", out var typeObj) && 
                    doc.RootElement.TryGetProperty("message", out var messageObj))
                {
                    string type = typeObj.GetString() ?? "error";
                    string message = messageObj.GetString() ?? errorContent;
                    return $"{type}: {message}";
                }
            }
            catch
            {
                // Not JSON or doesn't match expected structure
            }
            return errorContent;
        }
    }
}
