using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Controllers;
using ConduitLLM.Core.Extensions;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Base class for Admin API controllers providing event publishing and
    /// admin audit logging.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Extends <see cref="EventPublishingControllerBase"/> with fire-and-forget event publishing
    /// through the Conduit-owned <see cref="IEventBus"/> abstraction and structured admin audit logging.
    /// </para>
    /// <para>
    /// Error handling is delegated to the global <c>AdminExceptionMiddleware</c> (thrown exceptions
    /// are mapped to standardized <c>ErrorResponseDto</c> responses via <c>ExceptionToResponseMapper</c>),
    /// and per-action success logging is provided by <c>OperationLoggingFilter</c>. The former per-action
    /// <c>ExecuteAsync</c>/<c>ExecuteWithNotFoundAsync</c> wrappers were removed in the Tier 1a cleanup (#902).
    /// </para>
    /// </remarks>
    public abstract class AdminControllerBase : EventPublishingControllerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AdminControllerBase"/> class.
        /// </summary>
        /// <param name="eventBus">Optional event bus for event publishing.</param>
        /// <param name="logger">The logger instance for the derived controller.</param>
        protected AdminControllerBase(
            IEventBus? eventBus,
            ILogger logger)
            : base(eventBus, logger)
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
        /// Logs an audit event for bulk/import operations with success and failure counts.
        /// </summary>
        /// <param name="operation">The bulk operation (e.g., "ImportedCsv", "BulkCreated").</param>
        /// <param name="entityType">The type of entity affected.</param>
        /// <param name="successCount">Number of successfully processed items.</param>
        /// <param name="failureCount">Number of failed items.</param>
        protected void LogAdminAuditBulk(
            string operation,
            string entityType,
            int successCount,
            int failureCount)
        {
            LogAdminAudit(operation, entityType, detail: $"Success: {successCount}, Failures: {failureCount}");
        }

        /// <summary>
        /// Logs an audit event for state/toggle changes on an entity.
        /// </summary>
        /// <param name="entityType">The type of entity affected.</param>
        /// <param name="entityId">The identifier of the affected entity (null for global settings).</param>
        /// <param name="property">The property being changed (e.g., "Enabled").</param>
        /// <param name="newValue">The new value of the property.</param>
        protected void LogAdminAuditStateChange(
            string entityType,
            object? entityId,
            string property,
            object newValue)
        {
            LogAdminAudit("Updated", entityType, entityId, $"{property}: {newValue}");
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
