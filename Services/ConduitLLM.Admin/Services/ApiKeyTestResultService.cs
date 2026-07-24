using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Exceptions;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for creating standardized API key test responses
    /// </summary>
    public class ApiKeyTestResultService
    {
        /// <summary>
        /// Creates a success response for API key testing
        /// </summary>
        /// <param name="responseTimeMs">Response time in milliseconds</param>
        /// <param name="modelsAvailable">List of available models</param>
        /// <returns>Standardized success response</returns>
        public static StandardApiKeyTestResponse CreateSuccessResponse(
            double? responseTimeMs = null,
            string[]? modelsAvailable = null)
        {
            return new StandardApiKeyTestResponse
            {
                Result = ApiKeyTestResult.Success,
                Message = "Your API Key was tested and is authorized",
                Details = new ApiKeyTestDetails
                {
                    ResponseTimeMs = responseTimeMs,
                    ModelsAvailable = modelsAvailable
                }
            };
        }

        /// <summary>
        /// Creates a standardized error response based on exception details
        /// </summary>
        /// <param name="exception">The exception that occurred during testing</param>
        /// <param name="providerType">The provider type being tested</param>
        /// <returns>Standardized error response</returns>
        public static StandardApiKeyTestResponse CreateErrorResponse(
            Exception exception, 
            ProviderType? providerType = null)
        {
            // Check for provider-specific non-testable providers
            if (IsNonTestableProvider(providerType))
            {
                return new StandardApiKeyTestResponse
                {
                    Result = ApiKeyTestResult.Ignored,
                    Message = "Your API Key was untested because this provider doesn't support API Key testing without making a real API request that can cost money",
                    Details = new ApiKeyTestDetails
                    {
                        ProviderMessage = exception.Message
                    }
                };
            }

            // A configuration error (for example a missing required setting such as a Cloudflare
            // account ID) carries an already-actionable message; surface it directly.
            if (exception is ConfigurationException)
            {
                return new StandardApiKeyTestResponse
                {
                    Result = ApiKeyTestResult.Configuration,
                    Message = exception.Message,
                    Details = new ApiKeyTestDetails
                    {
                        ProviderMessage = exception.Message
                    }
                };
            }

            // Extract error details from exception
            var (statusCode, message, errorCode) = ExtractErrorDetails(exception);

            // Classify based on status code or error patterns
            if (statusCode == 401 || statusCode == 403 || 
                message?.ToLower().Contains("unauthorized") == true ||
                message?.ToLower().Contains("invalid api key") == true ||
                message?.ToLower().Contains("authentication") == true)
            {
                return new StandardApiKeyTestResponse
                {
                    Result = ApiKeyTestResult.InvalidKey,
                    Message = "Your API Key failed the authorization test",
                    Details = new ApiKeyTestDetails
                    {
                        StatusCode = statusCode,
                        ProviderMessage = message,
                        ErrorCode = errorCode
                    }
                };
            }

            if (statusCode == 429 || message?.ToLower().Contains("rate limit") == true)
            {
                return new StandardApiKeyTestResponse
                {
                    Result = ApiKeyTestResult.RateLimited,
                    Message = "API Key test was rate limited. Please try again later",
                    Details = new ApiKeyTestDetails
                    {
                        StatusCode = statusCode,
                        ProviderMessage = message,
                        ErrorCode = errorCode
                    }
                };
            }

            if (statusCode >= 500 || IsNetworkError(exception))
            {
                return new StandardApiKeyTestResponse
                {
                    Result = ApiKeyTestResult.ProviderDown,
                    Message = "We were unable to verify the request. Perhaps the LLM provider is down?",
                    Details = new ApiKeyTestDetails
                    {
                        StatusCode = statusCode,
                        ProviderMessage = message,
                        ErrorCode = errorCode
                    }
                };
            }

            // Other 4xx client errors (400/404/405) usually mean the endpoint or base URL is wrong
            // rather than the key. Report the status instead of bucketing to a generic unknown error.
            if (statusCode is 400 or 404 or 405)
            {
                return new StandardApiKeyTestResponse
                {
                    Result = ApiKeyTestResult.Configuration,
                    Message = $"The provider rejected the test request (HTTP {statusCode}). Verify the base URL and endpoint configuration for this provider.",
                    Details = new ApiKeyTestDetails
                    {
                        StatusCode = statusCode,
                        ProviderMessage = message,
                        ErrorCode = errorCode
                    }
                };
            }

            // Unknown error
            return new StandardApiKeyTestResponse
            {
                Result = ApiKeyTestResult.UnknownError,
                Message = "An unexpected error occurred during testing",
                Details = new ApiKeyTestDetails
                {
                    ProviderMessage = message,
                    ErrorCode = errorCode,
                    StatusCode = statusCode
                }
            };
        }

        /// <summary>
        /// Extracts error details from an exception
        /// </summary>
        /// <param name="exception">The exception to extract details from</param>
        /// <returns>Tuple of status code, message, and error code</returns>
        private static (int? statusCode, string? message, string? errorCode) ExtractErrorDetails(Exception exception)
        {
            var message = exception.Message;
            int? statusCode = null;
            string? errorCode = null;

            // Prefer the typed status code when available (HttpRequestException in .NET 5+).
            if (exception is HttpRequestException { StatusCode: { } typedStatus })
            {
                statusCode = (int)typedStatus;
            }

            // Otherwise parse a known status code out of the message. Provider exceptions
            // (for example LLMCommunicationException) surface the HTTP status in their text.
            if (statusCode == null && !string.IsNullOrEmpty(message))
            {
                foreach (var candidate in new[] { 401, 403, 429, 400, 404, 405, 408, 500, 502, 503 })
                {
                    if (message.Contains(candidate.ToString()))
                    {
                        statusCode = candidate;
                        break;
                    }
                }
            }

            return (statusCode, message, errorCode);
        }

        /// <summary>
        /// Determines if an exception represents a network error
        /// </summary>
        /// <param name="exception">The exception to check</param>
        /// <returns>True if it's a network error</returns>
        private static bool IsNetworkError(Exception exception)
        {
            return exception is TimeoutException ||
                   exception is TaskCanceledException ||
                   (exception is HttpRequestException httpEx && 
                    (httpEx.Message.Contains("timeout") || 
                     httpEx.Message.Contains("network") ||
                     httpEx.Message.Contains("connection")));
        }

        /// <summary>
        /// Determines if a provider type doesn't support simple API key testing
        /// </summary>
        /// <param name="providerType">The provider type to check</param>
        /// <returns>True if the provider doesn't support testing</returns>
        private static bool IsNonTestableProvider(ProviderType? providerType)
        {
            if (!providerType.HasValue)
                return false;

            // List of providers that don't support simple API key testing
            // Replicate: Requires real API calls that can incur costs
            // SambaNova: /models endpoint is public and doesn't validate API keys
            var nonTestableProviders = new[] { ProviderType.Replicate, ProviderType.SambaNova };
            return nonTestableProviders.Contains(providerType.Value);
        }
    }
}