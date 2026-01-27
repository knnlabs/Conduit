using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Exceptions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Extensions
{
    /// <summary>
    /// Extension methods for standardized error responses in controllers.
    /// </summary>
    /// <remarks>
    /// These extensions ensure consistent error response format across all Admin API controllers.
    /// All error responses use the <see cref="ErrorResponseDto"/> format for consistency.
    /// </remarks>
    public static class ControllerErrorExtensions
    {
        /// <summary>
        /// Creates a standardized 400 Bad Request response.
        /// </summary>
        /// <param name="controller">The controller instance.</param>
        /// <param name="message">The error message.</param>
        /// <param name="code">Optional error code for programmatic handling.</param>
        /// <returns>A BadRequest result with standardized error format.</returns>
        public static BadRequestObjectResult BadRequestError(
            this ControllerBase controller,
            string message,
            string? code = null)
        {
            return controller.BadRequest(new ErrorResponseDto(message) { Code = code });
        }

        /// <summary>
        /// Creates a standardized 404 Not Found response.
        /// </summary>
        /// <param name="controller">The controller instance.</param>
        /// <param name="message">The error message.</param>
        /// <param name="code">Optional error code for programmatic handling.</param>
        /// <returns>A NotFound result with standardized error format.</returns>
        public static NotFoundObjectResult NotFoundError(
            this ControllerBase controller,
            string message,
            string? code = null)
        {
            return controller.NotFound(new ErrorResponseDto(message) { Code = code });
        }

        /// <summary>
        /// Creates a standardized 404 Not Found response for a specific entity type.
        /// </summary>
        /// <param name="controller">The controller instance.</param>
        /// <param name="entityType">The type of entity that was not found (e.g., "Provider", "VirtualKey").</param>
        /// <param name="entityId">Optional identifier of the entity.</param>
        /// <returns>A NotFound result with standardized error format.</returns>
        public static NotFoundObjectResult NotFoundEntity(
            this ControllerBase controller,
            string entityType,
            object? entityId = null)
        {
            var message = entityId != null
                ? $"{entityType} with ID '{entityId}' not found"
                : $"{entityType} not found";
            return controller.NotFound(new ErrorResponseDto(message) { Code = "not_found" });
        }

        /// <summary>
        /// Creates a standardized 409 Conflict response.
        /// </summary>
        /// <param name="controller">The controller instance.</param>
        /// <param name="message">The error message.</param>
        /// <param name="code">Optional error code for programmatic handling.</param>
        /// <returns>A Conflict result with standardized error format.</returns>
        public static ConflictObjectResult ConflictError(
            this ControllerBase controller,
            string message,
            string? code = null)
        {
            return controller.Conflict(new ErrorResponseDto(message) { Code = code });
        }

        /// <summary>
        /// Creates a standardized 500 Internal Server Error response.
        /// </summary>
        /// <param name="controller">The controller instance.</param>
        /// <param name="message">The error message (defaults to generic message for security).</param>
        /// <param name="details">Optional additional details (only include in non-production environments).</param>
        /// <returns>An ObjectResult with 500 status code and standardized error format.</returns>
        public static ObjectResult InternalServerError(
            this ControllerBase controller,
            string message = "An unexpected error occurred.",
            string? details = null)
        {
            var error = new ErrorResponseDto(message) { Details = details, Code = "internal_error" };
            return controller.StatusCode(StatusCodes.Status500InternalServerError, error);
        }

        /// <summary>
        /// Creates a standardized 503 Service Unavailable response.
        /// </summary>
        /// <param name="controller">The controller instance.</param>
        /// <param name="message">The error message.</param>
        /// <param name="code">Optional error code for programmatic handling.</param>
        /// <returns>An ObjectResult with 503 status code and standardized error format.</returns>
        public static ObjectResult ServiceUnavailableError(
            this ControllerBase controller,
            string message,
            string? code = null)
        {
            var error = new ErrorResponseDto(message) { Code = code ?? "service_unavailable" };
            return controller.StatusCode(StatusCodes.Status503ServiceUnavailable, error);
        }

        /// <summary>
        /// Creates a standardized 422 Unprocessable Entity response for validation errors.
        /// </summary>
        /// <param name="controller">The controller instance.</param>
        /// <param name="message">The validation error message.</param>
        /// <param name="code">Optional error code for programmatic handling.</param>
        /// <returns>An UnprocessableEntity result with standardized error format.</returns>
        public static UnprocessableEntityObjectResult ValidationError(
            this ControllerBase controller,
            string message,
            string? code = null)
        {
            return controller.UnprocessableEntity(new ErrorResponseDto(message) { Code = code ?? "validation_error" });
        }

        /// <summary>
        /// Creates an appropriate error response from an exception.
        /// Uses <see cref="ExceptionToResponseMapper"/> for consistent exception-to-response mapping.
        /// </summary>
        /// <param name="controller">The controller instance.</param>
        /// <param name="ex">The exception that occurred.</param>
        /// <param name="logger">Optional logger for error logging.</param>
        /// <param name="contextMessage">Optional context message for logging.</param>
        /// <returns>An appropriate error result based on the exception type.</returns>
        public static IActionResult HandleException(
            this ControllerBase controller,
            Exception ex,
            ILogger? logger = null,
            string? contextMessage = null)
        {
            var logMessage = contextMessage ?? "An error occurred";
            var mapping = ExceptionToResponseMapper.Map(ex);

            // Log at appropriate level with context
            if (mapping.IncludeExceptionMessageInLog)
            {
                logger?.Log(mapping.LogLevel, ex, "{LogMessage}: {ExceptionMessage}", logMessage, ex.Message);
            }
            else if (mapping.LogLevel == LogLevel.Error)
            {
                logger?.LogError(ex, "{LogMessage}", logMessage);
            }
            else
            {
                logger?.LogWarning("{LogMessage}: {LogPrefix}", logMessage, mapping.LogPrefix);
            }

            // Return standardized response
            return new ObjectResult(new ErrorResponseDto(mapping.ResponseMessage) { Code = mapping.ErrorCode })
            {
                StatusCode = mapping.StatusCode
            };
        }
    }
}
