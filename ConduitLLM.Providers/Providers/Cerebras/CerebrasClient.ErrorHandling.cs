using System;
using System.Text.Json;

using ConduitLLM.Core.Exceptions;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Cerebras
{
    /// <summary>
    /// CerebrasClient partial class containing error handling methods.
    /// </summary>
    public partial class CerebrasClient
    {
        /// <summary>
        /// Processes HTTP errors and converts them to appropriate exceptions.
        /// </summary>
        /// <param name="statusCode">The HTTP status code.</param>
        /// <param name="responseContent">The response content.</param>
        /// <param name="requestId">Optional request ID for tracking.</param>
        /// <returns>An appropriate exception for the error.</returns>
        private Exception ProcessHttpError(System.Net.HttpStatusCode statusCode, string responseContent, string? requestId = null)
        {
            Logger.LogError("Cerebras API error - Status: {StatusCode}, Content: {Content}, RequestId: {RequestId}",
                statusCode, responseContent, requestId);

            return statusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => new ConfigurationException(Constants.ErrorMessages.InvalidApiKey),
                System.Net.HttpStatusCode.TooManyRequests => new LLMCommunicationException(Constants.ErrorMessages.RateLimitExceeded),
                System.Net.HttpStatusCode.NotFound => new ModelUnavailableException(Constants.ErrorMessages.ModelNotFound),
                System.Net.HttpStatusCode.PaymentRequired => new LLMCommunicationException(Constants.ErrorMessages.QuotaExceeded),
                System.Net.HttpStatusCode.BadRequest => ParseBadRequestError(responseContent),
                System.Net.HttpStatusCode.InternalServerError => new LLMCommunicationException($"Cerebras API internal error: {responseContent}"),
                System.Net.HttpStatusCode.ServiceUnavailable => new LLMCommunicationException("Cerebras API is temporarily unavailable. Please try again later."),
                _ => new LLMCommunicationException($"Cerebras API error ({statusCode}): {responseContent}")
            };
        }

        /// <summary>
        /// Parses bad request errors to provide more specific error information.
        /// </summary>
        /// <param name="responseContent">The response content containing error details.</param>
        /// <returns>An appropriate exception for the bad request error.</returns>
        private Exception ParseBadRequestError(string responseContent)
        {
            try
            {
                using var document = JsonDocument.Parse(responseContent);
                if (document.RootElement.TryGetProperty("error", out var errorElement))
                {
                    if (errorElement.TryGetProperty("message", out var messageElement))
                    {
                        var errorMessage = messageElement.GetString();
                        
                        // Check for specific error patterns
                        if (errorMessage?.Contains("model", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            return new ModelUnavailableException($"Model error: {errorMessage}");
                        }
                        
                        if (errorMessage?.Contains("token", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            return new ValidationException($"Token limit error: {errorMessage}");
                        }
                        
                        return new ValidationException($"Request error: {errorMessage}");
                    }
                }
            }
            catch (JsonException)
            {
                // Fall through to generic error if JSON parsing fails
            }

            return new ValidationException($"Bad request: {responseContent}");
        }
    }
}
