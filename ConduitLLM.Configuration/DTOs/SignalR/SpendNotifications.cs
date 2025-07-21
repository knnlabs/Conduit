using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.SignalR
{
    /// <summary>
    /// Notification for real-time spend updates.
    /// </summary>
    public class SpendUpdateNotification
    {
        /// <summary>
        /// Gets or sets the timestamp of the update.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the total spend amount.
        /// </summary>
        public decimal TotalSpend { get; set; }

        /// <summary>
        /// Gets or sets the budget remaining.
        /// </summary>
        public decimal BudgetRemaining { get; set; }

        /// <summary>
        /// Gets or sets the budget utilization percentage.
        /// </summary>
        public double BudgetUtilization { get; set; }

        /// <summary>
        /// Gets or sets the budget amount if configured.
        /// </summary>
        public decimal? Budget { get; set; }

        /// <summary>
        /// Gets or sets the percentage of budget used.
        /// </summary>
        public decimal? BudgetPercentage { get; set; }

        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the model used.
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the amount of new spend.
        /// </summary>
        public decimal NewSpend { get; set; }

        /// <summary>
        /// Gets or sets the request metadata.
        /// </summary>
        public RequestMetadata? Metadata { get; set; }
    }

    /// <summary>
    /// Notification for budget alerts.
    /// </summary>
    public class BudgetAlertNotification
    {
        /// <summary>
        /// Gets or sets the timestamp of the alert.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the alert type.
        /// </summary>
        public string AlertType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the alert message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current spend amount.
        /// </summary>
        public decimal CurrentSpend { get; set; }

        /// <summary>
        /// Gets or sets the budget limit.
        /// </summary>
        public decimal BudgetLimit { get; set; }

        /// <summary>
        /// Gets or sets the percentage of budget used.
        /// </summary>
        public double PercentageUsed { get; set; }

        /// <summary>
        /// Gets or sets the alert severity.
        /// </summary>
        public string Severity { get; set; } = "warning";

        /// <summary>
        /// Gets or sets recommendations for the alert.
        /// </summary>
        public List<string> Recommendations { get; set; } = new();
        
        
        /// <summary>
        /// Gets or sets the budget period end date.
        /// </summary>
        public DateTime? BudgetPeriodEnd { get; set; }
    }

    /// <summary>
    /// Notification for spend summaries.
    /// </summary>
    public class SpendSummaryNotification
    {
        /// <summary>
        /// Gets or sets the timestamp of the summary.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the period for this summary.
        /// </summary>
        public string Period { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the period type (hourly, daily, weekly, monthly).
        /// </summary>
        public string PeriodType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total spend.
        /// </summary>
        public decimal TotalSpend { get; set; }

        /// <summary>
        /// Gets or sets the request count.
        /// </summary>
        public int RequestCount { get; set; }

        /// <summary>
        /// Gets or sets the average cost per request.
        /// </summary>
        public decimal AverageRequestCost { get; set; }

        /// <summary>
        /// Gets or sets the top providers by spend.
        /// </summary>
        public List<ProviderSpendBreakdown> TopProviders { get; set; } = new();

        /// <summary>
        /// Gets or sets the top models by spend.
        /// </summary>
        public List<ModelSpendBreakdown> TopModels { get; set; } = new();

        /// <summary>
        /// Gets or sets the period comparison.
        /// </summary>
        public PeriodComparison? Comparison { get; set; }
    }

    /// <summary>
    /// Notification for unusual spending patterns.
    /// </summary>
    public class UnusualSpendingNotification
    {
        /// <summary>
        /// Gets or sets the timestamp of the detection.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the type of unusual activity.
        /// </summary>
        public string ActivityType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the unusual activity.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current rate.
        /// </summary>
        public decimal CurrentRate { get; set; }

        /// <summary>
        /// Gets or sets the normal rate.
        /// </summary>
        public decimal NormalRate { get; set; }

        /// <summary>
        /// Gets or sets the deviation percentage.
        /// </summary>
        public double DeviationPercentage { get; set; }

        /// <summary>
        /// Gets or sets the provider involved.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Gets or sets the model involved.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Gets or sets recommendations.
        /// </summary>
        public List<string> Recommendations { get; set; } = new();
        
    }

    /// <summary>
    /// Metadata for a request.
    /// </summary>
    public class RequestMetadata
    {
        /// <summary>
        /// Gets or sets the request ID.
        /// </summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the endpoint.
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the input token count.
        /// </summary>
        public int? InputTokens { get; set; }

        /// <summary>
        /// Gets or sets the output token count.
        /// </summary>
        public int? OutputTokens { get; set; }

        /// <summary>
        /// Gets or sets the request duration in milliseconds.
        /// </summary>
        public double? DurationMs { get; set; }
    }

    /// <summary>
    /// Provider spend breakdown.
    /// </summary>
    public class ProviderSpendBreakdown
    {
        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the spend amount.
        /// </summary>
        public decimal Spend { get; set; }

        /// <summary>
        /// Gets or sets the percentage of total.
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// Gets or sets the request count.
        /// </summary>
        public int RequestCount { get; set; }
    }

    /// <summary>
    /// Model spend breakdown.
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
        /// Gets or sets the spend amount.
        /// </summary>
        public decimal Spend { get; set; }

        /// <summary>
        /// Gets or sets the percentage of total.
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// Gets or sets the request count.
        /// </summary>
        public int RequestCount { get; set; }
    }

    /// <summary>
    /// Period comparison data.
    /// </summary>
    public class PeriodComparison
    {
        /// <summary>
        /// Gets or sets the previous period spend.
        /// </summary>
        public decimal PreviousPeriodSpend { get; set; }

        /// <summary>
        /// Gets or sets the change amount.
        /// </summary>
        public decimal ChangeAmount { get; set; }

        /// <summary>
        /// Gets or sets the change percentage.
        /// </summary>
        public double ChangePercentage { get; set; }

        /// <summary>
        /// Gets or sets the trend direction.
        /// </summary>
        public string Trend { get; set; } = string.Empty;
    }
}