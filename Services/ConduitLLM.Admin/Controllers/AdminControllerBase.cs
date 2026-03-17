using ConduitLLM.Admin.Extensions;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Controllers;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Extensions;

using MassTransit;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
    ///   <item><description>Admin audit logging for security-sensitive operations</description></item>
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
        /// Initializes a new instance of the <see cref="AdminControllerBase"/> class
        /// for controllers that do not require event publishing.
        /// </summary>
        /// <param name="logger">The logger instance for the derived controller.</param>
        protected AdminControllerBase(ILogger logger)
            : this(null, logger)
        {
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
                Logger.LogDebug("{OperationName} completed successfully", operationName);
                return successAction(result);
            }
            catch (Exception ex)
            {
                return HandleOperationException(ex, operationName, contextData);
            }
        }

        /// <summary>
        /// Executes an async operation that directly returns an IActionResult,
        /// with standardized error handling.
        /// </summary>
        /// <param name="operation">The async operation that returns an IActionResult.</param>
        /// <param name="operationName">Name of the operation for logging purposes.</param>
        /// <param name="contextData">Optional context data to include in log messages.</param>
        /// <returns>The operation's IActionResult, or an error response if an exception occurs.</returns>
        protected async Task<IActionResult> ExecuteAsync(
            Func<Task<IActionResult>> operation,
            string operationName,
            object? contextData = null)
        {
            try
            {
                var result = await operation();
                Logger.LogDebug("{OperationName} completed successfully", operationName);
                return result;
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
                if (contextData != null)
                {
                    Logger.LogInformation("{OperationName} completed successfully with context {ContextData}",
                        operationName, contextData);
                }
                else
                {
                    Logger.LogInformation("{OperationName} completed successfully", operationName);
                }
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
                Logger.LogDebug("{OperationName} completed successfully for {EntityType} {EntityId}",
                    operationName, entityType, entityId);
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
                Logger.LogDebug("{OperationName} completed successfully for {EntityType} {EntityId}",
                    operationName, entityType, entityId);
                return await successAction(result);
            }
            catch (Exception ex)
            {
                return HandleOperationException(ex, operationName, new { entityType, entityId });
            }
        }

        /// <summary>
        /// Handles exceptions from operations with standardized logging and response formatting.
        /// Uses <see cref="ExceptionToResponseMapper"/> for consistent exception-to-response mapping.
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

            var mapping = ExceptionToResponseMapper.Map(ex);

            // Capture request body for mutation failures (fire-and-forget — don't block error response)
            _ = LogExceptionWithBodyAsync(mapping, ex, logMessage);

            // Return appropriate result type based on status code
            var errorResponse = new ErrorResponseDto(mapping.ResponseMessage) { Code = mapping.ErrorCode };
            return CreateErrorResult(mapping.StatusCode, errorResponse);
        }

        /// <summary>
        /// Logs the exception with the request body for mutation requests.
        /// Falls back to logging without body if capture fails.
        /// </summary>
        private async Task LogExceptionWithBodyAsync(
            ExceptionToResponseMapper.ExceptionMappingResult mapping,
            Exception ex,
            string logMessage)
        {
            string? requestBody = null;
            try
            {
                requestBody = await RequestBodyCapture.CaptureAsync(HttpContext);
            }
            catch
            {
                // Body capture should never prevent error logging
            }

            if (requestBody != null)
            {
                if (mapping.IncludeExceptionMessageInLog)
                {
                    Logger.Log(mapping.LogLevel, ex, "{LogPrefix} in {LogMessage}: {ExceptionMessage}. RequestBody: {RequestBody}",
                        mapping.LogPrefix, logMessage, ex.Message, requestBody);
                }
                else if (mapping.LogLevel == LogLevel.Error)
                {
                    Logger.LogError(ex, "{LogPrefix} in {LogMessage}. RequestBody: {RequestBody}",
                        mapping.LogPrefix, logMessage, requestBody);
                }
                else
                {
                    Logger.LogWarning("{LogPrefix} in {LogMessage}. RequestBody: {RequestBody}",
                        mapping.LogPrefix, logMessage, requestBody);
                }
            }
            else
            {
                if (mapping.IncludeExceptionMessageInLog)
                {
                    Logger.Log(mapping.LogLevel, ex, "{LogPrefix} in {LogMessage}: {ExceptionMessage}",
                        mapping.LogPrefix, logMessage, ex.Message);
                }
                else if (mapping.LogLevel == LogLevel.Error)
                {
                    Logger.LogError(ex, "{LogPrefix} in {LogMessage}", mapping.LogPrefix, logMessage);
                }
                else
                {
                    Logger.LogWarning("{LogPrefix} in {LogMessage}", mapping.LogPrefix, logMessage);
                }
            }
        }

        /// <summary>
        /// Creates an appropriate IActionResult based on the HTTP status code.
        /// Returns semantically correct result types (BadRequestObjectResult, NotFoundObjectResult, etc.)
        /// </summary>
        private IActionResult CreateErrorResult(int statusCode, ErrorResponseDto errorResponse)
        {
            return statusCode switch
            {
                400 => new BadRequestObjectResult(errorResponse),
                404 => new NotFoundObjectResult(errorResponse),
                _ => new ObjectResult(errorResponse) { StatusCode = statusCode }
            };
        }

        /// <summary>
        /// Logs a security-sensitive admin operation for audit purposes.
        /// Captures the operation, entity context, client IP, and trace ID
        /// in a structured log entry that can be filtered and queried.
        /// </summary>
        /// <param name="operation">The operation performed (e.g., "Created", "Updated", "Deleted").</param>
        /// <param name="entityType">The type of entity affected (e.g., "VirtualKey", "Provider").</param>
        /// <param name="entityId">The identifier of the affected entity (can be null for bulk operations).</param>
        /// <param name="detail">Optional additional detail about the operation.</param>
        protected void LogAdminAudit(
            string operation,
            string entityType,
            object? entityId = null,
            string? detail = null)
        {
            var clientIp = HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
            var traceId = HttpContext?.TraceIdentifier ?? "unknown";

            if (detail != null)
            {
                Logger.LogInformation(
                    "Admin Audit: {Operation} {EntityType} {EntityId} from {ClientIp} [TraceId: {TraceId}] - {Detail}",
                    operation,
                    entityType,
                    entityId ?? "N/A",
                    clientIp,
                    traceId,
                    LoggingSanitizer.S(detail));
            }
            else
            {
                Logger.LogInformation(
                    "Admin Audit: {Operation} {EntityType} {EntityId} from {ClientIp} [TraceId: {TraceId}]",
                    operation,
                    entityType,
                    entityId ?? "N/A",
                    clientIp,
                    traceId);
            }
        }
    }
}
