using System;
using System.Threading.Tasks;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// Interface for the SpendNotificationHub that provides real-time spend tracking updates.
    /// </summary>
    public interface ISpendNotificationHub
    {
        /// <summary>
        /// Sends a spend update notification to connected clients.
        /// </summary>
        /// <param name="notification">The spend update notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SpendUpdate(SpendUpdateNotification notification);

        /// <summary>
        /// Sends a budget alert notification when thresholds are reached.
        /// </summary>
        /// <param name="alert">The budget alert notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task BudgetAlert(BudgetAlertNotification alert);

        /// <summary>
        /// Sends a spend summary notification.
        /// </summary>
        /// <param name="summary">The spend summary notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SpendSummary(SpendSummaryNotification summary);

        /// <summary>
        /// Sends a notification when unusual spending patterns are detected.
        /// </summary>
        /// <param name="notification">The unusual spending notification.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UnusualSpendingDetected(UnusualSpendingNotification notification);
    }

    /// <summary>
    /// Notification for individual spend updates.
    /// </summary>
    public class SpendUpdateNotification
    {
        /// <summary>
        /// Gets or sets the timestamp of the spend update.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the amount of the new spend.
        /// </summary>
        public decimal NewSpend { get; set; }

        /// <summary>
        /// Gets or sets the total spend amount.
        /// </summary>
        public decimal TotalSpend { get; set; }

        /// <summary>
        /// Gets or sets the budget amount if configured.
        /// </summary>
        public decimal? Budget { get; set; }

        /// <summary>
        /// Gets or sets the percentage of budget used.
        /// </summary>
        public decimal? BudgetPercentage { get; set; }

        /// <summary>
        /// Gets or sets the model that incurred the cost.
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the provider that incurred the cost.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional metadata about the request.
        /// </summary>
        public RequestMetadata? Metadata { get; set; }
    }

    /// <summary>
    /// Notification for budget threshold alerts.
    /// </summary>
    public class BudgetAlertNotification
    {
        /// <summary>
        /// Gets or sets the alert ID.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the timestamp of the alert.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the percentage of budget used.
        /// </summary>
        public decimal Percentage { get; set; }

        /// <summary>
        /// Gets or sets the remaining budget amount.
        /// </summary>
        public decimal Remaining { get; set; }

        /// <summary>
        /// Gets or sets the severity level (info, warning, critical).
        /// </summary>
        public string Severity { get; set; } = "info";

        /// <summary>
        /// Gets or sets the alert message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the budget period ends.
        /// </summary>
        public DateTime? BudgetPeriodEnd { get; set; }
    }

    /// <summary>
    /// Notification for spend summaries.
    /// </summary>
    public class SpendSummaryNotification
    {
        /// <summary>
        /// Gets or sets the summary ID.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the timestamp of the summary.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the period type (daily, weekly, monthly).
        /// </summary>
        public string PeriodType { get; set; } = "daily";

        /// <summary>
        /// Gets or sets the start date of the period.
        /// </summary>
        public DateTime PeriodStart { get; set; }

        /// <summary>
        /// Gets or sets the end date of the period.
        /// </summary>
        public DateTime PeriodEnd { get; set; }

        /// <summary>
        /// Gets or sets the total spend for the period.
        /// </summary>
        public decimal TotalSpend { get; set; }

        /// <summary>
        /// Gets or sets the number of requests in the period.
        /// </summary>
        public int RequestCount { get; set; }

        /// <summary>
        /// Gets or sets the breakdown by provider.
        /// </summary>
        public Dictionary<string, ProviderSpendBreakdown> ProviderBreakdown { get; set; } = new();

        /// <summary>
        /// Gets or sets the breakdown by model.
        /// </summary>
        public Dictionary<string, ModelSpendBreakdown> ModelBreakdown { get; set; } = new();

        /// <summary>
        /// Gets or sets the comparison with previous period.
        /// </summary>
        public PeriodComparison? Comparison { get; set; }
    }

    /// <summary>
    /// Notification for unusual spending patterns.
    /// </summary>
    public class UnusualSpendingNotification
    {
        /// <summary>
        /// Gets or sets the notification ID.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the timestamp of the detection.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the type of unusual pattern detected.
        /// </summary>
        public string PatternType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the unusual pattern.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the severity level.
        /// </summary>
        public string Severity { get; set; } = "warning";

        /// <summary>
        /// Gets or sets the current spend rate.
        /// </summary>
        public decimal CurrentSpendRate { get; set; }

        /// <summary>
        /// Gets or sets the normal spend rate.
        /// </summary>
        public decimal NormalSpendRate { get; set; }

        /// <summary>
        /// Gets or sets the percentage increase.
        /// </summary>
        public decimal PercentageIncrease { get; set; }

        /// <summary>
        /// Gets or sets recommended actions.
        /// </summary>
        public List<string> RecommendedActions { get; set; } = new();
    }

    /// <summary>
    /// Metadata about the request that incurred the cost.
    /// </summary>
    public class RequestMetadata
    {
        /// <summary>
        /// Gets or sets the request ID.
        /// </summary>
        public string? RequestId { get; set; }

        /// <summary>
        /// Gets or sets the endpoint used.
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// Gets or sets the number of input tokens.
        /// </summary>
        public int? InputTokens { get; set; }

        /// <summary>
        /// Gets or sets the number of output tokens.
        /// </summary>
        public int? OutputTokens { get; set; }
    }

    /// <summary>
    /// Provider spend breakdown information.
    /// </summary>
    public class ProviderSpendBreakdown
    {
        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total spend for this provider.
        /// </summary>
        public decimal TotalSpend { get; set; }

        /// <summary>
        /// Gets or sets the number of requests to this provider.
        /// </summary>
        public int RequestCount { get; set; }

        /// <summary>
        /// Gets or sets the percentage of total spend.
        /// </summary>
        public decimal Percentage { get; set; }
    }

    /// <summary>
    /// Model spend breakdown information.
    /// </summary>
    public class ModelSpendBreakdown
    {
        /// <summary>
        /// Gets or sets the model name.
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total spend for this model.
        /// </summary>
        public decimal TotalSpend { get; set; }

        /// <summary>
        /// Gets or sets the number of requests to this model.
        /// </summary>
        public int RequestCount { get; set; }

        /// <summary>
        /// Gets or sets the percentage of total spend.
        /// </summary>
        public decimal Percentage { get; set; }
    }

    /// <summary>
    /// Period comparison information.
    /// </summary>
    public class PeriodComparison
    {
        /// <summary>
        /// Gets or sets the previous period's total spend.
        /// </summary>
        public decimal PreviousPeriodSpend { get; set; }

        /// <summary>
        /// Gets or sets the change amount.
        /// </summary>
        public decimal ChangeAmount { get; set; }

        /// <summary>
        /// Gets or sets the change percentage.
        /// </summary>
        public decimal ChangePercentage { get; set; }

        /// <summary>
        /// Gets or sets the trend direction (up, down, stable).
        /// </summary>
        public string Trend { get; set; } = "stable";
    }
}