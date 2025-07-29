using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Implementation of spend notification service.
    /// </summary>
    public class SpendNotificationService : ISpendNotificationService, IHostedService
    {
        private readonly IHubContext<SpendNotificationHub> _hubContext;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<SpendNotificationService> _logger;
        
        // Track spending patterns per virtual key
        private readonly ConcurrentDictionary<int, SpendingPattern> _spendingPatterns = new();
        
        // Track budget alert thresholds already sent to avoid spam
        private readonly ConcurrentDictionary<int, HashSet<int>> _sentBudgetAlerts = new();
        
        // Timer for periodic pattern analysis
        private Timer? _patternAnalysisTimer;
        private readonly TimeSpan _analysisInterval = TimeSpan.FromMinutes(5);

        public SpendNotificationService(
            IHubContext<SpendNotificationHub> hubContext,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<SpendNotificationService> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _patternAnalysisTimer = new Timer(
                AnalyzeSpendingPatterns,
                null,
                _analysisInterval,
                _analysisInterval);
            
            _logger.LogInformation("SpendNotificationService started");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _patternAnalysisTimer?.Dispose();
            _logger.LogInformation("SpendNotificationService stopped");
            return Task.CompletedTask;
        }

        public async Task NotifySpendUpdateAsync(
            int virtualKeyId, 
            decimal amount, 
            decimal totalSpend, 
            decimal? budget,
            string model,
            string provider)
        {
            try
            {
                // Record the spend for pattern analysis
                RecordSpend(virtualKeyId, amount);

                // Calculate budget percentage if budget is set
                decimal? budgetPercentage = null;
                if (budget.HasValue && budget.Value > 0)
                {
                    budgetPercentage = (totalSpend / budget.Value) * 100;
                    
                    // Check budget thresholds and send alerts
                    await CheckBudgetThresholdsAsync(virtualKeyId, totalSpend, budget.Value, budgetPercentage.Value);
                }

                var notification = new SpendUpdateNotification
                {
                    NewSpend = amount,
                    TotalSpend = totalSpend,
                    Budget = budget,
                    BudgetPercentage = budgetPercentage,
                    Model = model,
                    ProviderType = Enum.TryParse<ProviderType>(provider, true, out var providerType) ? providerType : ProviderType.OpenAI,
                    Metadata = new RequestMetadata
                    {
                        RequestId = Guid.NewGuid().ToString(),
                        Endpoint = "/v1/chat/completions" // Should be passed in
                    }
                };

                // Get hub instance and send notification
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var hub = scope.ServiceProvider.GetService<SpendNotificationHub>();
                    if (hub != null)
                    {
                        await hub.SendSpendUpdate(virtualKeyId, notification);
                    }
                    else
                    {
                        // Fallback to hub context
                        var groupName = $"vkey-{virtualKeyId}";
                        await _hubContext.Clients.Group(groupName).SendAsync("SpendUpdate", notification);
                    }
                }

                // Check for unusual spending
                await CheckUnusualSpendingAsync(virtualKeyId);

                _logger.LogInformation(
                    "Sent spend update for VirtualKey {VirtualKeyId}: ${Amount:F2} (Total: ${TotalSpend:F2})",
                    virtualKeyId,
                    amount,
                    totalSpend);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending spend update notification");
                // Don't throw - notifications should not break the main flow
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility - delegates to NotifySpendUpdateAsync
        /// </summary>
        public async Task NotifySpendUpdatedAsync(int virtualKeyId, decimal spendAmount, string model, string provider)
        {
            // For the legacy method, we don't have totalSpend or budget information
            // So we'll call the new method with just the amount
            await NotifySpendUpdateAsync(virtualKeyId, spendAmount, spendAmount, null, model, provider);
        }

        public async Task SendSpendSummaryAsync(int virtualKeyId, SpendSummaryNotification summary)
        {
            try
            {
                var groupName = $"vkey-{virtualKeyId}";
                await _hubContext.Clients.Group(groupName).SendAsync("SpendSummary", summary);
                
                _logger.LogInformation(
                    "Sent {PeriodType} spend summary for VirtualKey {VirtualKeyId}: ${TotalSpend:F2}",
                    summary.PeriodType,
                    virtualKeyId,
                    summary.TotalSpend);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending spend summary notification");
            }
        }

        public void RecordSpend(int virtualKeyId, decimal amount)
        {
            var pattern = _spendingPatterns.GetOrAdd(virtualKeyId, _ => new SpendingPattern());
            pattern.RecordSpend(amount);
        }

        private async Task CheckBudgetThresholdsAsync(int virtualKeyId, decimal totalSpend, decimal budget, decimal percentageUsed)
        {
            try
            {
                // Get or create the set of sent alerts for this virtual key
                var sentAlerts = _sentBudgetAlerts.GetOrAdd(virtualKeyId, _ => new HashSet<int>());

                // Define budget thresholds
                var thresholds = new[]
                {
                    (threshold: 50, severity: "info", message: "You have used 50% of your budget"),
                    (threshold: 75, severity: "warning", message: "You have used 75% of your budget"),
                    (threshold: 90, severity: "critical", message: "You have used 90% of your budget - approaching limit"),
                    (threshold: 100, severity: "critical", message: "Budget limit reached - further requests may be blocked")
                };

                foreach (var (threshold, severity, message) in thresholds)
                {
                    if (percentageUsed >= threshold && !sentAlerts.Contains(threshold))
                    {
                        // Send budget alert
                        var alertType = threshold switch
                        {
                            50 => "budget_50_percent",
                            75 => "budget_75_percent",
                            90 => "budget_90_percent",
                            100 => "budget_exceeded",
                            _ => "budget_threshold"
                        };

                        var recommendations = threshold switch
                        {
                            50 => new List<string> { "Monitor your usage patterns", "Consider optimizing model selection" },
                            75 => new List<string> { "Review recent API usage", "Consider implementing caching", "Switch to more cost-effective models" },
                            90 => new List<string> { "Urgent: Review and reduce API usage", "Implement rate limiting", "Consider increasing budget if needed" },
                            100 => new List<string> { "API access may be restricted", "Increase budget immediately", "Review and optimize all API calls" },
                            _ => new List<string>()
                        };

                        var notification = new BudgetAlertNotification
                        {
                            AlertType = alertType,
                            Message = message,
                            CurrentSpend = totalSpend,
                            BudgetLimit = budget,
                            PercentageUsed = (double)percentageUsed,
                            Severity = severity,
                            Recommendations = recommendations
                        };

                        var groupName = $"vkey-{virtualKeyId}";
                        await _hubContext.Clients.Group(groupName).SendAsync("BudgetAlert", notification);

                        // Mark this threshold as sent
                        sentAlerts.Add(threshold);

                        _logger.LogWarning(
                            "[SignalR:BudgetAlert] Sent notification - VirtualKey: {VirtualKeyId}, Threshold: {Threshold}%, CurrentSpend: ${CurrentSpend:F2}, Budget: ${Budget:F2}, AlertType: {AlertType}, Severity: {Severity}, Group: {GroupName}",
                            virtualKeyId,
                            threshold,
                            totalSpend,
                            budget,
                            alertType,
                            severity,
                            groupName);
                    }
                }

                // Reset sent alerts if spending goes back down (e.g., new month)
                if (percentageUsed < 50 && sentAlerts.Count > 0)
                {
                    sentAlerts.Clear();
                    _logger.LogInformation("Budget alerts reset for VirtualKey {VirtualKeyId} as usage dropped below 50%", virtualKeyId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking budget thresholds for VirtualKey {VirtualKeyId}", virtualKeyId);
            }
        }

        public async Task CheckUnusualSpendingAsync(int virtualKeyId)
        {
            try
            {
                if (!_spendingPatterns.TryGetValue(virtualKeyId, out var pattern))
                    return;

                var analysis = pattern.AnalyzePattern();
                if (analysis.IsUnusual)
                {
                    var notification = new UnusualSpendingNotification
                    {
                        ActivityType = analysis.PatternType,
                        Description = analysis.Description,
                        CurrentRate = analysis.CurrentRate,
                        NormalRate = analysis.NormalRate,
                        DeviationPercentage = (double)analysis.PercentageIncrease,
                        Recommendations = analysis.RecommendedActions
                    };

                    var groupName = $"vkey-{virtualKeyId}";
                    await _hubContext.Clients.Group(groupName).SendAsync("UnusualSpendingDetected", notification);

                    _logger.LogWarning(
                        "Unusual spending detected for VirtualKey {VirtualKeyId}: {PatternType} - {Description}",
                        virtualKeyId,
                        analysis.PatternType,
                        analysis.Description);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking unusual spending patterns");
            }
        }

        private void AnalyzeSpendingPatterns(object? state)
        {
            // Fire and forget with proper error handling
            _ = AnalyzeSpendingPatternsAsync();
        }

        private async Task AnalyzeSpendingPatternsAsync()
        {
            try
            {
                foreach (var kvp in _spendingPatterns)
                {
                    await CheckUnusualSpendingAsync(kvp.Key);
                }

                // Clean up old patterns (not accessed in 24 hours)
                var cutoff = DateTime.UtcNow.AddHours(-24);
                var keysToRemove = _spendingPatterns
                    .Where(kvp => kvp.Value.LastAccessed < cutoff)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _spendingPatterns.TryRemove(key, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in pattern analysis timer");
            }
        }

        /// <summary>
        /// Tracks spending patterns for a virtual key.
        /// </summary>
        private class SpendingPattern
        {
            private readonly Queue<SpendRecord> _recentSpends = new();
            private readonly object _lock = new();
            
            public DateTime LastAccessed { get; private set; } = DateTime.UtcNow;

            public void RecordSpend(decimal amount)
            {
                lock (_lock)
                {
                    LastAccessed = DateTime.UtcNow;
                    _recentSpends.Enqueue(new SpendRecord { Amount = amount, Timestamp = DateTime.UtcNow });
                    
                    // Keep only last hour of data
                    var cutoff = DateTime.UtcNow.AddHours(-1);
                    while (_recentSpends.Count > 0 && _recentSpends.Peek().Timestamp < cutoff)
                    {
                        _recentSpends.Dequeue();
                    }
                }
            }

            public PatternAnalysis AnalyzePattern()
            {
                lock (_lock)
                {
                    if (_recentSpends.Count < 5) // Need at least 5 records
                    {
                        return new PatternAnalysis { IsUnusual = false };
                    }

                    var now = DateTime.UtcNow;
                    var lastHour = _recentSpends.Where(s => s.Timestamp > now.AddHours(-1)).ToList();
                    var previousHour = _recentSpends.Where(s => s.Timestamp <= now.AddHours(-1) && s.Timestamp > now.AddHours(-2)).ToList();

                    if (lastHour.Count == 0 || previousHour.Count == 0)
                    {
                        return new PatternAnalysis { IsUnusual = false };
                    }

                    var currentRate = lastHour.Sum(s => s.Amount);
                    var normalRate = previousHour.Sum(s => s.Amount);

                    // Check for spike
                    if (normalRate > 0 && currentRate > normalRate * 3)
                    {
                        var percentageIncrease = ((currentRate - normalRate) / normalRate) * 100;
                        return new PatternAnalysis
                        {
                            IsUnusual = true,
                            PatternType = "spend_spike",
                            Description = $"Spending has increased by {percentageIncrease:F0}% in the last hour",
                            Severity = percentageIncrease > 500 ? "critical" : "warning",
                            CurrentRate = currentRate,
                            NormalRate = normalRate,
                            PercentageIncrease = percentageIncrease,
                            RecommendedActions = new List<string>
                            {
                                "Review recent API usage",
                                "Check for runaway processes",
                                "Consider implementing rate limiting"
                            }
                        };
                    }

                    // Check for sustained high spending
                    var avgAmount = lastHour.Average(s => s.Amount);
                    if (avgAmount > 10 && lastHour.Count > 20) // More than 20 requests in an hour with high avg cost
                    {
                        return new PatternAnalysis
                        {
                            IsUnusual = true,
                            PatternType = "sustained_high_spending",
                            Description = "Sustained high API usage detected",
                            Severity = "warning",
                            CurrentRate = currentRate,
                            NormalRate = normalRate,
                            PercentageIncrease = 0,
                            RecommendedActions = new List<string>
                            {
                                "Review API usage patterns",
                                "Consider batch processing",
                                "Optimize model selection"
                            }
                        };
                    }

                    return new PatternAnalysis { IsUnusual = false };
                }
            }

            private class SpendRecord
            {
                public decimal Amount { get; set; }
                public DateTime Timestamp { get; set; }
            }
        }

        /// <summary>
        /// Result of pattern analysis.
        /// </summary>
        private class PatternAnalysis
        {
            public bool IsUnusual { get; set; }
            public string PatternType { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Severity { get; set; } = "info";
            public decimal CurrentRate { get; set; }
            public decimal NormalRate { get; set; }
            public decimal PercentageIncrease { get; set; }
            public List<string> RecommendedActions { get; set; } = new();
        }
    }
}