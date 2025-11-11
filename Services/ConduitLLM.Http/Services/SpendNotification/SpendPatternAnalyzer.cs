using Microsoft.AspNetCore.SignalR;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Http.Hubs;

namespace ConduitLLM.Http.Services.SpendNotification
{
    /// <summary>
    /// Analyzes spending patterns for unusual activity
    /// </summary>
    public interface ISpendPatternAnalyzer
    {
        /// <summary>
        /// Checks for unusual spending patterns for a virtual key
        /// </summary>
        Task CheckUnusualSpendingAsync(int virtualKeyId);
        
        /// <summary>
        /// Analyzes all spending patterns periodically
        /// </summary>
        Task AnalyzeAllPatternsAsync();
    }

    /// <summary>
    /// Implementation of spending pattern analyzer
    /// </summary>
    public class SpendPatternAnalyzer : ISpendPatternAnalyzer
    {
        private readonly IHubContext<SpendNotificationHub> _hubContext;
        private readonly ISpendDataRepository _repository;
        private readonly ILogger<SpendPatternAnalyzer> _logger;
        
        private readonly TimeSpan _unusualAlertCooldown = TimeSpan.FromHours(1);
        
        // Thresholds for unusual patterns
        private const int HighFrequencyThreshold = 60; // requests per hour
        private const decimal SpendingSpikeThreshold = 0.1m; // 10% of daily average
        
        public SpendPatternAnalyzer(
            IHubContext<SpendNotificationHub> hubContext,
            ISpendDataRepository repository,
            ILogger<SpendPatternAnalyzer> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task CheckUnusualSpendingAsync(int virtualKeyId)
        {
            try
            {
                var pattern = await _repository.GetSpendingPatternAsync(virtualKeyId);
                if (pattern == null) return;
                
                var analysis = AnalyzePattern(pattern);
                if (!analysis.IsUnusual) return;
                
                // Check cooldown for unusual spending alerts
                var cooldownKey = $"unusual:{analysis.PatternType}";
                if (await _repository.IsAlertInCooldownAsync(virtualKeyId, cooldownKey))
                {
                    return;
                }
                
                // Set cooldown
                await _repository.SetAlertCooldownAsync(virtualKeyId, cooldownKey, _unusualAlertCooldown);
                
                // Send unusual spending notification
                await SendUnusualSpendingNotificationAsync(virtualKeyId, analysis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking unusual spending patterns for VirtualKey {VirtualKeyId}", virtualKeyId);
            }
        }

        public async Task AnalyzeAllPatternsAsync()
        {
            try
            {
                var virtualKeyIds = await _repository.GetAllPatternKeysAsync();
                
                foreach (var virtualKeyId in virtualKeyIds)
                {
                    await CheckUnusualSpendingAsync(virtualKeyId);
                }
                
                _logger.LogDebug("Analyzed {Count} spending patterns", virtualKeyIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing all spending patterns");
            }
        }

        private PatternAnalysis AnalyzePattern(SpendingPatternData pattern)
        {
            var analysis = new PatternAnalysis { IsUnusual = false };
            
            // High frequency pattern detection
            if (pattern.HourlyCount > HighFrequencyThreshold)
            {
                analysis.IsUnusual = true;
                analysis.PatternType = "High Frequency";
                analysis.Description = $"Unusually high request rate: {pattern.HourlyCount} requests in the last hour";
                analysis.CurrentRate = pattern.HourlyCount;
                analysis.NormalRate = 30; // Assumed normal rate
            }
            // Spike detection (hourly spend > 10% of daily average)
            else if (pattern.DailyCount > 0 && pattern.HourlyTotal > (pattern.DailyTotal * SpendingSpikeThreshold))
            {
                analysis.IsUnusual = true;
                analysis.PatternType = "Spending Spike";
                analysis.Description = $"Sudden increase in spending: ${pattern.HourlyTotal:F2} in the last hour";
                analysis.CurrentRate = pattern.HourlyTotal;
                analysis.NormalRate = pattern.DailyTotal / 24m; // Daily average per hour
            }
            
            if (analysis.IsUnusual)
            {
                analysis.DeviationPercentage = analysis.NormalRate > 0 
                    ? ((analysis.CurrentRate - analysis.NormalRate) / analysis.NormalRate * 100) 
                    : 100;
            }
            
            return analysis;
        }

        private async Task SendUnusualSpendingNotificationAsync(int virtualKeyId, PatternAnalysis analysis)
        {
            try
            {
                var notification = new UnusualSpendingNotification
                {
                    ActivityType = analysis.PatternType,
                    Description = analysis.Description,
                    CurrentRate = analysis.CurrentRate,
                    NormalRate = analysis.NormalRate,
                    DeviationPercentage = (double)analysis.DeviationPercentage,
                    Recommendations = GetUnusualSpendingRecommendations()
                };
                
                var groupName = $"vkey-{virtualKeyId}";
                await _hubContext.Clients.Group(groupName).SendAsync("UnusualSpendingDetected", notification);
                
                _logger.LogWarning(
                    "[UnusualSpending] Detected - VirtualKey: {VirtualKeyId}, Pattern: {PatternType}, " +
                    "Description: {Description}",
                    virtualKeyId, analysis.PatternType, analysis.Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending unusual spending notification for VirtualKey {VirtualKeyId}", 
                    virtualKeyId);
            }
        }

        private static List<string> GetUnusualSpendingRecommendations()
        {
            return new List<string>
            {
                "Review recent API usage for anomalies",
                "Check for potential runaway processes",
                "Consider implementing rate limiting",
                "Verify API keys haven't been compromised"
            };
        }

        private class PatternAnalysis
        {
            public bool IsUnusual { get; set; }
            public string PatternType { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public decimal CurrentRate { get; set; }
            public decimal NormalRate { get; set; }
            public decimal DeviationPercentage { get; set; }
        }
    }
}