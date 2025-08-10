using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.HealthMonitoring;

namespace ConduitLLM.Http.Interfaces
{
    /// <summary>
    /// Interface for managing health alerts and notifications
    /// </summary>
    public interface IAlertManagementService
    {
        /// <summary>
        /// Get all active alerts
        /// </summary>
        Task<List<HealthAlert>> GetActiveAlertsAsync();

        /// <summary>
        /// Get real-time alert stream
        /// </summary>
        IAsyncEnumerable<HealthAlert> GetAlertStreamAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Acknowledge an alert
        /// </summary>
        Task<bool> AcknowledgeAlertAsync(string alertId, string user, string? notes);

        /// <summary>
        /// Resolve an alert
        /// </summary>
        Task<bool> ResolveAlertAsync(string alertId, string user, string? resolution);

        /// <summary>
        /// Get alert history
        /// </summary>
        Task<List<AlertHistoryEntry>> GetAlertHistoryAsync(string alertId);

        /// <summary>
        /// Save or update an alert rule
        /// </summary>
        Task<AlertRule> SaveAlertRuleAsync(AlertRule rule);

        /// <summary>
        /// Delete an alert rule
        /// </summary>
        Task<bool> DeleteAlertRuleAsync(string ruleId);

        /// <summary>
        /// Get all alert rules
        /// </summary>
        Task<List<AlertRule>> GetAlertRulesAsync();

        /// <summary>
        /// Create alert suppression
        /// </summary>
        Task<AlertSuppression> CreateSuppressionAsync(AlertSuppression suppression);

        /// <summary>
        /// Cancel alert suppression
        /// </summary>
        Task<bool> CancelSuppressionAsync(string suppressionId);

        /// <summary>
        /// Get active suppressions
        /// </summary>
        Task<List<AlertSuppression>> GetActiveSuppressionsAsync();

        /// <summary>
        /// Trigger a new alert
        /// </summary>
        Task TriggerAlertAsync(HealthAlert alert);
    }
}