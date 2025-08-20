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
public partial class AnalyticsService : IAnalyticsService
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



}