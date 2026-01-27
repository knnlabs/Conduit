using ConduitLLM.Configuration.DTOs;
using Microsoft.AspNetCore.Mvc;

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

            return ex switch
            {
                ArgumentNullException argEx => HandleArgumentException(controller, argEx, logger, logMessage),
                ArgumentException argEx => HandleArgumentException(controller, argEx, logger, logMessage),
                InvalidOperationException invEx => HandleInvalidOperationException(controller, invEx, logger, logMessage),
                KeyNotFoundException => HandleKeyNotFoundException(controller, logger, logMessage),
                UnauthorizedAccessException => HandleUnauthorizedAccessException(controller, logger, logMessage),
                _ => HandleGenericException(controller, ex, logger, logMessage)
            };
        }

        private static IActionResult HandleArgumentException(
            ControllerBase controller,
            ArgumentException ex,
            ILogger? logger,
            string logMessage)
        {
            logger?.LogWarning(ex, "{LogMessage}: {ExceptionMessage}", logMessage, ex.Message);
            return controller.BadRequestError(ex.Message, "invalid_argument");
        }

        private static IActionResult HandleInvalidOperationException(
            ControllerBase controller,
            InvalidOperationException ex,
            ILogger? logger,
            string logMessage)
        {
            logger?.LogWarning(ex, "{LogMessage}: {ExceptionMessage}", logMessage, ex.Message);
            return controller.BadRequestError(ex.Message, "invalid_operation");
        }

        private static IActionResult HandleKeyNotFoundException(
            ControllerBase controller,
            ILogger? logger,
            string logMessage)
        {
            logger?.LogWarning("{LogMessage}: Resource not found", logMessage);
            return controller.NotFoundError("The requested resource was not found", "not_found");
        }

        private static IActionResult HandleUnauthorizedAccessException(
            ControllerBase controller,
            ILogger? logger,
            string logMessage)
        {
            logger?.LogWarning("{LogMessage}: Unauthorized access attempt", logMessage);
            return controller.StatusCode(StatusCodes.Status403Forbidden,
                new ErrorResponseDto("Access denied") { Code = "forbidden" });
        }

        private static IActionResult HandleGenericException(
            ControllerBase controller,
            Exception ex,
            ILogger? logger,
            string logMessage)
        {
            logger?.LogError(ex, "{LogMessage}", logMessage);
            return controller.InternalServerError();
        }
    }
}
