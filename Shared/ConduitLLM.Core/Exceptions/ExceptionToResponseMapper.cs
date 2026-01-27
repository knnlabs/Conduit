using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Exceptions;

/// <summary>
/// Maps exceptions to standardized HTTP response information.
/// Single source of truth for controller-level exception handling.
/// </summary>
public static class ExceptionToResponseMapper
{
    /// <summary>
    /// Contains the mapping result for an exception, including HTTP status code,
    /// response message, error code, and logging information.
    /// </summary>
    /// <param name="StatusCode">The HTTP status code to return.</param>
    /// <param name="ResponseMessage">The message to include in the error response.</param>
    /// <param name="ErrorCode">The programmatic error code for clients.</param>
    /// <param name="LogLevel">The log level to use when logging this exception.</param>
    /// <param name="LogPrefix">The descriptive prefix for log messages (e.g., "Argument error").</param>
    /// <param name="IncludeExceptionMessageInLog">Whether to include the exception message in the log output.</param>
    public record ExceptionMappingResult(
        int StatusCode,
        string ResponseMessage,
        string ErrorCode,
        LogLevel LogLevel,
        string LogPrefix,
        bool IncludeExceptionMessageInLog);

    /// <summary>
    /// Maps an exception to its corresponding HTTP response information.
    /// </summary>
    /// <param name="ex">The exception to map.</param>
    /// <returns>An <see cref="ExceptionMappingResult"/> containing response and logging information.</returns>
    public static ExceptionMappingResult Map(Exception ex)
    {
        return ex switch
        {
            // Custom Conduit exceptions
            AuthorizationException authEx
                => new(403, authEx.Message, "forbidden", LogLevel.Warning, "Authorization denied", true),

            ModelNotFoundException modelEx
                => new(404, modelEx.Message, "model_not_found", LogLevel.Warning, "Model not found", true),

            InvalidRequestException invalidReq
                => new(400, invalidReq.Message, invalidReq.ErrorCode ?? "invalid_request", LogLevel.Warning, "Invalid request", true),

            RateLimitExceededException rateEx
                => new(429, rateEx.Message, "rate_limit_exceeded", LogLevel.Warning, "Rate limit exceeded", true),

            ServiceUnavailableException serviceEx
                => new(503, serviceEx.Message, "service_unavailable", LogLevel.Warning, "Service unavailable", true),

            ConfigurationException
                => new(500, "A configuration error occurred", "configuration_error", LogLevel.Error, "Configuration error", false),

            // Standard .NET exceptions
            ArgumentNullException argNullEx
                => new(400, argNullEx.Message, "invalid_argument", LogLevel.Warning, "Argument error", true),

            ArgumentException argEx
                => new(400, argEx.Message, "invalid_argument", LogLevel.Warning, "Argument error", true),

            InvalidOperationException invOpEx
                => new(400, invOpEx.Message, "invalid_operation", LogLevel.Warning, "Invalid operation", true),

            KeyNotFoundException
                => new(404, "The requested resource was not found", "not_found", LogLevel.Warning, "Resource not found", false),

            UnauthorizedAccessException
                => new(403, "Access denied", "forbidden", LogLevel.Warning, "Unauthorized access attempt", false),

            // Catch-all for unexpected exceptions
            _ => new(500, "An unexpected error occurred.", "internal_error", LogLevel.Error, "Unexpected error", false)
        };
    }
}
