using ConduitLLM.Core.Extensions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Auditing
{
    /// <summary>
    /// Structured admin audit logging usable from Minimal-API handlers (which have no
    /// <c>AdminControllerBase</c> to inherit <c>LogAdminAudit</c> from). Mirrors the controller
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
