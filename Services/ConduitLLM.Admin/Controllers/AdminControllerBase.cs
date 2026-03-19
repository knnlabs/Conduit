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
        /// Initializes a new instance of the <see cref="AdminControllerBase"/> class.
        /// </summary>
        /// <param name="publishEndpoint">Optional MassTransit publish endpoint for event publishing.</param>
        /// <param name="logger">The logger instance for the derived controller.</param>
        protected AdminControllerBase(
            IPublishEndpoint? publishEndpoint,
            ILogger logger)
            : base(publishEndpoint, logger)
        {
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
                LogOperationSuccess(operationName);
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
                LogOperationSuccess(operationName);
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
                LogOperationSuccess(operationName, entityType, entityId);
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
                LogOperationSuccess(operationName, entityType, entityId);
                return await successAction(result);
            }
            catch (Exception ex)
            {
                return HandleOperationException(ex, operationName, new { entityType, entityId });
            }
        }

        /// <summary>
        /// Logs operation success at Information level for mutations (POST/PUT/PATCH/DELETE)
        /// and Debug level for reads (GET/HEAD/OPTIONS).
        /// </summary>
        private void LogOperationSuccess(string operationName, string? entityType = null, object? entityId = null)
        {
            if (IsMutationRequest())
            {
                if (entityType != null)
                {
                    Logger.LogInformation("{OperationName} completed successfully for {EntityType} {EntityId}",
                        operationName, entityType, entityId);
                }
                else
                {
                    Logger.LogInformation("{OperationName} completed successfully", operationName);
                }
            }
            else
            {
                if (entityType != null)
                {
                    Logger.LogDebug("{OperationName} completed successfully for {EntityType} {EntityId}",
                        operationName, entityType, entityId);
                }
                else
                {
                    Logger.LogDebug("{OperationName} completed successfully", operationName);
                }
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
        /// Captures the operation, entity context, user identity, client IP, and trace ID
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
            var adminUser = GetAdminUserIdentity();

            if (detail != null)
            {
                Logger.LogInformation(
                    "Admin Audit: {Operation} {EntityType} {EntityId} by {AdminUser} from {ClientIp} [TraceId: {TraceId}] - {Detail}",
                    operation,
                    entityType,
                    entityId ?? "N/A",
                    adminUser,
                    clientIp,
                    traceId,
                    LoggingSanitizer.S(detail));
            }
            else
            {
                Logger.LogInformation(
                    "Admin Audit: {Operation} {EntityType} {EntityId} by {AdminUser} from {ClientIp} [TraceId: {TraceId}]",
                    operation,
                    entityType,
                    entityId ?? "N/A",
                    adminUser,
                    clientIp,
                    traceId);
            }
        }

        /// <summary>
        /// Logs an admin audit event with before/after change tracking for update operations.
        /// </summary>
        /// <param name="entityType">The type of entity affected (e.g., "Provider", "VirtualKey").</param>
        /// <param name="entityId">The identifier of the affected entity.</param>
        /// <param name="changes">List of property changes with old and new values.</param>
        /// <param name="detail">Optional additional detail about the operation.</param>
        protected void LogAdminAuditWithChanges(
            string entityType,
            object? entityId,
            IReadOnlyList<(string Property, string? OldValue, string? NewValue)> changes,
            string? detail = null)
        {
            if (changes.Count == 0)
                return;

            var changeSummary = string.Join(", ", changes.Select(c =>
                $"{c.Property}: '{LoggingSanitizer.S(c.OldValue ?? "null")}' -> '{LoggingSanitizer.S(c.NewValue ?? "null")}'"));

            var fullDetail = detail != null
                ? $"{detail}; Changes: [{changeSummary}]"
                : $"Changes: [{changeSummary}]";

            LogAdminAudit("Updated", entityType, entityId, fullDetail);
        }

        /// <summary>
        /// Gets the admin user identity string for audit logging.
        /// Combines the authentication identity with any forwarded user ID from the WebAdmin.
        /// </summary>
        /// <returns>A string identifying the admin user (e.g., "AdminUser", "AdminUser (user:clerk_abc123)").</returns>
        private string GetAdminUserIdentity()
        {
            var identityName = User?.Identity?.Name ?? "Unknown";

            // Check for forwarded user identity from WebAdmin (Clerk user ID)
            var forwardedUserId = HttpContext?.Request?.Headers["X-Admin-User-Id"].FirstOrDefault();

            if (!string.IsNullOrEmpty(forwardedUserId))
            {
                return $"{identityName} (user:{LoggingSanitizer.S(forwardedUserId)})";
            }

            return identityName;
        }
    }
}
