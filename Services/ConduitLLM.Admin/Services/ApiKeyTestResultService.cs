using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;

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

            // Try to extract HTTP status code from common exception types
            if (exception is HttpRequestException httpEx)
            {
                // Try to parse status code from message
                if (message.Contains("401"))
                    statusCode = 401;
                else if (message.Contains("403"))
                    statusCode = 403;
                else if (message.Contains("429"))
                    statusCode = 429;
                else if (message.Contains("500"))
                    statusCode = 500;
                else if (message.Contains("502"))
                    statusCode = 502;
                else if (message.Contains("503"))
                    statusCode = 503;
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