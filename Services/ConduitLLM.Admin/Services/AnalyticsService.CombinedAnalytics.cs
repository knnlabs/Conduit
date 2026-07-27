using System.Diagnostics;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Extensions;

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
            var cacheKey = $"{CacheKeys.Analytics.SummaryPrefix}full:{timeframe}:{startDate?.Ticks}:{endDate?.Ticks}";
            var cacheHit = false;

            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                _cacheInvalidator.TrackEntry(entry, cacheKey);
                _metrics?.RecordCacheMiss(cacheKey);
                entry.AbsoluteExpirationRelativeToNow = MediumCacheDuration;

                _logger.LogDebug("Getting comprehensive analytics summary");

                timeframe = NormalizeTimeframe(timeframe);
                startDate ??= DateTime.UtcNow.AddDays(-30);
                endDate ??= DateTime.UtcNow;

                // Fetch all aggregations from database in parallel — no full log loading
                var fetchStopwatch = Stopwatch.StartNew();
                var summaryTask = _requestLogRepository.GetSummaryAsync(startDate.Value, endDate.Value);
                var modelTask = _requestLogRepository.GetAggregatedByModelAsync(startDate.Value, endDate.Value);
                var virtualKeyTask = _requestLogRepository.GetAggregatedByVirtualKeyAsync(startDate.Value, endDate.Value);
                var dailyStatsTask = _requestLogRepository.GetDailyStatisticsAsync(startDate.Value, endDate.Value);
                var comparisonTask = CalculatePreviousPeriodComparison(startDate.Value, endDate.Value);

                await Task.WhenAll(summaryTask, modelTask, virtualKeyTask, dailyStatsTask, comparisonTask);
                _metrics?.RecordFetchDuration("RequestLogRepository.AggregateQueries", fetchStopwatch.ElapsedMilliseconds);

                var summary = await summaryTask;
                var modelAggregations = await modelTask;
                var virtualKeyAggregations = await virtualKeyTask;

                // Get virtual key names for the top keys
                fetchStopwatch.Restart();
                var virtualKeyIds = virtualKeyAggregations.Take(10).Select(v => v.VirtualKeyId).ToList();
                var keyMap = virtualKeyIds.Count != 0
                    ? await _virtualKeyRepository.GetKeyNamesByIdsAsync(virtualKeyIds)
                    : new Dictionary<int, string>();
                _metrics?.RecordFetchDuration("VirtualKeyRepository.GetKeyNamesByIdsAsync", fetchStopwatch.ElapsedMilliseconds);

                var successRate = summary.TotalRequests > 0
                    ? (summary.SuccessCount * 100.0 / summary.TotalRequests)
                    : 0;

                // Convert model aggregations to top models summary
                var topModels = modelAggregations.Take(10).Select(m => new ModelUsageSummary
                {
                    ModelName = m.ModelName,
                    RequestCount = m.RequestCount,
                    TotalCost = m.TotalCost,
                    InputTokens = m.InputTokens,
                    OutputTokens = m.OutputTokens,
                    // Not available from the model aggregation (would need an additional
                    // query) — left null so consumers can render "unknown" instead of a
                    // fabricated 0ms / 0% reading
                    AverageResponseTime = null,
                    ErrorRate = null
                }).ToList();

                // Convert virtual key aggregations to top keys summary
                var topVirtualKeys = virtualKeyAggregations.Take(10).Select(v => new VirtualKeyUsageSummary
                {
                    VirtualKeyId = v.VirtualKeyId,
                    KeyName = keyMap.GetValueOrDefault(v.VirtualKeyId, $"Key #{v.VirtualKeyId}"),
                    RequestCount = v.RequestCount,
                    TotalCost = v.TotalCost,
                    LastUsed = v.LastUsed,
                    ModelsUsed = null // Not available from aggregation; null, not an empty list
                }).ToList();

                // Aggregate daily stats to requested timeframe
                var dailyStats = AggregateStatisticsByTimeframe(await dailyStatsTask, timeframe);

                return new AnalyticsSummaryDto
                {
                    TotalRequests = summary.TotalRequests,
                    TotalCost = summary.TotalCost,
                    TotalInputTokens = summary.TotalInputTokens,
                    TotalOutputTokens = summary.TotalOutputTokens,
                    AverageResponseTime = summary.AverageResponseTimeMs,
                    SuccessRate = successRate,
                    UniqueVirtualKeys = virtualKeyAggregations.Count,
                    UniqueModels = modelAggregations.Count,
                    TopModels = topModels,
                    TopVirtualKeys = topVirtualKeys,
                    DailyStats = dailyStats,
                    Comparison = await comparisonTask
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
                Comparison = new AnalyticsPeriodComparison()
            };
        }

        /// <inheritdoc/>
        public async Task<UsageStatisticsDto> GetVirtualKeyUsageAsync(
            int virtualKeyId,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            _logger.LogDebug("Getting usage statistics for virtual key {VirtualKeyId}", virtualKeyId);

            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

            // Fetch summary and model breakdown for this specific key via database-level aggregation
            var summaryTask = _requestLogRepository.GetSummaryForVirtualKeyAsync(virtualKeyId, startDate.Value, endDate.Value);
            var modelTask = _requestLogRepository.GetAggregatedByModelForVirtualKeyAsync(virtualKeyId, startDate.Value, endDate.Value);
            await Task.WhenAll(summaryTask, modelTask);

            var summary = await summaryTask;
            var modelAggregations = await modelTask;

            var result = new UsageStatisticsDto
            {
                TotalRequests = summary.TotalRequests,
                TotalCost = summary.TotalCost,
                TotalInputTokens = (int)Math.Min(summary.TotalInputTokens, int.MaxValue),
                TotalOutputTokens = (int)Math.Min(summary.TotalOutputTokens, int.MaxValue),
                AverageResponseTimeMs = summary.AverageResponseTimeMs,
                ModelUsage = new Dictionary<string, ModelUsage>()
            };

            foreach (var model in modelAggregations)
            {
                result.ModelUsage[model.ModelName] = new ModelUsage
                {
                    RequestCount = model.RequestCount,
                    Cost = model.TotalCost,
                    InputTokens = (int)Math.Min(model.InputTokens, int.MaxValue),
                    OutputTokens = (int)Math.Min(model.OutputTokens, int.MaxValue)
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
            _logger.LogInformation("Exporting analytics in {Format} format, date range {StartDate} to {EndDate}",
                format, startDate?.ToString("yyyy-MM-dd") ?? "default", endDate?.ToString("yyyy-MM-dd") ?? "default");

            try
            {
                startDate ??= DateTime.UtcNow.AddDays(-30);
                endDate ??= DateTime.UtcNow;

                // Export requires full entity data, but the repository pushes the model and
                // virtual-key filters into SQL so we don't materialize rows we'd just discard.
                var logs = await _requestLogRepository.GetByDateRangeFilteredAsync(
                    startDate.Value, endDate.Value, model, virtualKeyId);

                _logger.LogInformation("Analytics export completed: {RecordCount} records in {Format} format",
                    logs.Count, format);

                return format.ToLower() switch
                {
                    "csv" => ExportToCsv(logs),
                    "json" => ExportToJson(logs),
                    _ => throw new ArgumentException($"Unsupported export format: {format}")
                };
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting analytics in {Format} format", format);
                throw;
            }
        }

        #endregion
    }
}
