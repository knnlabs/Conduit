using System;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.Anthropic
{
    /// <summary>
    /// AnthropicClient partial class containing utility and helper methods.
    /// </summary>
    public partial class AnthropicClient
    {
        /// <summary>
        /// Extracts a more helpful error message from exception details for Anthropic errors.
        /// </summary>
        /// <param name="ex">The exception to extract information from.</param>
        /// <returns>An enhanced error message specific to Anthropic errors.</returns>
        /// <remarks>
        /// <para>
        /// This method extracts detailed error information from Anthropic API exceptions, using
        /// several strategies to find the most helpful error message:
        /// </para>
        /// <list type="number">
        ///   <item><description>Looking for specific error patterns in the exception message</description></item>
        ///   <item><description>Examining JSON content in the message for error details</description></item>
        ///   <item><description>Checking for response body data stored in the exception</description></item>
        ///   <item><description>Examining inner exceptions</description></item>
        /// </list>
        /// <para>
        /// For common error conditions, this method returns standardized, user-friendly
        /// error messages that provide clear guidance on how to resolve the issue.
        /// </para>
        /// </remarks>
        protected string ExtractEnhancedErrorMessage(Exception ex)
        {
            // Extract the message from the exception
            var msg = ex.Message;

            // Check for model not found errors
            if (msg.Contains("model not found", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("not supported", StringComparison.OrdinalIgnoreCase) &&
                msg.Contains("model", StringComparison.OrdinalIgnoreCase))
            {
                return Constants.ErrorMessages.ModelNotFound;
            }

            // Check for authentication errors
            if (msg.Contains("invalid api key", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("invalid_auth", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("authentication", StringComparison.OrdinalIgnoreCase) &&
                msg.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
            {
                return Constants.ErrorMessages.InvalidApiKey;
            }

            // Check for rate limit errors
            if (msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("rate_limit", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("429", StringComparison.OrdinalIgnoreCase))
            {
                return Constants.ErrorMessages.RateLimitExceeded;
            }

            // Look for JSON content in the message
            var jsonStart = msg.IndexOf("{");
            var jsonEnd = msg.LastIndexOf("}");
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonPart = msg.Substring(jsonStart, jsonEnd - jsonStart + 1);
                try
                {
                    var json = JsonDocument.Parse(jsonPart);
                    if (json.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        if (errorElement.TryGetProperty("message", out var messageElement))
                        {
                            var errorMsg = messageElement.GetString();
                            if (!string.IsNullOrEmpty(errorMsg))
                            {
                                return $"Anthropic API error: {errorMsg}";
                            }
                        }
                        else if (errorElement.ValueKind == JsonValueKind.String)
                        {
                            var errorMsg = errorElement.GetString();
                            if (!string.IsNullOrEmpty(errorMsg))
                            {
                                return $"Anthropic API error: {errorMsg}";
                            }
                        }
                    }
                }
                catch
                {
                    // If parsing fails, continue to the next method
                }
            }

            // Look for Body data in the exception's Data dictionary
            if (ex.Data.Contains("Body") && ex.Data["Body"] is string body && !string.IsNullOrEmpty(body))
            {
                return $"Anthropic API error: {body}";
            }

            // Try inner exception
            if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
            {
                return $"Anthropic API error: {ex.InnerException.Message}";
            }

            // Fallback to original message with provider name
            return $"Anthropic API error: {msg}";
        }
    }
}