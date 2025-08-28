using System.Diagnostics;

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
            var cacheKey = $"{CachePrefixSummary}cost:{timeframe}:{startDate?.Ticks}:{endDate?.Ticks}";
            var cacheHit = false;
            
            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                _metrics?.RecordCacheMiss(cacheKey);
                entry.AbsoluteExpirationRelativeToNow = ShortCacheDuration;
                
                _logger.LogInformation("Getting cost summary with timeframe: {Timeframe}", timeframe);

                // Normalize parameters
                timeframe = NormalizeTimeframe(timeframe);
                startDate = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
                endDate = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : DateTime.UtcNow;

                var fetchStopwatch = Stopwatch.StartNew();
                var logs = await _requestLogRepository.GetByDateRangeAsync(startDate.Value, endDate.Value);
                _metrics?.RecordFetchDuration("RequestLogRepository.GetByDateRangeAsync", fetchStopwatch.ElapsedMilliseconds);

                // Calculate aggregations
                var dailyCosts = CalculateDailyCosts(logs);
                var modelBreakdown = CalculateModelBreakdown(logs);
                var providerBreakdown = CalculateProviderBreakdown(logs);
                var virtualKeyBreakdown = CalculateVirtualKeyBreakdown(logs);

                // Aggregate by timeframe
                var aggregatedCosts = AggregateByTimeframe(dailyCosts, timeframe);

                // Convert to DetailedCostDataDto format for compatibility
                var topModelsBySpend = modelBreakdown.Take(10).Select(m => new DetailedCostDataDto
                {
                    Name = m.ModelName,
                    Cost = m.TotalCost,
                    Percentage = logs.Any() ? (m.TotalCost / logs.Sum(l => l.Cost) * 100) : 0,
                    RequestCount = m.RequestCount
                }).ToList();

                var topProvidersBySpend = providerBreakdown.Take(10).Select(p => new DetailedCostDataDto
                {
                    Name = p.ProviderName,
                    Cost = p.TotalCost,
                    Percentage = logs.Any() ? (p.TotalCost / logs.Sum(l => l.Cost) * 100) : 0,
                    RequestCount = p.RequestCount
                }).ToList();

                var topVirtualKeysBySpend = virtualKeyBreakdown.Take(10).Select(v => new DetailedCostDataDto
                {
                    Name = v.KeyName,
                    Cost = v.TotalCost,
                    Percentage = logs.Any() ? (v.TotalCost / logs.Sum(l => l.Cost) * 100) : 0,
                    RequestCount = v.RequestCount
                }).ToList();

                return new CostDashboardDto
                {
                    TimeFrame = timeframe,
                    StartDate = startDate.Value,
                    EndDate = endDate.Value,
                    TotalCost = logs.Sum(l => l.Cost),
                    Last24HoursCost = CalculateLast24HoursCost(logs),
                    Last7DaysCost = CalculateLast7DaysCost(logs),
                    Last30DaysCost = CalculateLast30DaysCost(logs),
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
            var cacheKey = $"{CachePrefixCostTrend}{period}:{startDate?.Ticks}:{endDate?.Ticks}";
            var cacheHit = false;
            
            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                _metrics?.RecordCacheMiss(cacheKey);
                entry.AbsoluteExpirationRelativeToNow = MediumCacheDuration;
                
                _logger.LogInformation("Getting cost trends with period: {Period}", period);

                period = NormalizeTimeframe(period);
                startDate = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
                endDate = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : DateTime.UtcNow;

                var fetchStopwatch = Stopwatch.StartNew();
                var logs = await _requestLogRepository.GetByDateRangeAsync(startDate.Value, endDate.Value);
                _metrics?.RecordFetchDuration("RequestLogRepository.GetByDateRangeAsync", fetchStopwatch.ElapsedMilliseconds);

                // Calculate trends
                var trendData = CalculateCostTrends(logs, period);
                var previousPeriodComparison = await CalculatePreviousPeriodComparison(startDate.Value, endDate.Value);

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
            _logger.LogInformation("Getting model costs breakdown");

            startDate = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
            endDate = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : DateTime.UtcNow;

            var logs = await _requestLogRepository.GetByDateRangeAsync(startDate.Value, endDate.Value);
            var modelBreakdown = CalculateModelBreakdown(logs);

            return new ModelCostBreakdownDto
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                Models = modelBreakdown.Take(topN).ToList(),
                TotalCost = logs.Sum(l => l.Cost),
                TotalRequests = logs.Count
            };
        }

        /// <inheritdoc/>
        public async Task<VirtualKeyCostBreakdownDto> GetVirtualKeyCostsAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            int topN = 10)
        {
            _logger.LogInformation("Getting virtual key costs breakdown");

            startDate = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
            endDate = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : DateTime.UtcNow;

            var logs = await _requestLogRepository.GetByDateRangeAsync(startDate.Value, endDate.Value);
            var virtualKeys = await _virtualKeyRepository.GetAllAsync();
            var keyMap = virtualKeys.ToDictionary(k => k.Id, k => k.KeyName);

            var breakdown = logs
                .GroupBy(l => l.VirtualKeyId)
                .Select(g => new VirtualKeyCostDetail
                {
                    VirtualKeyId = g.Key,
                    KeyName = keyMap.GetValueOrDefault(g.Key, $"Key #{g.Key}"),
                    TotalCost = g.Sum(l => l.Cost),
                    RequestCount = g.Count(),
                    AverageCostPerRequest = g.Average(l => l.Cost),
                    LastUsed = g.Max(l => l.Timestamp),
                    UniqueModels = g.Select(l => l.ModelName).Distinct().Count()
                })
                .OrderByDescending(v => v.TotalCost)
                .Take(topN)
                .ToList();

            return new VirtualKeyCostBreakdownDto
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                VirtualKeys = breakdown,
                TotalCost = logs.Sum(l => l.Cost),
                TotalRequests = logs.Count
            };
        }

        #endregion
    }
}