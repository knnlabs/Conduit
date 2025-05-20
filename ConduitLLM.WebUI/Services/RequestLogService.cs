using System.Diagnostics;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using WebUIDTOs = ConduitLLM.WebUI.DTOs;
using ConfigDTOs = ConduitLLM.Configuration.DTOs;
using ConfigServiceDTOs = ConduitLLM.Configuration.Services.Dtos;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Service for logging API requests made with virtual keys using the repository pattern
/// </summary>
public class RequestLogService : IRequestLogService
{
    private readonly IRequestLogRepository _requestLogRepository;
    private readonly IVirtualKeyRepository _virtualKeyRepository;
    private readonly ILogger<RequestLogService> _logger;
    private readonly IMemoryCache _memoryCache;

    public RequestLogService(
        IRequestLogRepository requestLogRepository,
        IVirtualKeyRepository virtualKeyRepository,
        ILogger<RequestLogService> logger,
        IMemoryCache memoryCache)
    {
        _requestLogRepository = requestLogRepository ?? throw new ArgumentNullException(nameof(requestLogRepository));
        _virtualKeyRepository = virtualKeyRepository ?? throw new ArgumentNullException(nameof(virtualKeyRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    /// <summary>
    /// Creates a new request log entry
    /// </summary>
    public async Task<RequestLog> CreateRequestLogAsync(
        int virtualKeyId,
        string modelName,
        string requestType,
        int inputTokens,
        int outputTokens,
        decimal cost,
        double responseTimeMs,
        string? userId = null,
        string? clientIp = null,
        string? requestPath = null,
        int? statusCode = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestLog = new RequestLog
            {
                VirtualKeyId = virtualKeyId,
                ModelName = modelName,
                RequestType = requestType,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                Cost = cost,
                ResponseTimeMs = responseTimeMs,
                Timestamp = DateTime.UtcNow,
                UserId = userId,
                ClientIp = clientIp,
                RequestPath = requestPath,
                StatusCode = statusCode
            };

            // Create the request log
            await _requestLogRepository.CreateAsync(requestLog, cancellationToken);
            
            // Update the virtual key's current spend
            var key = await _virtualKeyRepository.GetByIdAsync(virtualKeyId, cancellationToken);
            if (key != null)
            {
                key.CurrentSpend += cost;
                key.UpdatedAt = DateTime.UtcNow;
                await _virtualKeyRepository.UpdateAsync(key, cancellationToken);
            }
            
            // Invalidate relevant cache entries
            InvalidateKeyCache(virtualKeyId);
            
            return requestLog;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating request log for virtual key {VirtualKeyId}", virtualKeyId);
            throw;
        }
    }

    /// <summary>
    /// Gets request logs for a specific virtual key
    /// </summary>
    public async Task<(List<RequestLog> Logs, int TotalCount)> GetRequestLogsForKeyAsync(
        int virtualKeyId, 
        int page = 1, 
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get logs from the repository
            var totalCount = (await _requestLogRepository.GetByVirtualKeyIdAsync(virtualKeyId, cancellationToken)).Count;
            
            // Get paginated logs
            var skip = (page - 1) * pageSize;
            var logs = (await _requestLogRepository.GetByVirtualKeyIdAsync(virtualKeyId, cancellationToken))
                .OrderByDescending(r => r.Timestamp)
                .Skip(skip)
                .Take(pageSize)
                .ToList();
                
            return (logs, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving request logs for virtual key {VirtualKeyId}", virtualKeyId);
            throw;
        }
    }

    /// <summary>
    /// Gets summary statistics for a specific virtual key
    /// </summary>
    public async Task<WebUIDTOs.KeyUsageSummary?> GetKeyUsageSummaryAsync(int virtualKeyId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"KeyUsageSummary_{virtualKeyId}";

        // Try to get from cache first
        if (_memoryCache.TryGetValue(cacheKey, out WebUIDTOs.KeyUsageSummary? cachedSummary))
        {
            return cachedSummary;
        }

        try
        {
            // Get all logs for this key
            var logs = await _requestLogRepository.GetByVirtualKeyIdAsync(virtualKeyId, cancellationToken);

            if (logs.Count == 0)
            {
                return null;
            }

            // Calculate summary statistics
            var now = DateTime.UtcNow;
            var summary = new WebUIDTOs.KeyUsageSummary
            {
                VirtualKeyId = virtualKeyId,
                TotalRequests = logs.Count,
                TotalCost = logs.Sum(l => l.Cost),
                AverageResponseTimeMs = logs.Average(l => l.ResponseTimeMs),
                TotalInputTokens = logs.Sum(l => l.InputTokens),
                TotalOutputTokens = logs.Sum(l => l.OutputTokens),
                FirstRequestTime = logs.Min(l => l.Timestamp),
                LastRequestDate = logs.Max(l => l.Timestamp),
                RequestsLast24Hours = logs.Count(l => l.Timestamp > now.AddDays(-1)),
                RequestsLast7Days = logs.Count(l => l.Timestamp > now.AddDays(-7)),
                RequestsLast30Days = logs.Count(l => l.Timestamp > now.AddDays(-30))
            };

            // Cache the result for 10 minutes
            _memoryCache.Set(cacheKey, summary, TimeSpan.FromMinutes(10));

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving usage summary for virtual key {VirtualKeyId}", virtualKeyId);
            throw;
        }
    }

    /// <summary>
    /// Gets aggregated usage data for all virtual keys in the system
    /// </summary>
    public async Task<List<WebUIDTOs.KeyAggregateSummary>?> GetAllKeysUsageSummaryAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = "AllKeysUsageSummary";

        // Try to get from cache first
        if (_memoryCache.TryGetValue(cacheKey, out List<WebUIDTOs.KeyAggregateSummary>? cachedSummaries))
        {
            return cachedSummaries;
        }

        try
        {
            // Get all virtual keys
            var keys = await _virtualKeyRepository.GetAllAsync(cancellationToken);
            var keyDict = keys.ToDictionary(k => k.Id, k => k.KeyName);

            if (!keyDict.Any())
            {
                return new List<WebUIDTOs.KeyAggregateSummary>();
            }

            // Get all logs
            var logs = await _requestLogRepository.GetAllAsync(cancellationToken);
            var now = DateTime.UtcNow;

            // Group by virtual key
            var keyStats = logs
                .GroupBy(r => r.VirtualKeyId)
                .Select(g => new WebUIDTOs.KeyAggregateSummary
                {
                    VirtualKeyId = g.Key,
                    TotalRequests = g.Count(),
                    TotalCost = g.Sum(r => r.Cost),
                    AverageResponseTime = g.Average(r => r.ResponseTimeMs),
                    RecentRequests = g.Count(r => r.Timestamp > now.AddDays(-1))
                })
                .ToList();

            // Add key names to the summaries
            foreach (var stat in keyStats)
            {
                if (keyDict.TryGetValue(stat.VirtualKeyId, out var name))
                {
                    stat.KeyName = name;
                }
            }

            // Cache the result for 5 minutes
            _memoryCache.Set(cacheKey, keyStats, TimeSpan.FromMinutes(5));

            return keyStats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving usage summaries for all keys");
            throw;
        }
    }

    /// <summary>
    /// Gets daily usage statistics for a specific period
    /// </summary>
    public async Task<List<WebUIDTOs.DailyUsageSummary>?> GetDailyUsageStatsAsync(
        int? virtualKeyId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"DailyUsageStats_{virtualKeyId}_{startDate?.ToString("yyyyMMdd")}_{endDate?.ToString("yyyyMMdd")}";

        // Try to get from cache first
        if (_memoryCache.TryGetValue(cacheKey, out List<WebUIDTOs.DailyUsageSummary>? cachedStats))
        {
            return cachedStats;
        }

        try
        {
            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

            // Get logs for the date range
            List<RequestLog> logs;
            if (virtualKeyId.HasValue)
            {
                logs = await _requestLogRepository.GetByVirtualKeyIdAsync(virtualKeyId.Value, cancellationToken);
                logs = logs.Where(r => r.Timestamp >= startDate && r.Timestamp <= endDate).ToList();
            }
            else
            {
                logs = await _requestLogRepository.GetByDateRangeAsync(startDate.Value, endDate.Value, cancellationToken);
            }

            // Group by date
            var dailyStats = logs
                .GroupBy(r => r.Timestamp.Date)
                .Select(g => new WebUIDTOs.DailyUsageSummary
                {
                    Date = g.Key,
                    RequestCount = g.Count(),
                    TotalCost = g.Sum(r => r.Cost),
                    InputTokens = g.Sum(r => r.InputTokens),
                    OutputTokens = g.Sum(r => r.OutputTokens),
                    VirtualKeyId = virtualKeyId
                })
                .OrderBy(s => s.Date)
                .ToList();

            // Cache the result for 10 minutes
            _memoryCache.Set(cacheKey, dailyStats, TimeSpan.FromMinutes(10));

            return dailyStats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving daily usage statistics");
            throw;
        }
    }

    /// <summary>
    /// Searches for request logs with various filter criteria
    /// </summary>
    public async Task<(List<RequestLog> Logs, int TotalCount)> SearchLogsAsync(
        int? virtualKeyId,
        string? modelFilter,
        DateTime startDate,
        DateTime endDate,
        int? statusCode,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get logs based on the date range
            var logs = await _requestLogRepository.GetByDateRangeAsync(startDate, endDate, cancellationToken);
            
            // Apply filters
            if (virtualKeyId.HasValue)
            {
                logs = logs.Where(l => l.VirtualKeyId == virtualKeyId.Value).ToList();
            }
            
            if (!string.IsNullOrWhiteSpace(modelFilter))
            {
                logs = logs.Where(l => l.ModelName != null && l.ModelName.Contains(modelFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            
            if (statusCode.HasValue)
            {
                logs = logs.Where(l => l.StatusCode == statusCode.Value).ToList();
            }
            
            // Count total after filtering
            var totalCount = logs.Count;
            
            // Apply pagination
            logs = logs
                .OrderByDescending(l => l.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            
            return (logs, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching request logs");
            throw;
        }
    }

    /// <summary>
    /// Gets a summary of logs for the specified date range
    /// </summary>
    public async Task<WebUIDTOs.LogsSummaryDto> GetLogsSummaryAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get logs for the date range
            var logs = await _requestLogRepository.GetByDateRangeAsync(startDate, endDate, cancellationToken);

            // First create the service DTO with all the detailed information
            var configSummary = new ConfigServiceDTOs.LogsSummaryDto
            {
                TotalRequests = logs.Count,
                TotalInputTokens = logs.Sum(l => l.InputTokens),
                TotalOutputTokens = logs.Sum(l => l.OutputTokens),
                TotalCost = logs.Sum(l => l.Cost),
                AverageResponseTimeMs = logs.Any() ? logs.Average(l => l.ResponseTimeMs) : 0,
                StartDate = startDate,
                EndDate = endDate
            };

            // Get requests by model
            var modelGroups = logs
                .GroupBy(l => l.ModelName ?? "Unknown")
                .Select(g => new
                {
                    ModelName = g.Key,
                    RequestCount = g.Count(),
                    TotalCost = g.Sum(l => l.Cost)
                })
                .ToList();

            foreach (var model in modelGroups)
            {
                configSummary.RequestsByModelDict[model.ModelName] = model.RequestCount;
                configSummary.CostByModel[model.ModelName] = model.TotalCost;

                // Also populate the RequestsByModel list for newer API
                configSummary.RequestsByModel.Add(new ConfigServiceDTOs.RequestsByModelDto
                {
                    ModelName = model.ModelName,
                    RequestCount = model.RequestCount,
                    TotalCost = model.TotalCost
                });
            }

            // Get requests by key
            var keyGroups = logs
                .GroupBy(l => l.VirtualKeyId)
                .Select(g => new
                {
                    VirtualKeyId = g.Key,
                    RequestCount = g.Count(),
                    TotalCost = g.Sum(l => l.Cost)
                })
                .ToList();

            foreach (var keyGroup in keyGroups)
            {
                var key = await _virtualKeyRepository.GetByIdAsync(keyGroup.VirtualKeyId, cancellationToken);
                var keySummary = new ConfigServiceDTOs.KeySummary
                {
                    KeyName = key?.KeyName ?? "Unknown",
                    RequestCount = keyGroup.RequestCount,
                    TotalCost = keyGroup.TotalCost
                };

                configSummary.RequestsByKey[keyGroup.VirtualKeyId] = keySummary;
            }

            // Calculate success rate
            var successfulRequests = logs.Count(l => l.StatusCode >= 200 && l.StatusCode < 300);
            configSummary.SuccessRate = logs.Count > 0 ? (double)successfulRequests / logs.Count * 100 : 0;
            configSummary.SuccessfulRequests = successfulRequests;
            configSummary.FailedRequests = logs.Count - successfulRequests;

            // Get requests by status
            var statusGroups = logs
                .GroupBy(l => l.StatusCode ?? 0)
                .Select(g => new { StatusCode = g.Key, Count = g.Count() })
                .ToList();

            foreach (var status in statusGroups)
            {
                configSummary.RequestsByStatus[status.StatusCode] = status.Count;
            }

            // Now convert to the WebUI DTO
            var webUiSummary = new WebUIDTOs.LogsSummaryDto
            {
                TotalRequests = configSummary.TotalRequests,
                EstimatedCost = configSummary.TotalCost,
                InputTokens = configSummary.TotalInputTokens,
                OutputTokens = configSummary.TotalOutputTokens,
                AverageResponseTime = configSummary.AverageResponseTimeMs,
                StartDate = configSummary.StartDate,
                EndDate = configSummary.EndDate,
                SuccessRate = configSummary.SuccessRate,
                SuccessfulRequests = configSummary.SuccessfulRequests,
                FailedRequests = configSummary.FailedRequests
            };

            return webUiSummary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting logs summary for date range {StartDate} to {EndDate}",
                startDate, endDate);
            throw;
        }
    }

    /// <summary>
    /// Gets all distinct model names from the request logs
    /// </summary>
    public async Task<List<string>> GetDistinctModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var logs = await _requestLogRepository.GetAllAsync(cancellationToken);
            return logs
                .Select(l => l.ModelName ?? "Unknown")
                .Distinct()
                .OrderBy(m => m)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting distinct models");
            throw;
        }
    }

    /// <summary>
    /// Starts a timer for tracking request execution time
    /// </summary>
    public Stopwatch StartRequestTimer()
    {
        return Stopwatch.StartNew();
    }
    
    /// <summary>
    /// Invalidates cache entries related to a specific key
    /// </summary>
    private void InvalidateKeyCache(int keyId)
    {
        _memoryCache.Remove($"KeyUsageSummary_{keyId}");
        _memoryCache.Remove("AllKeysUsageSummary");
        // Also remove all daily usage stats entries that might include this key
        _memoryCache.Remove($"DailyUsageStats_{keyId}_");
        _memoryCache.Remove($"DailyUsageStats__"); // Stats for all keys
    }
}

/// <summary>
/// Model usage summary statistics
/// </summary>
public class ModelUsageSummary
{
    public string ModelName { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public decimal TotalCost { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}

/// <summary>
/// Virtual key usage summary statistics
/// </summary>
public class VirtualKeyUsageSummary
{
    public int VirtualKeyId { get; set; }
    public string VirtualKeyName { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public decimal TotalCost { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}