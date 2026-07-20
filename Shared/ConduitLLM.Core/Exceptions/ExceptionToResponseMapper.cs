using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Exceptions;

/// <summary>
/// Maps exceptions to standardized HTTP response information.
/// Single source of truth for exception-to-response mapping across both
/// Admin API (ErrorResponseDto) and Gateway API (OpenAIErrorResponse).
/// </summary>
public static class ExceptionToResponseMapper
{
    /// <summary>
    /// Contains the mapping result for an exception, including HTTP status code,
    /// response message, error code, OpenAI error type, and logging information.
    /// </summary>
    /// <param name="StatusCode">The HTTP status code to return.</param>
    /// <param name="ResponseMessage">The message to include in the error response. For custom ConduitExceptions
    /// this is the exception message; for standard .NET exceptions this is a safe generic message.</param>
    /// <param name="ErrorCode">The programmatic error code for clients (e.g., "model_not_found", "rate_limit_exceeded").</param>
    /// <param name="LogLevel">The log level to use when logging this exception.</param>
    /// <param name="LogPrefix">The descriptive prefix for log messages (e.g., "Argument error").</param>
    /// <param name="IncludeExceptionMessageInLog">Whether the ResponseMessage contains the actual exception message.
    /// When false, the ResponseMessage is a safe generic message and callers may choose to show the real
    /// exception message in development environments.</param>
    /// <param name="OpenAIErrorType">The OpenAI-compatible error type string (e.g., "invalid_request_error", "server_error").</param>
    /// <param name="Param">The parameter that caused the error, if applicable (e.g., from ArgumentException.ParamName).</param>
    public record ExceptionMappingResult(
        int StatusCode,
        string ResponseMessage,
        string ErrorCode,
        LogLevel LogLevel,
        string LogPrefix,
        bool IncludeExceptionMessageInLog,
        string OpenAIErrorType,
        string? Param = null);

    /// <summary>
    /// Maps an exception to its corresponding HTTP response information.
    /// </summary>
    /// <param name="ex">The exception to map.</param>
    /// <returns>An <see cref="ExceptionMappingResult"/> containing response and logging information.</returns>
    public static ExceptionMappingResult Map(Exception ex)
    {
        return ex switch
        {
            // Custom Conduit exceptions — messages are user-safe
            AuthorizationException authEx
                => new(403, authEx.Message, "forbidden", LogLevel.Warning,
                    "Authorization denied", true, "invalid_request_error"),

            ModelNotFoundException modelEx
                => new(404, modelEx.Message, "model_not_found", LogLevel.Warning,
                    "Model not found", true, "invalid_request_error", "model"),

            InvalidRequestException invalidReq
                => new(400, invalidReq.Message, invalidReq.ErrorCode ?? "invalid_request", LogLevel.Warning,
                    "Invalid request", true, "invalid_request_error", invalidReq.Param),

            RequestTimeoutException timeoutEx
                => new(408, timeoutEx.Message, "request_timeout", LogLevel.Warning,
                    "Request timeout", true, "timeout_error"),

            PayloadTooLargeException payloadEx
                => new(413, payloadEx.Message, "payload_too_large", LogLevel.Warning,
                    "Payload too large", true, "invalid_request_error"),

            RateLimitExceededException rateEx
                => new(429, rateEx.Message, "rate_limit_exceeded", LogLevel.Warning,
                    "Rate limit exceeded", true, "rate_limit_error"),

            ServiceUnavailableException serviceEx
                => new(503, serviceEx.Message, "service_unavailable", LogLevel.Warning,
                    "Service unavailable", true, "service_unavailable"),

            LLMCommunicationException commEx
                => MapLLMCommunicationException(commEx),

            ConfigurationException
                => new(500, "A configuration error occurred", "configuration_error", LogLevel.Error,
                    "Configuration error", false, "server_error"),

            // Standard .NET exceptions — use safe generic messages
            ArgumentNullException argNullEx
                => new(400, "Required parameter is missing", "missing_parameter", LogLevel.Warning,
                    "Argument error", false, "invalid_request_error", argNullEx.ParamName),

            ArgumentException argEx
                => new(400, "Invalid parameter value", "invalid_parameter", LogLevel.Warning,
                    "Argument error", false, "invalid_request_error", argEx.ParamName),

            InvalidOperationException invalidOp when IsDependencyResolutionFailure(invalidOp)
                => new(500, "A server dependency could not be resolved", "dependency_resolution_error", LogLevel.Error,
                    "Dependency resolution error", false, "server_error"),

            InvalidOperationException
                => new(400, "The requested operation is not valid", "invalid_operation", LogLevel.Warning,
                    "Invalid operation", false, "invalid_request_error"),

            KeyNotFoundException
                => new(404, "The requested resource was not found", "not_found", LogLevel.Warning,
                    "Resource not found", false, "invalid_request_error"),

            UnauthorizedAccessException
                => new(401, "Authentication required", "unauthorized", LogLevel.Warning,
                    "Unauthorized access attempt", false, "invalid_request_error"),

            TimeoutException
                => new(408, "Request timed out", "timeout", LogLevel.Warning,
                    "Request timeout", false, "timeout_error"),

            NotSupportedException
                => new(400, "The requested feature is not supported", "not_supported", LogLevel.Warning,
                    "Not supported", false, "invalid_request_error"),

            NotImplementedException
                => new(501, "Feature not implemented", "not_implemented", LogLevel.Warning,
                    "Not implemented", false, "server_error"),

            // Catch-all for unexpected exceptions
            _ => new(500, "An unexpected error occurred", "internal_error", LogLevel.Error,
                    "Unexpected error", false, "server_error")
        };
    }

    private static bool IsDependencyResolutionFailure(InvalidOperationException exception)
    {
        return exception.Message.StartsWith("Unable to resolve service for type", StringComparison.Ordinal)
            || (exception.Message.StartsWith("No service for type", StringComparison.Ordinal)
                && exception.Message.EndsWith("has been registered.", StringComparison.Ordinal));
    }

    /// <summary>
    /// Maps an LLMCommunicationException, deriving status code and error type from the provider's response.
    /// </summary>
    private static ExceptionMappingResult MapLLMCommunicationException(LLMCommunicationException commEx)
    {
        if (commEx.StatusCode.HasValue)
        {
            var statusCode = (int)commEx.StatusCode.Value;
            var isServerError = statusCode >= 500;
            return new(
                statusCode,
                commEx.Message,
                "provider_communication_error",
                isServerError ? LogLevel.Error : LogLevel.Warning,
                "Provider communication error",
                true,
                isServerError ? "server_error" : "invalid_request_error");
        }

        return new(500, commEx.Message, "provider_communication_error", LogLevel.Error,
            "Provider communication error", true, "server_error");
    }
}
