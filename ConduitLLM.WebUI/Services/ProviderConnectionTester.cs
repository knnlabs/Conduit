using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ConduitLLM.WebUI.Data;
using ConduitLLM.WebUI.Services;

namespace ConduitLLM.WebUI.Services
{
    public static class ProviderConnectionTester
    {
        /// <summary>
        /// Test a provider connection and return a ProviderStatus with user-friendly error handling.
        /// </summary>
        public static async Task<ProviderStatus> TestConnectionAsync(
            ProviderStatusService providerStatusService,
            ConduitLLM.Configuration.Entities.ProviderCredential credentials) // Use correct entity type
        {
            if (string.IsNullOrWhiteSpace(credentials.ApiKey))
            {
                return new ProviderStatus
                {
                    IsOnline = false,
                    StatusMessage = "API key is required",
                    LastCheckedUtc = DateTime.UtcNow
                };
            }

            try
            {
                return await providerStatusService.CheckProviderStatusAsync(credentials);
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message;
                if (ex is HttpRequestException httpEx && httpEx.Data.Contains("Body"))
                {
                    var body = httpEx.Data["Body"]?.ToString();
                    if (!string.IsNullOrEmpty(body))
                        errorMsg = ExtractUserFriendlyError(body);
                }
                else if (ex.InnerException is HttpRequestException innerHttpEx && innerHttpEx.Data.Contains("Body"))
                {
                    var body = innerHttpEx.Data["Body"]?.ToString();
                    if (!string.IsNullOrEmpty(body))
                        errorMsg = ExtractUserFriendlyError(body);
                }
                return new ProviderStatus
                {
                    IsOnline = false,
                    StatusMessage = $"Error: {errorMsg}",
                    LastCheckedUtc = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Extracts a user-friendly error message from a JSON error response.
        /// </summary>
        public static string ExtractUserFriendlyError(string errorContent)
        {
            try
            {
                using var doc = JsonDocument.Parse(errorContent);
                if (doc.RootElement.TryGetProperty("error", out var errorObj) &&
                    errorObj.TryGetProperty("message", out var messageProp))
                {
                    return messageProp.GetString() ?? errorContent;
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
