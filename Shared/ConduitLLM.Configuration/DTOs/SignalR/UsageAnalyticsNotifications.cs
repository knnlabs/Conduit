namespace ConduitLLM.Configuration.DTOs.SignalR
{
    /// <summary>
    /// Notification containing real-time usage metrics.
    /// </summary>
    public class UsageMetricsNotification
    {
        /// <summary>
        /// Gets or sets the time period for these metrics.
        /// </summary>
        public string Period { get; set; } = "minute";

        /// <summary>
        /// Gets or sets the number of requests per minute.
        /// </summary>
        public int RequestsPerMinute { get; set; }

        /// <summary>
        /// Gets or sets the number of tokens processed per minute.
        /// </summary>
        public long TokensPerMinute { get; set; }

        /// <summary>
        /// Gets or sets the number of unique models used.
        /// </summary>
        public int UniqueModelsUsed { get; set; }

        /// <summary>
        /// Gets or sets the most used model.
        /// </summary>
        public string? MostUsedModel { get; set; }

        /// <summary>
        /// Gets or sets the breakdown by provider.
        /// </summary>
        public Dictionary<string, int> RequestsByProvider { get; set; } = new();

        /// <summary>
        /// Gets or sets the breakdown by model.
        /// </summary>
        public Dictionary<string, int> RequestsByModel { get; set; } = new();

        /// <summary>
        /// Gets or sets the timestamp of the metrics.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Notification containing real-time cost analytics.
    /// </summary>
    public class CostAnalyticsNotification
    {
        /// <summary>
        /// Gets or sets the total cost incurred.
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Gets or sets the cost in the last hour.
        /// </summary>
        public decimal CostLastHour { get; set; }

        /// <summary>
        /// Gets or sets the cost per hour rate.
        /// </summary>
        public decimal CostPerHour { get; set; }

        /// <summary>
        /// Gets or sets the projected daily cost.
        /// </summary>
        public decimal ProjectedDailyCost { get; set; }

        /// <summary>
        /// Gets or sets the projected monthly cost.
        /// </summary>
        public decimal ProjectedMonthlyCost { get; set; }

        /// <summary>
        /// Gets or sets the cost breakdown by provider.
        /// </summary>
        public Dictionary<string, decimal> CostByProvider { get; set; } = new();

        /// <summary>
        /// Gets or sets the cost breakdown by model.
        /// </summary>
        public Dictionary<string, decimal> CostByModel { get; set; } = new();

        /// <summary>
        /// Gets or sets the most expensive request details.
        /// </summary>
        public ExpensiveRequestDetails? MostExpensiveRequest { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the analytics.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Details about an expensive request.
    /// </summary>
    public class ExpensiveRequestDetails
    {
        /// <summary>
        /// Gets or sets the request ID.
        /// </summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the model used.
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the cost of the request.
        /// </summary>
        public decimal Cost { get; set; }

        /// <summary>
        /// Gets or sets the number of tokens.
        /// </summary>
        public int Tokens { get; set; }

        /// <summary>
        /// Gets or sets when the request occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Notification containing model performance metrics.
    /// </summary>
    public class PerformanceMetricsNotification
    {
        /// <summary>
        /// Gets or sets the model name.
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the provider type.
        /// </summary>
        public ProviderType ProviderType { get; set; }

        /// <summary>
        /// Gets or sets the average latency in milliseconds.
        /// </summary>
        public double AverageLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the minimum latency in milliseconds.
        /// </summary>
        public double MinLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the maximum latency in milliseconds.
        /// </summary>
        public double MaxLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the 95th percentile latency.
        /// </summary>
        public double P95LatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the 99th percentile latency.
        /// </summary>
        public double P99LatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the success rate (0.0 to 1.0).
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Gets or sets the error rate (0.0 to 1.0).
        /// </summary>
        public double ErrorRate { get; set; }

        /// <summary>
        /// Gets or sets the timeout rate (0.0 to 1.0).
        /// </summary>
        public double TimeoutRate { get; set; }

        /// <summary>
        /// Gets or sets the average tokens per second.
        /// </summary>
        public double AverageTokensPerSecond { get; set; }

        /// <summary>
        /// Gets or sets the number of requests in the sample.
        /// </summary>
        public int SampleSize { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the metrics.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Notification containing error analytics.
    /// </summary>
    public class ErrorAnalyticsNotification
    {
        /// <summary>
        /// Gets or sets the total number of errors.
        /// </summary>
        public int TotalErrors { get; set; }

        /// <summary>
        /// Gets or sets the error rate (0.0 to 1.0).
        /// </summary>
        public double ErrorRate { get; set; }

        /// <summary>
        /// Gets or sets errors by type.
        /// </summary>
        public Dictionary<string, int> ErrorsByType { get; set; } = new();

        /// <summary>
        /// Gets or sets errors by provider.
        /// </summary>
        public Dictionary<string, int> ErrorsByProvider { get; set; } = new();

        /// <summary>
        /// Gets or sets errors by model.
        /// </summary>
        public Dictionary<string, int> ErrorsByModel { get; set; } = new();

        /// <summary>
        /// Gets or sets the most common error details.
        /// </summary>
        public List<CommonErrorDetails> CommonErrors { get; set; } = new();

        /// <summary>
        /// Gets or sets the time period for these analytics.
        /// </summary>
        public string Period { get; set; } = "hour";

        /// <summary>
        /// Gets or sets the timestamp of the analytics.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Details about common errors.
    /// </summary>
    public class CommonErrorDetails
    {
        /// <summary>
        /// Gets or sets the error type.
        /// </summary>
        public string ErrorType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the occurrence count.
        /// </summary>
        public int Occurrences { get; set; }

        /// <summary>
        /// Gets or sets the affected model.
        /// </summary>
        public string? AffectedModel { get; set; }

        /// <summary>
        /// Gets or sets the affected provider type.
        /// </summary>
        public ProviderType? AffectedProviderType { get; set; }

        /// <summary>
        /// Gets or sets when this error first occurred.
        /// </summary>
        public DateTime FirstOccurrence { get; set; }

        /// <summary>
        /// Gets or sets when this error last occurred.
        /// </summary>
        public DateTime LastOccurrence { get; set; }
    }

    /// <summary>
    /// Analytics summary notification.
    /// </summary>
    public class AnalyticsSummaryNotification
    {
        /// <summary>
        /// Gets or sets the virtual key ID.
        /// </summary>
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// Gets or sets the list of subscribed analytics types.
        /// </summary>
        public List<string> SubscribedAnalytics { get; set; } = new();

        /// <summary>
        /// Gets or sets the summary message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}