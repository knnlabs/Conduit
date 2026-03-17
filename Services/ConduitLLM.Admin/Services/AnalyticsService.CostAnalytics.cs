using System.Diagnostics;

using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Costs;

using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Unified analytics service - Cost Analytics functionality
    /// </summary>
    public partial class AnalyticsService
    {
        #region Cost Analytics

        /// <inheritdoc/>
        public async Task<CostDashboardDto> GetCostSummaryAsync(
            string timeframe = "daily",
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var cacheKey = $"{CacheKeys.Analytics.SummaryPrefix}cost:{timeframe}:{startDate?.Ticks}:{endDate?.Ticks}";
            var cacheHit = false;

            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                _metrics?.RecordCacheMiss(cacheKey);
                entry.AbsoluteExpirationRelativeToNow = ShortCacheDuration;

                _logger.LogDebug("Getting cost summary with timeframe: {Timeframe}", timeframe);

                // Normalize parameters
                timeframe = NormalizeTimeframe(timeframe);
                startDate = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
                endDate = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : DateTime.UtcNow;

                // Fetch aggregations from database in parallel — no full log loading
                var fetchStopwatch = Stopwatch.StartNew();
                var modelTask = _requestLogRepository.GetAggregatedByModelAsync(startDate.Value, endDate.Value);
                var virtualKeyTask = _requestLogRepository.GetAggregatedByVirtualKeyAsync(startDate.Value, endDate.Value);
                var dailyCostsTask = _requestLogRepository.GetCostsByDateAsync(startDate.Value, endDate.Value);
                var last24hTask = _requestLogRepository.GetSummaryAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
                var last7dTask = _requestLogRepository.GetSummaryAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

                await Task.WhenAll(modelTask, virtualKeyTask, dailyCostsTask, last24hTask, last7dTask);
                _metrics?.RecordFetchDuration("RequestLogRepository.AggregateQueries", fetchStopwatch.ElapsedMilliseconds);

                var modelBreakdown = await modelTask;
                var virtualKeyBreakdown = await virtualKeyTask;
                var dailyCosts = await dailyCostsTask;
                var providerBreakdown = CalculateProviderBreakdownFromModels(modelBreakdown);

                var totalCost = dailyCosts.Sum(d => d.TotalCost);

                // Aggregate by timeframe
                var aggregatedCosts = AggregateByTimeframe(dailyCosts, timeframe);

                // Convert to DetailedCostDataDto format for compatibility
                List<DetailedCostDataDto> topModelsBySpend = [
                    ..modelBreakdown.Take(10).Select(m => new DetailedCostDataDto
                    {
                        Name = m.ModelName,
                        Cost = m.TotalCost,
                        Percentage = totalCost > 0 ? (m.TotalCost / totalCost * 100) : 0,
                        RequestCount = m.RequestCount
                    })
                ];

                List<DetailedCostDataDto> topProvidersBySpend = [
                    ..providerBreakdown.Take(10).Select(p => new DetailedCostDataDto
                    {
                        Name = p.ProviderName,
                        Cost = p.TotalCost,
                        Percentage = totalCost > 0 ? (p.TotalCost / totalCost * 100) : 0,
                        RequestCount = p.RequestCount
                    })
                ];

                List<DetailedCostDataDto> topVirtualKeysBySpend = [
                    ..ToVirtualKeyCostDetails(virtualKeyBreakdown).Take(10).Select(v => new DetailedCostDataDto
                    {
                        Name = v.KeyName,
                        Cost = v.TotalCost,
                        Percentage = totalCost > 0 ? (v.TotalCost / totalCost * 100) : 0,
                        RequestCount = v.RequestCount
                    })
                ];

                return new CostDashboardDto
                {
                    TimeFrame = timeframe,
                    StartDate = startDate.Value,
                    EndDate = endDate.Value,
                    TotalCost = totalCost,
                    Last24HoursCost = (await last24hTask).TotalCost,
                    Last7DaysCost = (await last7dTask).TotalCost,
                    Last30DaysCost = totalCost, // Date range already defaults to 30 days
                    TopModelsBySpend = topModelsBySpend,
                    TopProvidersBySpend = topProvidersBySpend,
                    TopVirtualKeysBySpend = topVirtualKeysBySpend
                };
            });

            if (!cacheHit && result != null)
            {
                cacheHit = true;
                _metrics?.RecordCacheHit(cacheKey);
            }

            _metrics?.RecordOperationDuration("GetCostSummaryAsync", stopwatch.ElapsedMilliseconds);

            return result ?? new CostDashboardDto
            {
                TimeFrame = timeframe,
                StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
                EndDate = endDate ?? DateTime.UtcNow,
                TotalCost = 0,
                Last24HoursCost = 0,
                Last7DaysCost = 0,
                Last30DaysCost = 0,
                TopModelsBySpend = new List<DetailedCostDataDto>(),
                TopProvidersBySpend = new List<DetailedCostDataDto>(),
                TopVirtualKeysBySpend = new List<DetailedCostDataDto>()
            };
        }

        /// <inheritdoc/>
        public async Task<CostTrendDto> GetCostTrendsAsync(
            string period = "daily",
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var cacheKey = $"{CacheKeys.Analytics.CostTrendPrefix}{period}:{startDate?.Ticks}:{endDate?.Ticks}";
            var cacheHit = false;

            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                _metrics?.RecordCacheMiss(cacheKey);
                entry.AbsoluteExpirationRelativeToNow = MediumCacheDuration;

                _logger.LogDebug("Getting cost trends with period: {Period}", period);

                period = NormalizeTimeframe(period);
                startDate = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
                endDate = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : DateTime.UtcNow;

                // Fetch daily cost aggregations from database and comparison in parallel
                var fetchStopwatch = Stopwatch.StartNew();
                var dailyCostsTask = _requestLogRepository.GetCostsByDateAsync(startDate.Value, endDate.Value);
                var comparisonTask = CalculatePreviousPeriodComparison(startDate.Value, endDate.Value);
                await Task.WhenAll(dailyCostsTask, comparisonTask);
                _metrics?.RecordFetchDuration("RequestLogRepository.GetCostsByDateAsync", fetchStopwatch.ElapsedMilliseconds);

                // Calculate trends from daily aggregations (~365 rows max)
                var trendData = CalculateCostTrendsFromDaily(await dailyCostsTask, period);

                // Convert to CostTrendDataDto format
                var trendDataDto = trendData.Select(t => new CostTrendDataDto
                {
                    Date = t.Date,
                    Cost = t.Cost,
                    RequestCount = t.RequestCount
                }).ToList();

                return new CostTrendDto
                {
                    Period = period,
                    StartDate = startDate.Value,
                    EndDate = endDate.Value,
                    Data = trendDataDto
                };
            });

            if (!cacheHit && result != null)
            {
                cacheHit = true;
                _metrics?.RecordCacheHit(cacheKey);
            }

            _metrics?.RecordOperationDuration("GetCostTrendsAsync", stopwatch.ElapsedMilliseconds);

            return result ?? new CostTrendDto
            {
                Period = period,
                StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
                EndDate = endDate ?? DateTime.UtcNow,
                Data = new List<CostTrendDataDto>()
            };
        }

        /// <inheritdoc/>
        public async Task<ModelCostBreakdownDto> GetModelCostsAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            int topN = 10)
        {
            _logger.LogDebug("Getting model costs breakdown");

            startDate = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
            endDate = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : DateTime.UtcNow;

            var modelAggregations = await _requestLogRepository.GetAggregatedByModelAsync(startDate.Value, endDate.Value);
            var modelBreakdown = ToModelCostDetails(modelAggregations);
            var totalCost = modelAggregations.Sum(m => m.TotalCost);
            var totalRequests = modelAggregations.Sum(m => m.RequestCount);

            return new ModelCostBreakdownDto
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                Models = modelBreakdown.Take(topN).ToList(),
                TotalCost = totalCost,
                TotalRequests = totalRequests
            };
        }

        /// <inheritdoc/>
        public async Task<VirtualKeyCostBreakdownDto> GetVirtualKeyCostsAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            int topN = 10)
        {
            _logger.LogDebug("Getting virtual key costs breakdown");

            startDate = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
            endDate = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : DateTime.UtcNow;

            var keyAggregations = await _requestLogRepository.GetAggregatedByVirtualKeyAsync(startDate.Value, endDate.Value);

            // Get only the virtual key names we need using efficient lookup
            var virtualKeyIds = keyAggregations.Select(k => k.VirtualKeyId).ToList();
            var keyMap = virtualKeyIds.Count != 0
                ? await _virtualKeyRepository.GetKeyNamesByIdsAsync(virtualKeyIds)
                : new Dictionary<int, string>();

            var breakdown = keyAggregations
                .Select(v => new VirtualKeyCostDetail
                {
                    VirtualKeyId = v.VirtualKeyId,
                    KeyName = keyMap.GetValueOrDefault(v.VirtualKeyId, $"Key #{v.VirtualKeyId}"),
                    TotalCost = v.TotalCost,
                    RequestCount = v.RequestCount,
                    AverageCostPerRequest = v.RequestCount > 0 ? v.TotalCost / v.RequestCount : 0,
                    LastUsed = v.LastUsed,
                    UniqueModels = v.UniqueModels
                })
                .Take(topN)
                .ToList();

            var totalCost = keyAggregations.Sum(k => k.TotalCost);
            var totalRequests = keyAggregations.Sum(k => k.RequestCount);

            return new VirtualKeyCostBreakdownDto
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                VirtualKeys = breakdown,
                TotalCost = totalCost,
                TotalRequests = totalRequests
            };
        }

        #endregion
    }
}
