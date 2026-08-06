using ConduitLLM.Gateway.Interfaces;
using Prometheus;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Records operational alerts to the structured log and to a Prometheus counter.
    /// </summary>
    /// <remarks>
    /// This replaces the former in-process alert management pipeline (Redis-backed alert store,
    /// rules, suppressions, acknowledgement workflow and webhook/email/Slack fan-out). None of
    /// that was consumed: the dashboard that read it was removed, and Grafana already owns
    /// alert routing. What business callers actually need is for the condition to be visible,
    /// which the log entry and the counter provide.
    /// </remarks>
    public sealed class OperationalAlertPublisher : IOperationalAlertPublisher
    {
        private static readonly Counter Alerts = Prometheus.Metrics.CreateCounter(
            "conduit_operational_alerts_total",
            "Operational alerts raised by business services.",
            "component",
            "severity");

        private readonly ILogger<OperationalAlertPublisher> _logger;

        public OperationalAlertPublisher(ILogger<OperationalAlertPublisher> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public void Raise(
            OperationalAlertSeverity severity,
            string component,
            string title,
            string message,
            IReadOnlyDictionary<string, object>? context = null)
        {
            Alerts.WithLabels(component, severity.ToString()).Inc();

            var logLevel = severity switch
            {
                OperationalAlertSeverity.Critical => LogLevel.Critical,
                OperationalAlertSeverity.Error => LogLevel.Error,
                OperationalAlertSeverity.Warning => LogLevel.Warning,
                _ => LogLevel.Information
            };

            // Context is serialized rather than scoped so the detail survives in log sinks that
            // do not capture scopes.
            _logger.Log(
                logLevel,
                "Operational alert [{Severity}] {Component}: {Title} - {Message} {AlertContext}",
                severity,
                component,
                title,
                message,
                context is { Count: > 0 }
                    ? string.Join(", ", context.Select(pair => $"{pair.Key}={pair.Value}"))
                    : string.Empty);
        }
    }
}
