using System.Diagnostics;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Service for logging API requests made with virtual keys
/// </summary>
public class RequestLogService
{
    private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
    private readonly ILogger<RequestLogService> _logger;
    private readonly IMemoryCache _memoryCache;

    public RequestLogService(
        IDbContextFactory<ConfigurationDbContext> dbContextFactory,
        ILogger<RequestLogService> logger,
        IMemoryCache memoryCache)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _memoryCache = memoryCache;
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
        int? statusCode = null)
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

            using var context = await _dbContextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();
            
            try
            {
                context.RequestLogs.Add(requestLog);
                
                // Also update the virtual key's current spend
                var key = await context.VirtualKeys
                    .Where(k => k.Id == virtualKeyId)
                    .FirstOrDefaultAsync();
                
                if (key != null)
                {
                    key.CurrentSpend += cost;
                    key.UpdatedAt = DateTime.UtcNow;
                }
                
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                // Invalidate relevant cache entries
                InvalidateKeyCache(virtualKeyId);
                
                return requestLog;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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
        int pageSize = 100)
    {
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var query = context.RequestLogs
                .AsNoTracking()
                .Where(r => r.VirtualKeyId == virtualKeyId)
                .OrderByDescending(r => r.Timestamp);
                
            var totalCount = await query.CountAsync();
            
            var skip = (page - 1) * pageSize;
            var logs = await query
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();
                
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
    public async Task<KeyUsageSummary?> GetKeyUsageSummaryAsync(int virtualKeyId)
    {
        var cacheKey = $"KeyUsageSummary_{virtualKeyId}";
        
        // Try to get from cache first
        if (_memoryCache.TryGetValue(cacheKey, out KeyUsageSummary? cachedSummary))
        {
            return cachedSummary;
        }
        
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            
            // Use server-side aggregation instead of loading all entities
            var summary = await context.RequestLogs
                .AsNoTracking()
                .Where(r => r.VirtualKeyId == virtualKeyId)
                .GroupBy(r => 1)
                .Select(g => new KeyUsageSummary
                {
                    TotalRequests = g.Count(),
                    TotalCost = g.Sum(l => l.Cost),
                    AverageResponseTime = g.Any() ? g.Average(l => l.ResponseTimeMs) : 0,
                    TotalInputTokens = g.Sum(l => l.InputTokens),
                    TotalOutputTokens = g.Sum(l => l.OutputTokens),
                    FirstRequestTime = g.Min(l => l.Timestamp),
                    LastRequestTime = g.Max(l => l.Timestamp),
                    RequestsLast24Hours = g.Count(l => l.Timestamp > DateTime.UtcNow.AddDays(-1)),
                    RequestsLast7Days = g.Count(l => l.Timestamp > DateTime.UtcNow.AddDays(-7)),
                    RequestsLast30Days = g.Count(l => l.Timestamp > DateTime.UtcNow.AddDays(-30))
                })
                .FirstOrDefaultAsync();
                
            // Cache the result for 10 minutes
            if (summary != null)
            {
                _memoryCache.Set(cacheKey, summary, TimeSpan.FromMinutes(10));
            }
            
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
    public async Task<List<KeyAggregateSummary>?> GetAllKeysUsageSummaryAsync()
    {
        var cacheKey = "AllKeysUsageSummary";
        
        // Try to get from cache first
        if (_memoryCache.TryGetValue(cacheKey, out List<KeyAggregateSummary>? cachedSummaries))
        {
            return cachedSummaries;
        }
        
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            
            // Get all virtual key IDs
            var keyIds = await context.VirtualKeys
                .AsNoTracking()
                .Select(k => new { k.Id, k.KeyName })
                .ToDictionaryAsync(k => k.Id, k => k.KeyName);
                
            if (!keyIds.Any())
            {
                return new List<KeyAggregateSummary>();
            }
            
            // Optimize by using server-side aggregation and joining results
            var keyStats = await context.RequestLogs
                .AsNoTracking()
                .GroupBy(r => r.VirtualKeyId)
                .Select(g => new KeyAggregateSummary
                {
                    VirtualKeyId = g.Key,
                    TotalRequests = g.Count(),
                    TotalCost = g.Sum(r => r.Cost),
                    AverageResponseTime = g.Average(r => r.ResponseTimeMs),
                    RecentRequests = g.Count(r => r.Timestamp > DateTime.UtcNow.AddDays(-1))
                })
                .ToListAsync();
                
            // Add key names to the summaries
            foreach (var stat in keyStats)
            {
                if (keyIds.TryGetValue(stat.VirtualKeyId, out var name))
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
    public async Task<List<DailyUsageSummary>?> GetDailyUsageStatsAsync(
        int? virtualKeyId = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var cacheKey = $"DailyUsageStats_{virtualKeyId}_{startDate?.ToString("yyyyMMdd")}_{endDate?.ToString("yyyyMMdd")}";
        
        // Try to get from cache first
        if (_memoryCache.TryGetValue(cacheKey, out List<DailyUsageSummary>? cachedStats))
        {
            return cachedStats;
        }
        
        try
        {
            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

            using var context = await _dbContextFactory.CreateDbContextAsync();
            var query = context.RequestLogs.AsNoTracking().AsQueryable();

            if (virtualKeyId.HasValue)
            {
                query = query.Where(r => r.VirtualKeyId == virtualKeyId.Value);
            }

            query = query.Where(r => r.Timestamp >= startDate && r.Timestamp <= endDate);

            var dailyStats = await query
                .GroupBy(r => r.Timestamp.Date)
                .Select(g => new DailyUsageSummary
                {
                    Date = g.Key,
                    RequestCount = g.Count(),
                    TotalCost = g.Sum(r => r.Cost),
                    InputTokens = g.Sum(r => r.InputTokens),
                    OutputTokens = g.Sum(r => r.OutputTokens)
                })
                .OrderBy(s => s.Date)
                .ToListAsync();
                
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
/// Summary of usage statistics for a virtual key
/// </summary>
public class KeyUsageSummary
{
    public int TotalRequests { get; set; }
    public decimal TotalCost { get; set; }
    public double AverageResponseTime { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public DateTime? FirstRequestTime { get; set; }
    public DateTime? LastRequestTime { get; set; }
    public int RequestsLast24Hours { get; set; }
    public int RequestsLast7Days { get; set; }
    public int RequestsLast30Days { get; set; }
}

/// <summary>
/// Aggregated summary of usage for a virtual key
/// </summary>
public class KeyAggregateSummary
{
    public int VirtualKeyId { get; set; }
    public string KeyName { get; set; } = string.Empty;
    public int TotalRequests { get; set; }
    public decimal TotalCost { get; set; }
    public double AverageResponseTime { get; set; }
    public int RecentRequests { get; set; }
}

/// <summary>
/// Daily usage summary statistics
/// </summary>
public class DailyUsageSummary
{
    public DateTime Date { get; set; }
    public int RequestCount { get; set; }
    public decimal TotalCost { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}
