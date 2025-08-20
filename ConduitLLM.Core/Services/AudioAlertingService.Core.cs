using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Manages audio operation alerts and notifications.
    /// </summary>
    public partial class AudioAlertingService : IAudioAlertingService
    {
        private readonly ILogger<AudioAlertingService> _logger;
        private readonly AudioAlertingOptions _options;
        private readonly ConcurrentDictionary<string, AudioAlertRule> _alertRules = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastAlertTimes = new();
        private readonly List<TriggeredAlert> _alertHistory = new();
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _evaluationSemaphore = new(1);
        private readonly object _historyLock = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioAlertingService"/> class.
        /// </summary>
        public AudioAlertingService(
            ILogger<AudioAlertingService> logger,
            IOptions<AudioAlertingOptions> options,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _httpClient = httpClientFactory?.CreateClient("AlertingService") ?? throw new ArgumentNullException(nameof(httpClientFactory));

            // Load default alert rules
            LoadDefaultRules();
        }

        /// <inheritdoc />
        public Task<string> RegisterAlertRuleAsync(AudioAlertRule rule)
        {
            ArgumentNullException.ThrowIfNull(rule);

            if (string.IsNullOrEmpty(rule.Id))
                rule.Id = Guid.NewGuid().ToString();

            _alertRules[rule.Id] = rule;

            _logger.LogInformation(
                "Registered alert rule: {RuleName} ({RuleId}) for metric {MetricType}",
                rule.Name, rule.Id, rule.MetricType);

            return Task.FromResult(rule.Id);
        }

        /// <inheritdoc />
        public Task UpdateAlertRuleAsync(string ruleId, AudioAlertRule rule)
        {
            if (string.IsNullOrEmpty(ruleId))
                throw new ArgumentException("Rule ID cannot be empty", nameof(ruleId));

            ArgumentNullException.ThrowIfNull(rule);

            if (!_alertRules.ContainsKey(ruleId))
                throw new InvalidOperationException($"Alert rule {ruleId} not found");

            rule.Id = ruleId;
            _alertRules[ruleId] = rule;

            _logger.LogInformation("Updated alert rule: {RuleId}", ruleId);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task DeleteAlertRuleAsync(string ruleId)
        {
            if (_alertRules.TryRemove(ruleId, out var rule))
            {
                _logger.LogInformation("Deleted alert rule: {RuleName} ({RuleId})", rule.Name, ruleId);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<List<AudioAlertRule>> GetActiveRulesAsync()
        {
            var activeRules = _alertRules.Values
                .Where(r => r.IsEnabled)
                .ToList();

            return Task.FromResult(activeRules);
        }

        private void LoadDefaultRules()
        {
            // High error rate alert
            _ = RegisterAlertRuleAsync(new AudioAlertRule
            {
                Name = "High Error Rate",
                Description = "Alert when error rate exceeds 5%",
                MetricType = AudioMetricType.ErrorRate,
                Condition = new AlertCondition
                {
                    Operator = ComparisonOperator.GreaterThan,
                    Threshold = 0.05,
                    TimeWindow = TimeSpan.FromMinutes(5),
                    MinimumOccurrences = 2
                },
                Severity = AlertSeverity.Error,
                IsEnabled = true
            }).Result;

            // Provider down alert
            _ = RegisterAlertRuleAsync(new AudioAlertRule
            {
                Name = "Provider Availability Low",
                Description = "Alert when provider availability drops below 50%",
                MetricType = AudioMetricType.ProviderAvailability,
                Condition = new AlertCondition
                {
                    Operator = ComparisonOperator.LessThan,
                    Threshold = 0.5,
                    TimeWindow = TimeSpan.FromMinutes(2)
                },
                Severity = AlertSeverity.Critical,
                IsEnabled = true
            }).Result;

            // High request rate alert
            _ = RegisterAlertRuleAsync(new AudioAlertRule
            {
                Name = "High Request Rate",
                Description = "Alert when request rate exceeds 100 RPS",
                MetricType = AudioMetricType.RequestRate,
                Condition = new AlertCondition
                {
                    Operator = ComparisonOperator.GreaterThan,
                    Threshold = 100,
                    TimeWindow = TimeSpan.FromMinutes(1)
                },
                Severity = AlertSeverity.Warning,
                IsEnabled = true
            }).Result;
        }
    }
}