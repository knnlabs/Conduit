using ConduitLLM.Core.Extensions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Auditing
{
    /// <summary>
    /// Structured admin audit logging usable from Minimal-API handlers (which have no
    /// a shared MVC base to inherit audit helpers from). Mirrors the former behavior
    /// base's audit format: operation, entity context, admin user identity, client IP, and trace id.
    /// </summary>
    /// <remarks>Introduced for the Tier 3 Minimal-API pilot (#906).</remarks>
    public static class AdminAudit
    {
        /// <summary>
        /// Logs a security-sensitive admin operation for audit purposes.
        /// </summary>
        public static void Log(
            HttpContext httpContext,
            ILogger logger,
            string operation,
            string entityType,
            object? entityId = null,
            string? detail = null)
        {
            var clientIp = httpContext.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
            var traceId = httpContext.TraceIdentifier ?? "unknown";
            var adminUser = GetAdminUserIdentity(httpContext);

            if (detail != null)
            {
                logger.LogInformation(
                    "Admin Audit: {Operation} {EntityType} {EntityId} by {AdminUser} from {ClientIp} [TraceId: {TraceId}] - {Detail}",
                    operation, entityType, entityId ?? "N/A", adminUser, clientIp, traceId, LoggingSanitizer.S(detail));
            }
            else
            {
                logger.LogInformation(
                    "Admin Audit: {Operation} {EntityType} {EntityId} by {AdminUser} from {ClientIp} [TraceId: {TraceId}]",
                    operation, entityType, entityId ?? "N/A", adminUser, clientIp, traceId);
            }
        }

        /// <summary>Logs a bulk operation with success and failure counts.</summary>
        public static void LogBulk(
            HttpContext httpContext,
            ILogger logger,
            string operation,
            string entityType,
            int successCount,
            int failureCount) =>
            Log(httpContext, logger, operation, entityType,
                detail: $"Success: {successCount}, Failures: {failureCount}");

        /// <summary>Logs an update with sanitized before/after values.</summary>
        public static void LogWithChanges(
            HttpContext httpContext,
            ILogger logger,
            string entityType,
            object? entityId,
            IReadOnlyList<(string Property, string? OldValue, string? NewValue)> changes,
            string? detail = null)
        {
            if (changes.Count == 0)
            {
                return;
            }
            var summary = string.Join(", ", changes.Select(change =>
                $"{change.Property}: '{LoggingSanitizer.S(change.OldValue ?? "null")}' -> " +
                $"'{LoggingSanitizer.S(change.NewValue ?? "null")}'"));
            Log(httpContext, logger, "Updated", entityType, entityId,
                detail is null ? $"Changes: [{summary}]" : $"{detail}; Changes: [{summary}]");
        }

        /// <summary>Logs a single state transition.</summary>
        public static void LogStateChange(
            HttpContext httpContext,
            ILogger logger,
            string entityType,
            object? entityId,
            string property,
            object newValue) =>
            Log(httpContext, logger, "Updated", entityType, entityId, $"{property}: {newValue}");

        private static string GetAdminUserIdentity(HttpContext httpContext)
        {
            var identityName = httpContext.User?.Identity?.Name ?? "Unknown";
            var forwardedUserId = httpContext.Request?.Headers["X-Admin-User-Id"].FirstOrDefault();

            if (!string.IsNullOrEmpty(forwardedUserId))
            {
                return $"{identityName} (user:{LoggingSanitizer.S(forwardedUserId)})";
            }

            return identityName;
        }
    }
}
