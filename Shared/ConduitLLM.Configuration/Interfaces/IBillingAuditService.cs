using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Service for auditing billing events and failures
    /// </summary>
    public interface IBillingAuditService
    {
        /// <summary>
        /// Logs a billing audit event asynchronously
        /// </summary>
        /// <param name="auditEvent">The audit event to log</param>
        /// <returns>Task representing the async operation</returns>
        Task LogBillingEventAsync(BillingAuditEvent auditEvent);

        /// <summary>
        /// Logs a billing audit event without waiting (fire-and-forget)
        /// </summary>
        /// <param name="auditEvent">The audit event to log</param>
        void LogBillingEvent(BillingAuditEvent auditEvent);

        /// <summary>
        /// Removes audit events older than the retention period (90 days)
        /// </summary>
        /// <returns>Task representing the cleanup operation</returns>
        Task CleanupOldAuditEventsAsync();
    }

}
