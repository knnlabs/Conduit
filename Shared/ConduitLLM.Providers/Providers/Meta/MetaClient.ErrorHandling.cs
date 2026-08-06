using System.Text.Json;

using ConduitLLM.Core.Exceptions;

namespace ConduitLLM.Providers.Meta
{
    /// <summary>
    /// MetaClient partial class containing error handling methods.
    /// </summary>
    public partial class MetaClient
    {
        /// <inheritdoc />
        protected override Exception? TranslateHttpError(
            HttpResponseMessage response,
            string responseContent)
        {
            var requestId = response.Headers.TryGetValues("X-Request-Id", out var requestIds)
                ? requestIds.FirstOrDefault()
                : null;

            return ProcessHttpError(response.StatusCode, responseContent, requestId);
        }

        /// <summary>
        /// Processes HTTP errors and converts them to appropriate exceptions.
        /// </summary>
        /// <param name="statusCode">The HTTP status code.</param>
        /// <param name="responseContent">The response content.</param>
        /// <param name="requestId">Optional request ID for tracking.</param>
        /// <returns>An appropriate exception for the error.</returns>
        private Exception ProcessHttpError(System.Net.HttpStatusCode statusCode, string responseContent, string? requestId = null)
        {
            var communicationError = CreateCommunicationError(
                $"Meta Model API error ({(int)statusCode} {statusCode}): {responseContent}",
                statusCode,
                responseContent,
                requestId);

            return statusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized =>
                    new ConfigurationException(MetaErrorMessages.InvalidApiKey, communicationError),
                System.Net.HttpStatusCode.TooManyRequests =>
                    CreateCommunicationError(
                        MetaErrorMessages.RateLimitExceeded,
                        statusCode,
                        responseContent,
                        requestId),
                System.Net.HttpStatusCode.NotFound =>
                    new ModelNotFoundException(
                        ProviderModelId,
                        MetaErrorMessages.ModelNotFound,
                        communicationError),
                System.Net.HttpStatusCode.PaymentRequired =>
                    CreateCommunicationError(
                        MetaErrorMessages.QuotaExceeded,
                        statusCode,
                        responseContent,
                        requestId),
                System.Net.HttpStatusCode.BadRequest =>
                    ParseBadRequestError(responseContent, communicationError),
                System.Net.HttpStatusCode.InternalServerError =>
                    CreateCommunicationError(
                        $"Meta Model API internal error: {responseContent}",
                        statusCode,
                        responseContent,
                        requestId),
                System.Net.HttpStatusCode.ServiceUnavailable =>
                    CreateCommunicationError(
                        "Meta Model API is temporarily unavailable. Please try again later.",
                        statusCode,
                        responseContent,
                        requestId),
                _ => communicationError
            };
        }

        private static LLMCommunicationException CreateCommunicationError(
            string message,
            System.Net.HttpStatusCode statusCode,
            string responseContent,
            string? requestId)
        {
            var exception = new LLMCommunicationException(
                message,
                statusCode,
                responseContent);

            if (!string.IsNullOrWhiteSpace(requestId))
            {
                exception.Data["RequestId"] = requestId;
            }

            return exception;
        }

        /// <summary>
        /// Parses bad request errors to provide more specific error information.
        /// </summary>
        /// <param name="responseContent">The response content containing error details.</param>
        /// <param name="communicationError">The structured provider communication error.</param>
        /// <returns>An appropriate exception for the bad request error.</returns>
        private Exception ParseBadRequestError(
            string responseContent,
            LLMCommunicationException communicationError)
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
                            return new ModelNotFoundException(
                                ProviderModelId,
                                $"Model error: {errorMessage}",
                                communicationError);
                        }

                        if (errorMessage?.Contains("token", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            return new ValidationException(
                                $"Token limit error: {errorMessage}",
                                communicationError);
                        }

                        return new ValidationException(
                            $"Request error: {errorMessage}",
                            communicationError);
                    }
                }
            }
            catch (JsonException)
            {
                // Fall through to generic error if JSON parsing fails
            }

            return new ValidationException(
                $"Bad request: {responseContent}",
                communicationError);
        }
    }
}
