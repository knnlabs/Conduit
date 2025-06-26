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
using ConduitLLM.Http.Hubs;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for managing spend notifications and detecting unusual spending patterns.
    /// </summary>
    public interface ISpendNotificationService
    {
        /// <summary>
        /// Notifies about a spend update.
        /// </summary>
        Task NotifySpendUpdateAsync(int virtualKeyId, decimal amount, decimal totalSpend, decimal? budget, string model, string provider);

        /// <summary>
        /// Sends a spend summary for a period.
        /// </summary>
        Task SendSpendSummaryAsync(int virtualKeyId, SpendSummaryNotification summary);

        /// <summary>
        /// Records spend data for pattern analysis.
        /// </summary>
        void RecordSpend(int virtualKeyId, decimal amount);

        /// <summary>
        /// Checks for unusual spending patterns.
        /// </summary>
        Task CheckUnusualSpendingAsync(int virtualKeyId);
    }

    /// <summary>
    /// Implementation of spend notification service.
    /// </summary>
    public class SpendNotificationService : ISpendNotificationService, IHostedService
    {
        private readonly IHubContext<SpendNotificationHub> _hubContext;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SpendNotificationService> _logger;
        
        // Track spending patterns per virtual key
        private readonly ConcurrentDictionary<int, SpendingPattern> _spendingPatterns = new();
        
        // Timer for periodic pattern analysis
        private Timer? _patternAnalysisTimer;
        private readonly TimeSpan _analysisInterval = TimeSpan.FromMinutes(5);

        public SpendNotificationService(
            IHubContext<SpendNotificationHub> hubContext,
            IServiceProvider serviceProvider,
            ILogger<SpendNotificationService> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _patternAnalysisTimer = new Timer(
                AnalyzeSpendingPatterns,
                null,
                _analysisInterval,
                _analysisInterval);
            
            _logger.LogInformation("SpendNotificationService started");
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
                }

                var notification = new SpendUpdateNotification
                {
                    NewSpend = amount,
                    TotalSpend = totalSpend,
                    Budget = budget,
                    BudgetPercentage = budgetPercentage,
                    Model = model,
                    Provider = provider,
                    Metadata = new RequestMetadata
                    {
                        RequestId = Guid.NewGuid().ToString(),
                        Endpoint = "/v1/chat/completions" // Should be passed in
                    }
                };

                // Get hub instance and send notification
                var hub = _serviceProvider.GetService<SpendNotificationHub>();
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
                        PatternType = analysis.PatternType,
                        Description = analysis.Description,
                        Severity = analysis.Severity,
                        CurrentSpendRate = analysis.CurrentRate,
                        NormalSpendRate = analysis.NormalRate,
                        PercentageIncrease = analysis.PercentageIncrease,
                        RecommendedActions = analysis.RecommendedActions
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

        private async void AnalyzeSpendingPatterns(object? state)
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