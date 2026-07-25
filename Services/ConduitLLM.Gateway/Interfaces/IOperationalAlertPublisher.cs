namespace ConduitLLM.Gateway.Interfaces
{
    /// <summary>
    /// Severity of an operational alert, ordered from least to most urgent.
    /// </summary>
    public enum OperationalAlertSeverity
    {
        /// <summary>Informational; no action expected.</summary>
        Info,

        /// <summary>Degraded but functioning; worth investigating.</summary>
        Warning,

        /// <summary>An operation failed and needs attention.</summary>
        Error,

        /// <summary>Correctness or availability is at risk; page someone.</summary>
        Critical
    }

    /// <summary>
    /// Publishes operational alerts raised by business services.
    /// </summary>
    /// <remarks>
    /// Alerting itself lives in Prometheus/Grafana. This abstraction only records that an alert
    /// condition occurred: the detail goes to the structured log, and a counter is exported so
    /// Grafana alert rules can fire on the rate of alerts per component and severity.
    /// </remarks>
    public interface IOperationalAlertPublisher
    {
        /// <summary>
        /// Records an operational alert.
        /// </summary>
        /// <param name="severity">How urgent the condition is.</param>
        /// <param name="component">Component raising the alert; becomes a metric label, so keep it low-cardinality.</param>
        /// <param name="title">Short summary of the condition.</param>
        /// <param name="message">Human-readable detail.</param>
        /// <param name="context">Optional structured context included in the log entry.</param>
        void Raise(
            OperationalAlertSeverity severity,
            string component,
            string title,
            string message,
            IReadOnlyDictionary<string, object>? context = null);
    }
}
