using ConduitLLM.Admin.Extensions;
using ConduitLLM.Core.Controllers;

using MassTransit;

using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Base class for Admin API controllers providing standardized error handling,
    /// event publishing, and common operation patterns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This base class combines the functionality of <see cref="EventPublishingControllerBase"/>
    /// with standardized error response patterns for the Admin API.
    /// </para>
    /// <para>
    /// Features:
    /// <list type="bullet">
    ///   <item><description>Fire-and-forget event publishing via MassTransit</description></item>
    ///   <item><description>Standardized error responses using <see cref="ControllerErrorExtensions"/></description></item>
    ///   <item><description>Async operation wrappers with automatic exception handling</description></item>
    ///   <item><description>Consistent logging patterns</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public abstract class AdminControllerBase : EventPublishingControllerBase
    {
        /// <summary>
        /// Logger instance for derived controllers.
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminControllerBase"/> class.
        /// </summary>
        /// <param name="publishEndpoint">Optional MassTransit publish endpoint for event publishing.</param>
        /// <param name="logger">The logger instance for the derived controller.</param>
        protected AdminControllerBase(
            IPublishEndpoint? publishEndpoint,
            ILogger logger)
            : base(publishEndpoint, logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes an async operation with standardized error handling.
        /// Automatically handles common exception types and returns appropriate responses.
        /// </summary>
        /// <typeparam name="T">The type of result returned by the operation.</typeparam>
        /// <param name="operation">The async operation to execute.</param>
        /// <param name="successAction">Function to convert the result to an IActionResult on success.</param>
        /// <param name="operationName">Name of the operation for logging purposes.</param>
        /// <param name="contextData">Optional context data to include in log messages.</param>
        /// <returns>An appropriate IActionResult based on the operation outcome.</returns>
        /// <remarks>
        /// This method handles the following exception types:
        /// <list type="bullet">
        ///   <item><description><see cref="ArgumentNullException"/> - Returns 400 Bad Request</description></item>
        ///   <item><description><see cref="ArgumentException"/> - Returns 400 Bad Request</description></item>
        ///   <item><description><see cref="InvalidOperationException"/> - Returns 400 Bad Request</description></item>
        ///   <item><description><see cref="KeyNotFoundException"/> - Returns 404 Not Found</description></item>
        ///   <item><description><see cref="UnauthorizedAccessException"/> - Returns 403 Forbidden</description></item>
        ///   <item><description>Other exceptions - Returns 500 Internal Server Error</description></item>
        /// </list>
        /// </remarks>
        protected async Task<IActionResult> ExecuteAsync<T>(
            Func<Task<T>> operation,
            Func<T, IActionResult> successAction,
            string operationName,
            object? contextData = null)
        {
            try
            {
                var result = await operation();
                return successAction(result);
            }
            catch (Exception ex)
            {
                return HandleOperationException(ex, operationName, contextData);
            }
        }

        /// <summary>
        /// Executes an async operation that returns no value with standardized error handling.
        /// </summary>
        /// <param name="operation">The async operation to execute.</param>
        /// <param name="successResult">The action result to return on success.</param>
        /// <param name="operationName">Name of the operation for logging purposes.</param>
        /// <param name="contextData">Optional context data to include in log messages.</param>
        /// <returns>An appropriate IActionResult based on the operation outcome.</returns>
        protected async Task<IActionResult> ExecuteAsync(
            Func<Task> operation,
            IActionResult successResult,
            string operationName,
            object? contextData = null)
        {
            try
            {
                await operation();
                return successResult;
            }
            catch (Exception ex)
            {
                return HandleOperationException(ex, operationName, contextData);
            }
        }

        /// <summary>
        /// Executes an async operation that may return null with standardized error handling.
        /// Returns 404 Not Found if the result is null.
        /// </summary>
        /// <typeparam name="T">The type of result returned by the operation.</typeparam>
        /// <param name="operation">The async operation to execute.</param>
        /// <param name="successAction">Function to convert the non-null result to an IActionResult.</param>
        /// <param name="entityType">The type of entity being retrieved (for 404 message).</param>
        /// <param name="entityId">Optional entity ID (for 404 message).</param>
        /// <param name="operationName">Name of the operation for logging purposes.</param>
        /// <returns>An appropriate IActionResult based on the operation outcome.</returns>
        protected async Task<IActionResult> ExecuteWithNotFoundAsync<T>(
            Func<Task<T?>> operation,
            Func<T, IActionResult> successAction,
            string entityType,
            object? entityId,
            string operationName) where T : class
        {
            try
            {
                var result = await operation();
                if (result == null)
                {
                    Logger.LogWarning("{OperationName}: {EntityType} not found with ID {EntityId}",
                        operationName, entityType, entityId);
                    return this.NotFoundEntity(entityType, entityId);
                }
                return successAction(result);
            }
            catch (Exception ex)
            {
                return HandleOperationException(ex, operationName, new { entityType, entityId });
            }
        }

        /// <summary>
        /// Executes an async operation that may return null with standardized error handling.
        /// Returns 404 Not Found if the result is null. Supports async success actions.
        /// </summary>
        /// <typeparam name="T">The type of result returned by the operation.</typeparam>
        /// <param name="operation">The async operation to execute.</param>
        /// <param name="successAction">Async function to convert the non-null result to an IActionResult.</param>
        /// <param name="entityType">The type of entity being retrieved (for 404 message).</param>
        /// <param name="entityId">Optional entity ID (for 404 message).</param>
        /// <param name="operationName">Name of the operation for logging purposes.</param>
        /// <returns>An appropriate IActionResult based on the operation outcome.</returns>
        protected async Task<IActionResult> ExecuteWithNotFoundAsync<T>(
            Func<Task<T?>> operation,
            Func<T, Task<IActionResult>> successAction,
            string entityType,
            object? entityId,
            string operationName) where T : class
        {
            try
            {
                var result = await operation();
                if (result == null)
                {
                    Logger.LogWarning("{OperationName}: {EntityType} not found with ID {EntityId}",
                        operationName, entityType, entityId);
                    return this.NotFoundEntity(entityType, entityId);
                }
                return await successAction(result);
            }
            catch (Exception ex)
            {
                return HandleOperationException(ex, operationName, new { entityType, entityId });
            }
        }

        /// <summary>
        /// Handles exceptions from operations with standardized logging and response formatting.
        /// </summary>
        /// <param name="ex">The exception that occurred.</param>
        /// <param name="operationName">Name of the operation for logging purposes.</param>
        /// <param name="contextData">Optional context data to include in log messages.</param>
        /// <returns>An appropriate IActionResult based on the exception type.</returns>
        protected IActionResult HandleOperationException(
            Exception ex,
            string operationName,
            object? contextData = null)
        {
            var logMessage = contextData != null
                ? $"{operationName} with context {contextData}"
                : operationName;

            return ex switch
            {
                ArgumentNullException argEx => HandleArgumentException(argEx, logMessage),
                ArgumentException argEx => HandleArgumentException(argEx, logMessage),
                InvalidOperationException invEx => HandleInvalidOperationException(invEx, logMessage),
                KeyNotFoundException => HandleKeyNotFoundException(logMessage),
                UnauthorizedAccessException => HandleUnauthorizedAccessException(logMessage),
                _ => HandleGenericException(ex, logMessage)
            };
        }

        private IActionResult HandleArgumentException(ArgumentException ex, string logMessage)
        {
            Logger.LogWarning(ex, "Argument error in {LogMessage}: {ExceptionMessage}", logMessage, ex.Message);
            return this.BadRequestError(ex.Message, "invalid_argument");
        }

        private IActionResult HandleInvalidOperationException(InvalidOperationException ex, string logMessage)
        {
            Logger.LogWarning(ex, "Invalid operation in {LogMessage}: {ExceptionMessage}", logMessage, ex.Message);
            return this.BadRequestError(ex.Message, "invalid_operation");
        }

        private IActionResult HandleKeyNotFoundException(string logMessage)
        {
            Logger.LogWarning("Resource not found in {LogMessage}", logMessage);
            return this.NotFoundError("The requested resource was not found", "not_found");
        }

        private IActionResult HandleUnauthorizedAccessException(string logMessage)
        {
            Logger.LogWarning("Unauthorized access attempt in {LogMessage}", logMessage);
            return StatusCode(StatusCodes.Status403Forbidden,
                new Configuration.DTOs.ErrorResponseDto("Access denied") { Code = "forbidden" });
        }

        private IActionResult HandleGenericException(Exception ex, string logMessage)
        {
            Logger.LogError(ex, "Unexpected error in {LogMessage}", logMessage);
            return this.InternalServerError();
        }
    }
}
