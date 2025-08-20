using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Costs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Extensions;
using static ConduitLLM.Core.Extensions.LoggingSanitizer;

namespace ConduitLLM.Admin.Services;

/// <summary>
/// Unified analytics service combining logs, costs, and usage metrics
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly IRequestLogRepository _requestLogRepository;
    private readonly IVirtualKeyRepository _virtualKeyRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AnalyticsService> _logger;
    private readonly IAnalyticsMetrics? _metrics;

    // Cache keys
    private const string CachePrefixSummary = "analytics:summary:";
    private const string CachePrefixModels = "analytics:models";
    private const string CachePrefixCostTrend = "analytics:cost:trend:";
    
    // Cache durations
    private static readonly TimeSpan ShortCacheDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MediumCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LongCacheDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Initializes a new instance of the AnalyticsService
    /// </summary>
    /// <param name="requestLogRepository">Repository for request logs</param>
    /// <param name="virtualKeyRepository">Repository for virtual keys</param>
    /// <param name="cache">Memory cache for performance optimization</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="metrics">Optional metrics collection service for monitoring cache performance</param>
    public AnalyticsService(
        IRequestLogRepository requestLogRepository,
        IVirtualKeyRepository virtualKeyRepository,
        IMemoryCache cache,
        ILogger<AnalyticsService> logger,
        IAnalyticsMetrics? metrics = null)
    {
        _requestLogRepository = requestLogRepository ?? throw new ArgumentNullException(nameof(requestLogRepository));
        _virtualKeyRepository = virtualKeyRepository ?? throw new ArgumentNullException(nameof(virtualKeyRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics;
    }

    #region Request Logs

    /// <inheritdoc/>
    public async Task<PagedResult<LogRequestDto>> GetLogsAsync(
        int page = 1,
        int pageSize = 50,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? model = null,
        int? virtualKeyId = null,
        int? status = null)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation(
                "Getting logs - Page: {Page}, PageSize: {PageSize}, Filters: Model={Model}, VirtualKeyId={VirtualKeyId}, Status={Status}",
                page, pageSize, model ?? "all", virtualKeyId?.ToString() ?? "all", status?.ToString() ?? "all");

            // Validate and normalize parameters
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            startDate ??= DateTime.UtcNow.AddDays(-7);
            endDate ??= DateTime.UtcNow;

            // Use paginated repository method for better performance
            var fetchStopwatch = Stopwatch.StartNew();
            var (logs, totalCount) = await _requestLogRepository.GetByDateRangePaginatedAsync(
                startDate.Value, endDate.Value, page, pageSize, default);
            _metrics?.RecordFetchDuration("RequestLogRepository.GetByDateRangePaginatedAsync", fetchStopwatch.ElapsedMilliseconds);

            // Apply additional filters if needed
            if (!string.IsNullOrEmpty(model) || virtualKeyId.HasValue || status.HasValue)
            {
                var query = logs.AsEnumerable();
                
                if (!string.IsNullOrEmpty(model))
                    query = query.Where(l => l.ModelName.Contains(model, StringComparison.OrdinalIgnoreCase));
                
                if (virtualKeyId.HasValue)
                    query = query.Where(l => l.VirtualKeyId == virtualKeyId.Value);
                
                if (status.HasValue)
                    query = query.Where(l => l.StatusCode == status.Value);

                logs = query.ToList();
                // Recalculate total count after filtering
                totalCount = logs.Count;
            }

            // Map to DTOs
            var pagedLogs = logs
                .OrderByDescending(l => l.Timestamp)
                .Select(MapToLogRequestDto)
                .ToList();

            _metrics?.RecordOperationDuration("GetLogsAsync", stopwatch.ElapsedMilliseconds);

            return new PagedResult<LogRequestDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                Items = pagedLogs
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorSecure(ex, "Error getting logs");
            _metrics?.RecordOperationDuration("GetLogsAsync", stopwatch.ElapsedMilliseconds);
            
            return new PagedResult<LogRequestDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = 0,
                TotalPages = 0,
                Items = new List<LogRequestDto>()
            };
        }
    }

    /// <inheritdoc/>
    public async Task<LogRequestDto?> GetLogByIdAsync(int id)
    {
        try
        {
            _logger.LogInformationSecure("Getting log with ID: {LogId}", id);
            var log = await _requestLogRepository.GetByIdAsync(id);
            return log != null ? MapToLogRequestDto(log) : null;
        }
        catch (Exception ex)
        {
            _logger.LogErrorSecure(ex, "Error getting log with ID {LogId}", id);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> GetDistinctModelsAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var cacheHit = false;
        
        var result = await _cache.GetOrCreateAsync(CachePrefixModels, async entry =>
        {
            _metrics?.RecordCacheMiss(CachePrefixModels);
            entry.AbsoluteExpirationRelativeToNow = MediumCacheDuration;
            
            _logger.LogInformationSecure("Getting distinct models from request logs");
            
            var fetchStopwatch = Stopwatch.StartNew();
            var logs = await _requestLogRepository.GetAllAsync();
            _metrics?.RecordFetchDuration("RequestLogRepository.GetAllAsync", fetchStopwatch.ElapsedMilliseconds);
            
            return logs
                .Where(l => !string.IsNullOrEmpty(l.ModelName))
                .Select(l => l.ModelName)
                .Distinct()
                .OrderBy(m => m)
                .ToList();
        });
        
        if (!cacheHit && result != null)
        {
            cacheHit = true;
            _metrics?.RecordCacheHit(CachePrefixModels);
        }
        
        _metrics?.RecordOperationDuration("GetDistinctModelsAsync", stopwatch.ElapsedMilliseconds);
        
        return result ?? Enumerable.Empty<string>();
    }

    #endregion

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
            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

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
            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

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

        startDate ??= DateTime.UtcNow.AddDays(-30);
        endDate ??= DateTime.UtcNow;

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

        startDate ??= DateTime.UtcNow.AddDays(-30);
        endDate ??= DateTime.UtcNow;

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

    #region Private Helper Methods

    private static LogRequestDto MapToLogRequestDto(RequestLog log)
    {
        return new LogRequestDto
        {
            Id = log.Id,
            VirtualKeyId = log.VirtualKeyId,
            ModelName = log.ModelName,
            RequestType = log.RequestType,
            InputTokens = log.InputTokens,
            OutputTokens = log.OutputTokens,
            Cost = log.Cost,
            ResponseTimeMs = log.ResponseTimeMs,
            UserId = log.UserId,
            ClientIp = log.ClientIp,
            RequestPath = log.RequestPath,
            StatusCode = log.StatusCode,
            Timestamp = log.Timestamp
        };
    }

    private static string NormalizeTimeframe(string timeframe)
    {
        return timeframe.ToLower() switch
        {
            "daily" => "daily",
            "weekly" => "weekly",
            "monthly" => "monthly",
            _ => "daily"
        };
    }

    private static List<(DateTime Date, decimal Cost)> CalculateDailyCosts(IEnumerable<RequestLog> logs)
    {
        return logs
            .GroupBy(l => l.Timestamp.Date)
            .Select(g => (Date: g.Key, Cost: g.Sum(l => l.Cost)))
            .OrderBy(d => d.Date)
            .ToList();
    }

    private static List<ModelCostDetail> CalculateModelBreakdown(IEnumerable<RequestLog> logs)
    {
        return logs
            .GroupBy(l => l.ModelName)
            .Select(g => new ModelCostDetail
            {
                ModelName = g.Key,
                TotalCost = g.Sum(l => l.Cost),
                RequestCount = g.Count(),
                InputTokens = g.Sum(l => (long)l.InputTokens),
                OutputTokens = g.Sum(l => (long)l.OutputTokens),
                AverageCostPerRequest = g.Average(l => l.Cost),
                CostPercentage = 0 // Will be calculated later
            })
            .OrderByDescending(m => m.TotalCost)
            .ToList();
    }

    private static List<ProviderCostDetail> CalculateProviderBreakdown(IEnumerable<RequestLog> logs)
    {
        return logs
            .GroupBy(l => ExtractProviderFromModel(l.ModelName))
            .Select(g => new ProviderCostDetail
            {
                ProviderName = g.Key,
                TotalCost = g.Sum(l => l.Cost),
                RequestCount = g.Count(),
                AverageCostPerRequest = g.Average(l => l.Cost),
                CostPercentage = 0 // Will be calculated later
            })
            .OrderByDescending(p => p.TotalCost)
            .ToList();
    }

    private static List<VirtualKeyCostDetail> CalculateVirtualKeyBreakdown(IEnumerable<RequestLog> logs)
    {
        return logs
            .GroupBy(l => l.VirtualKeyId)
            .Select(g => new VirtualKeyCostDetail
            {
                VirtualKeyId = g.Key,
                KeyName = $"Key #{g.Key}", // Will be enriched with actual name
                TotalCost = g.Sum(l => l.Cost),
                RequestCount = g.Count(),
                AverageCostPerRequest = g.Average(l => l.Cost),
                LastUsed = g.Max(l => l.Timestamp),
                UniqueModels = g.Select(l => l.ModelName).Distinct().Count()
            })
            .OrderByDescending(v => v.TotalCost)
            .ToList();
    }

    private static string ExtractProviderFromModel(string modelName)
    {
        // Extract provider from model name (e.g., "openai/gpt-4" -> "openai")
        var parts = modelName.Split('/');
        return parts.Length > 1 ? parts[0] : "unknown";
    }

    private static decimal CalculateLast24HoursCost(IEnumerable<RequestLog> logs)
    {
        var cutoff = DateTime.UtcNow.AddDays(-1);
        return logs.Where(l => l.Timestamp >= cutoff).Sum(l => l.Cost);
    }

    private static decimal CalculateLast7DaysCost(IEnumerable<RequestLog> logs)
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);
        return logs.Where(l => l.Timestamp >= cutoff).Sum(l => l.Cost);
    }

    private static decimal CalculateLast30DaysCost(IEnumerable<RequestLog> logs)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);
        return logs.Where(l => l.Timestamp >= cutoff).Sum(l => l.Cost);
    }

    private static decimal CalculateAverageDailyCost(List<(DateTime Date, decimal Cost)> dailyCosts)
    {
        return dailyCosts.Any() ? dailyCosts.Average(d => d.Cost) : 0;
    }

    private static List<(DateTime Date, decimal Cost)> AggregateByTimeframe(
        List<(DateTime Date, decimal Cost)> dailyCosts,
        string timeframe)
    {
        return timeframe switch
        {
            "weekly" => AggregateByWeek(dailyCosts),
            "monthly" => AggregateByMonth(dailyCosts),
            _ => dailyCosts
        };
    }

    private static List<(DateTime Date, decimal Cost)> AggregateByWeek(List<(DateTime Date, decimal Cost)> dailyCosts)
    {
        return dailyCosts
            .GroupBy(d => GetStartOfWeek(d.Date))
            .Select(g => (Date: g.Key, Cost: g.Sum(d => d.Cost)))
            .OrderBy(w => w.Date)
            .ToList();
    }

    private static List<(DateTime Date, decimal Cost)> AggregateByMonth(List<(DateTime Date, decimal Cost)> dailyCosts)
    {
        return dailyCosts
            .GroupBy(d => new DateTime(d.Date.Year, d.Date.Month, 1))
            .Select(g => (Date: g.Key, Cost: g.Sum(d => d.Cost)))
            .OrderBy(m => m.Date)
            .ToList();
    }

    private static DateTime GetStartOfWeek(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-1 * diff).Date;
    }

    private List<CostTrendPoint> CalculateCostTrends(IEnumerable<RequestLog> logs, string period)
    {
        var grouped = period switch
        {
            "weekly" => logs.GroupBy(l => GetStartOfWeek(l.Timestamp.Date)),
            "monthly" => logs.GroupBy(l => new DateTime(l.Timestamp.Year, l.Timestamp.Month, 1)),
            _ => logs.GroupBy(l => l.Timestamp.Date)
        };

        return grouped
            .Select(g => new CostTrendPoint
            {
                Date = g.Key,
                Cost = g.Sum(l => l.Cost),
                RequestCount = g.Count(),
                AverageRequestCost = g.Average(l => l.Cost)
            })
            .OrderBy(t => t.Date)
            .ToList();
    }

    private List<DailyStatistics> CalculateDailyStatistics(IEnumerable<RequestLog> logs, string timeframe)
    {
        var grouped = timeframe switch
        {
            "weekly" => logs.GroupBy(l => GetStartOfWeek(l.Timestamp.Date)),
            "monthly" => logs.GroupBy(l => new DateTime(l.Timestamp.Year, l.Timestamp.Month, 1)),
            _ => logs.GroupBy(l => l.Timestamp.Date)
        };

        return grouped
            .Select(g => new DailyStatistics
            {
                Date = g.Key,
                RequestCount = g.Count(),
                Cost = g.Sum(l => l.Cost),
                InputTokens = g.Sum(l => (long)l.InputTokens),
                OutputTokens = g.Sum(l => (long)l.OutputTokens),
                AverageResponseTime = g.Average(l => l.ResponseTimeMs),
                ErrorCount = g.Count(l => l.StatusCode >= 400)
            })
            .OrderBy(s => s.Date)
            .ToList();
    }

    private async Task<PeriodComparison> CalculatePreviousPeriodComparison(DateTime startDate, DateTime endDate)
    {
        var periodLength = endDate - startDate;
        var previousStart = startDate - periodLength;
        var previousEnd = startDate;

        var currentLogs = await _requestLogRepository.GetByDateRangeAsync(startDate, endDate);
        var previousLogs = await _requestLogRepository.GetByDateRangeAsync(previousStart, previousEnd);

        var currentCost = currentLogs.Sum(l => l.Cost);
        var previousCost = previousLogs.Sum(l => l.Cost);
        var currentRequests = currentLogs.Count;
        var previousRequests = previousLogs.Count;

        return new PeriodComparison
        {
            CostChange = currentCost - previousCost,
            CostChangePercentage = previousCost > 0 ? ((currentCost - previousCost) / previousCost * 100) : 0,
            RequestChange = currentRequests - previousRequests,
            RequestChangePercentage = previousRequests > 0 ? ((decimal)(currentRequests - previousRequests) / previousRequests * 100) : 0,
            ResponseTimeChange = currentLogs.Any() && previousLogs.Any() 
                ? currentLogs.Average(l => l.ResponseTimeMs) - previousLogs.Average(l => l.ResponseTimeMs) 
                : 0,
            ErrorRateChange = CalculateErrorRateChange(currentLogs, previousLogs)
        };
    }

    private static double CalculateErrorRateChange(IList<RequestLog> currentLogs, IList<RequestLog> previousLogs)
    {
        var currentErrorRate = currentLogs.Any() 
            ? currentLogs.Count(l => l.StatusCode >= 400) * 100.0 / currentLogs.Count 
            : 0;
        var previousErrorRate = previousLogs.Any() 
            ? previousLogs.Count(l => l.StatusCode >= 400) * 100.0 / previousLogs.Count 
            : 0;
        return currentErrorRate - previousErrorRate;
    }

    private static byte[] ExportToCsv(IList<RequestLog> logs)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,VirtualKeyId,Model,RequestType,InputTokens,OutputTokens,Cost,ResponseTime,StatusCode");
        
        foreach (var log in logs)
        {
            csv.AppendLine($"{log.Timestamp:yyyy-MM-dd HH:mm:ss},{log.VirtualKeyId},{log.ModelName},{log.RequestType}," +
                          $"{log.InputTokens},{log.OutputTokens},{log.Cost:F6},{log.ResponseTimeMs:F2},{log.StatusCode}");
        }
        
        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    private static byte[] ExportToJson(IList<RequestLog> logs)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(logs.Select(MapToLogRequestDto), new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        return Encoding.UTF8.GetBytes(json);
    }

    #endregion
}