using System.Diagnostics;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;

using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Unified analytics service - Combined Analytics functionality
    /// </summary>
    public partial class AnalyticsService
    {
        #region Combined Analytics

        /// <inheritdoc/>
        public async Task<AnalyticsSummaryDto> GetAnalyticsSummaryAsync(
            string timeframe = "daily",
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var cacheKey = $"{CachePrefixSummary}full:{timeframe}:{startDate?.Ticks}:{endDate?.Ticks}";
            var cacheHit = false;
            
            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                _metrics?.RecordCacheMiss(cacheKey);
                entry.AbsoluteExpirationRelativeToNow = MediumCacheDuration;
                
                _logger.LogInformation("Getting comprehensive analytics summary");

                timeframe = NormalizeTimeframe(timeframe);
                startDate ??= DateTime.UtcNow.AddDays(-30);
                endDate ??= DateTime.UtcNow;

                var fetchStopwatch = Stopwatch.StartNew();
                var logs = await _requestLogRepository.GetByDateRangeAsync(startDate.Value, endDate.Value);
                _metrics?.RecordFetchDuration("RequestLogRepository.GetByDateRangeAsync", fetchStopwatch.ElapsedMilliseconds);
                
                fetchStopwatch.Restart();
                var virtualKeys = await _virtualKeyRepository.GetAllAsync();
                _metrics?.RecordFetchDuration("VirtualKeyRepository.GetAllAsync", fetchStopwatch.ElapsedMilliseconds);
                var keyMap = virtualKeys.ToDictionary(k => k.Id, k => k.KeyName);

                // Calculate metrics
                var successfulRequests = logs.Count(l => l.StatusCode >= 200 && l.StatusCode < 300);
                var totalRequests = logs.Count;
                var successRate = totalRequests > 0 ? (successfulRequests * 100.0 / totalRequests) : 0;

                // Get top models
                var topModels = logs
                    .GroupBy(l => l.ModelName)
                    .Select(g => new ModelUsageSummary
                    {
                        ModelName = g.Key,
                        RequestCount = g.Count(),
                        TotalCost = g.Sum(l => l.Cost),
                        InputTokens = g.Sum(l => (long)l.InputTokens),
                        OutputTokens = g.Sum(l => (long)l.OutputTokens),
                        AverageResponseTime = g.Average(l => l.ResponseTimeMs),
                        ErrorRate = g.Count(l => l.StatusCode >= 400) * 100.0 / g.Count()
                    })
                    .OrderByDescending(m => m.TotalCost)
                    .Take(10)
                    .ToList();

                // Get top virtual keys
                var topVirtualKeys = logs
                    .GroupBy(l => l.VirtualKeyId)
                    .Select(g => new VirtualKeyUsageSummary
                    {
                        VirtualKeyId = g.Key,
                        KeyName = keyMap.GetValueOrDefault(g.Key, $"Key #{g.Key}"),
                        RequestCount = g.Count(),
                        TotalCost = g.Sum(l => l.Cost),
                        LastUsed = g.Max(l => l.Timestamp),
                        ModelsUsed = g.Select(l => l.ModelName).Distinct().ToList()
                    })
                    .OrderByDescending(v => v.TotalCost)
                    .Take(10)
                    .ToList();

                // Calculate daily statistics
                var dailyStats = CalculateDailyStatistics(logs, timeframe);

                // Get comparison with previous period
                var comparison = await CalculatePreviousPeriodComparison(startDate.Value, endDate.Value);

                return new AnalyticsSummaryDto
                {
                    TotalRequests = totalRequests,
                    TotalCost = logs.Sum(l => l.Cost),
                    TotalInputTokens = logs.Sum(l => (long)l.InputTokens),
                    TotalOutputTokens = logs.Sum(l => (long)l.OutputTokens),
                    AverageResponseTime = logs.Any() ? logs.Average(l => l.ResponseTimeMs) : 0,
                    SuccessRate = successRate,
                    UniqueVirtualKeys = logs.Select(l => l.VirtualKeyId).Distinct().Count(),
                    UniqueModels = logs.Select(l => l.ModelName).Distinct().Count(),
                    TopModels = topModels,
                    TopVirtualKeys = topVirtualKeys,
                    DailyStats = dailyStats,
                    Comparison = comparison
                };
            });
            
            if (!cacheHit && result != null)
            {
                cacheHit = true;
                _metrics?.RecordCacheHit(cacheKey);
            }
            
            _metrics?.RecordOperationDuration("GetAnalyticsSummaryAsync", stopwatch.ElapsedMilliseconds);
            
            return result ?? new AnalyticsSummaryDto
            {
                TotalRequests = 0,
                TotalCost = 0,
                TotalInputTokens = 0,
                TotalOutputTokens = 0,
                UniqueVirtualKeys = 0,
                UniqueModels = 0,
                SuccessRate = 0,
                AverageResponseTime = 0,
                DailyStats = new List<DailyStatistics>(),
                TopModels = new List<ModelUsageSummary>(),
                TopVirtualKeys = new List<VirtualKeyUsageSummary>(),
                Comparison = new PeriodComparison()
            };
        }

        /// <inheritdoc/>
        public async Task<UsageStatisticsDto> GetVirtualKeyUsageAsync(
            int virtualKeyId,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            _logger.LogInformation("Getting usage statistics for virtual key {VirtualKeyId}", virtualKeyId);

            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

            // Get all logs and filter by virtual key
            var allLogs = await _requestLogRepository.GetByDateRangeAsync(startDate.Value, endDate.Value);
            var logs = allLogs.Where(l => l.VirtualKeyId == virtualKeyId).ToList();

            var result = new UsageStatisticsDto
            {
                TotalRequests = logs.Count(),
                TotalCost = logs.Sum(l => l.Cost),
                TotalInputTokens = logs.Sum(l => l.InputTokens),
                TotalOutputTokens = logs.Sum(l => l.OutputTokens),
                AverageResponseTimeMs = logs.Any() ? logs.Average(l => l.ResponseTimeMs) : 0,
                ModelUsage = new Dictionary<string, ModelUsage>()
            };

            // Group by model
            var modelGroups = logs.GroupBy(l => l.ModelName);
            foreach (var group in modelGroups)
            {
                result.ModelUsage[group.Key] = new ModelUsage
                {
                    RequestCount = group.Count(),
                    Cost = group.Sum(l => l.Cost),
                    InputTokens = group.Sum(l => l.InputTokens),
                    OutputTokens = group.Sum(l => l.OutputTokens)
                };
            }

            return result;
        }

        /// <inheritdoc/>
        public async Task<byte[]> ExportAnalyticsAsync(
            string format = "csv",
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? model = null,
            int? virtualKeyId = null)
        {
            _logger.LogInformation("Exporting analytics in {Format} format", format);

            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

            var logs = await _requestLogRepository.GetByDateRangeAsync(startDate.Value, endDate.Value);

            // Apply filters
            if (!string.IsNullOrEmpty(model))
                logs = logs.Where(l => l.ModelName.Contains(model, StringComparison.OrdinalIgnoreCase)).ToList();
            
            if (virtualKeyId.HasValue)
                logs = logs.Where(l => l.VirtualKeyId == virtualKeyId.Value).ToList();

            return format.ToLower() switch
            {
                "csv" => ExportToCsv(logs),
                "json" => ExportToJson(logs),
                _ => throw new ArgumentException($"Unsupported export format: {format}")
            };
        }

        #endregion
    }
}