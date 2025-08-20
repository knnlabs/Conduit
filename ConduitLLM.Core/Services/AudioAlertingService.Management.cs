using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    public partial class AudioAlertingService
    {
        /// <inheritdoc />
        public Task<List<TriggeredAlert>> GetAlertHistoryAsync(
            DateTime startTime,
            DateTime endTime,
            AlertSeverity? severity = null)
        {
            lock (_historyLock)
            {
                var filtered = _alertHistory
                    .Where(a => a.TriggeredAt >= startTime && a.TriggeredAt <= endTime)
                    .Where(a => severity == null || a.Rule.Severity == severity)
                    .OrderByDescending(a => a.TriggeredAt)
                    .ToList();

                return Task.FromResult(filtered);
            }
        }

        /// <inheritdoc />
        public Task AcknowledgeAlertAsync(
            string alertId,
            string acknowledgedBy,
            string? notes = null)
        {
            lock (_historyLock)
            {
                var alert = _alertHistory.FirstOrDefault(a => a.Id == alertId);
                if (alert != null)
                {
                    alert.State = AlertState.Acknowledged;
                    alert.AcknowledgedBy = acknowledgedBy;
                    alert.AcknowledgedAt = DateTime.UtcNow;
                    alert.AcknowledgmentNotes = notes;

                    _logger.LogInformation(
                        "Alert {AlertId} acknowledged by {User}",
                        alertId, acknowledgedBy);
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task<AlertTestResult> TestAlertRuleAsync(AudioAlertRule rule)
        {
            var result = new AlertTestResult
            {
                Success = true,
                Message = "Alert rule test completed"
            };

            try
            {
                // Simulate metric value
                var testMetrics = CreateTestMetrics(rule.MetricType);
                var metricValue = ExtractMetricValue(rule.MetricType, testMetrics);

                result.SimulatedMetricValue = metricValue;
                result.WouldTrigger = EvaluateCondition(rule.Condition, metricValue);

                // Test notification channels
                foreach (var channel in rule.NotificationChannels)
                {
                    var notificationTest = await TestNotificationChannelAsync(channel);
                    result.NotificationTests.Add(notificationTest);
                }

                if (result.WouldTrigger)
                {
                    result.Message = $"Alert would trigger: {rule.Name} (value: {metricValue})";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Test failed: {ex.Message}";
            }

            return result;
        }

        private AudioMetricsSnapshot CreateTestMetrics(AudioMetricType metricType)
        {
            return new AudioMetricsSnapshot
            {
                Timestamp = DateTime.UtcNow,
                ActiveTranscriptions = 5,
                ActiveTtsOperations = 3,
                ActiveRealtimeSessions = 10,
                RequestsPerSecond = 25.5,
                CurrentErrorRate = 0.02,
                ProviderHealth = new Dictionary<string, bool>
                {
                    ["openai"] = true,
                    ["elevenlabs"] = true,
                    ["deepgram"] = false
                },
                Resources = new SystemResources
                {
                    CpuUsagePercent = 45.2,
                    MemoryUsageMb = 2048,
                    ActiveConnections = 75,
                    CacheSizeMb = 512
                }
            };
        }
    }

    /// <summary>
    /// Options for audio alerting service.
    /// </summary>
    public class AudioAlertingOptions
    {
        /// <summary>
        /// Gets or sets the maximum alert history size.
        /// </summary>
        public int MaxHistorySize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the default cooldown period.
        /// </summary>
        public TimeSpan DefaultCooldownPeriod { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the evaluation interval.
        /// </summary>
        public TimeSpan EvaluationInterval { get; set; } = TimeSpan.FromMinutes(1);
    }
}