using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    public partial class AudioAlertingService
    {
        /// <inheritdoc />
        public async Task EvaluateMetricsAsync(
            AudioMetricsSnapshot metrics,
            CancellationToken cancellationToken = default)
        {
            await _evaluationSemaphore.WaitAsync(cancellationToken);
            try
            {
                var activeRules = await GetActiveRulesAsync();

                foreach (var rule in activeRules)
                {
                    try
                    {
                        await EvaluateRuleAsync(rule, metrics, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error evaluating alert rule {RuleId}", rule.Id);
                    }
                }
            }
            finally
            {
                _evaluationSemaphore.Release();
            }
        }

        private async Task EvaluateRuleAsync(
            AudioAlertRule rule,
            AudioMetricsSnapshot metrics,
            CancellationToken cancellationToken)
        {
            // Check cooldown period
            if (_lastAlertTimes.TryGetValue(rule.Id, out var lastAlert))
            {
                if (DateTime.UtcNow - lastAlert < rule.CooldownPeriod)
                {
                    return; // Still in cooldown
                }
            }

            // Extract metric value
            var metricValue = ExtractMetricValue(rule.MetricType, metrics);

            // Evaluate condition
            if (!EvaluateCondition(rule.Condition, metricValue))
            {
                return; // Condition not met
            }

            // Create triggered alert
            var alert = new TriggeredAlert
            {
                Rule = rule,
                MetricValue = metricValue,
                Message = FormatAlertMessage(rule, metricValue),
                Details = new Dictionary<string, object>
                {
                    ["metric_type"] = rule.MetricType.ToString(),
                    ["threshold"] = rule.Condition.Threshold,
                    ["actual_value"] = metricValue,
                    ["timestamp"] = metrics.Timestamp
                },
                State = AlertState.Active
            };

            // Add to history
            lock (_historyLock)
            {
                _alertHistory.Add(alert);

                // Trim old history
                if (_alertHistory.Count() > _options.MaxHistorySize)
                {
                    _alertHistory.RemoveAt(0);
                }
            }

            // Update last alert time
            _lastAlertTimes[rule.Id] = DateTime.UtcNow;

            // Send notifications
            await SendNotificationsAsync(alert, cancellationToken);

            _logger.LogWarning(
                "Alert triggered: {AlertName} - {Message}",
                rule.Name, alert.Message);
        }

        private double ExtractMetricValue(AudioMetricType metricType, AudioMetricsSnapshot metrics)
        {
            return metricType switch
            {
                AudioMetricType.ErrorRate => metrics.CurrentErrorRate,
                AudioMetricType.Latency => 0, // Would need historical data
                AudioMetricType.ProviderAvailability => metrics.ProviderHealth.Count(p => p.Value) / (double)Math.Max(1, metrics.ProviderHealth.Count()),
                AudioMetricType.CacheHitRate => 0, // Would need cache metrics
                AudioMetricType.ActiveSessions => metrics.ActiveRealtimeSessions,
                AudioMetricType.RequestRate => metrics.RequestsPerSecond,
                AudioMetricType.CostRate => 0, // Would need cost data
                AudioMetricType.ConnectionPoolUtilization => metrics.Resources.ActiveConnections / 100.0,
                AudioMetricType.QueueLength => 0, // Would need queue metrics
                _ => 0
            };
        }

        private bool EvaluateCondition(AlertCondition condition, double value)
        {
            var result = condition.Operator switch
            {
                ComparisonOperator.GreaterThan => value > condition.Threshold,
                ComparisonOperator.LessThan => value < condition.Threshold,
                ComparisonOperator.Equals => Math.Abs(value - condition.Threshold) < 0.001,
                ComparisonOperator.NotEquals => Math.Abs(value - condition.Threshold) >= 0.001,
                ComparisonOperator.GreaterThanOrEqual => value >= condition.Threshold,
                ComparisonOperator.LessThanOrEqual => value <= condition.Threshold,
                _ => false
            };

            return result;
        }

        private string FormatAlertMessage(AudioAlertRule rule, double metricValue)
        {
            return $"{rule.Name}: {rule.MetricType} is {metricValue:F2} " +
                   $"(threshold: {rule.Condition.Operator} {rule.Condition.Threshold:F2})";
        }
    }
}