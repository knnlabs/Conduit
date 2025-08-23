using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Service for logging and retrieving API requests made using virtual keys
    /// </summary>
    public class RequestLogService : IRequestLogService
    {
        private readonly ConduitDbContext _context;
        private readonly ILogger<RequestLogService> _logger;

        /// <summary>
        /// Initializes a new instance of the RequestLogService
        /// </summary>
        /// <param name="context">Database context</param>
        /// <param name="logger">Logger instance</param>
        public RequestLogService(ConduitDbContext context, ILogger<RequestLogService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <inheritdoc/>
        public decimal CalculateCost(string modelName, int inputTokens, int outputTokens)
        {
            // This is a simplified implementation - in a real system,
            // you'd likely have a more sophisticated pricing model
            decimal inputRate = 0;
            decimal outputRate = 0;

            // Set rates based on model
            switch (modelName.ToLowerInvariant())
            {
                case string name when name.Contains("gpt-4"):
                    inputRate = 0.00001m;  // $0.01 per 1K tokens
                    outputRate = 0.00003m;  // $0.03 per 1K tokens
                    break;
                case string name when name.Contains("gpt-3.5"):
                    inputRate = 0.0000015m;  // $0.0015 per 1K tokens
                    outputRate = 0.000002m;  // $0.002 per 1K tokens
                    break;
                default:
                    inputRate = 0.000001m;  // Default rate
                    outputRate = 0.000002m;  // Default rate
                    break;
            }

            decimal inputCost = inputTokens * inputRate;
            decimal outputCost = outputTokens * outputRate;

            return inputCost + outputCost;
        }

        /// <inheritdoc/>
        public (int InputTokens, int OutputTokens) EstimateTokens(string requestContent, string responseContent)
        {
            // This is a simplified implementation - in a real system,
            // you'd likely use a tokenizer like GPT-2/3 BPE

            // Rough estimate: ~4 characters per token for English text
            int inputTokens = !string.IsNullOrEmpty(requestContent)
                ? (int)Math.Ceiling(requestContent.Length / 4.0)
                : 0;

            int outputTokens = !string.IsNullOrEmpty(responseContent)
                ? (int)Math.Ceiling(responseContent.Length / 4.0)
                : 0;

            return (inputTokens, outputTokens);
        }

        /// <inheritdoc/>
        public async Task<int?> GetVirtualKeyIdFromKeyValueAsync(string keyValue)
        {
            return await _context.VirtualKeys
                .AsNoTracking()
                .Where(k => k.KeyHash == keyValue)
                .Select(k => (int?)k.Id)
                .FirstOrDefaultAsync();
        }

        /// <inheritdoc/>
        public async Task<UsageStatisticsDto> GetUsageStatisticsAsync(int virtualKeyId, DateTime startDate, DateTime endDate)
        {
            // Use projection to avoid loading the entire entities into memory
            var result = new UsageStatisticsDto();

            var stats = await _context.RequestLogs
                .AsNoTracking()
                .Where(r => r.VirtualKeyId == virtualKeyId)
                .Where(r => r.Timestamp >= startDate && r.Timestamp <= endDate)
                .GroupBy(r => 1)
                .Select(g => new
                {
                    TotalRequests = g.Count(),
                    TotalCost = g.Sum(r => r.Cost),
                    TotalInputTokens = g.Sum(r => r.InputTokens),
                    TotalOutputTokens = g.Sum(r => r.OutputTokens),
                    AverageResponseTime = g.Count() > 0 ? g.Average(r => r.ResponseTimeMs) : 0
                })
                .FirstOrDefaultAsync();

            if (stats != null)
            {
                result.TotalRequests = stats.TotalRequests;
                result.TotalCost = stats.TotalCost;
                result.TotalInputTokens = stats.TotalInputTokens;
                result.TotalOutputTokens = stats.TotalOutputTokens;
                result.AverageResponseTimeMs = stats.AverageResponseTime;

                // Get model-specific usage statistics
                var modelStats = await _context.RequestLogs
                    .AsNoTracking()
                    .Where(r => r.VirtualKeyId == virtualKeyId)
                    .Where(r => r.Timestamp >= startDate && r.Timestamp <= endDate)
                    .GroupBy(r => r.ModelName)
                    .Select(g => new
                    {
                        ModelName = g.Key,
                        RequestCount = g.Count(),
                        Cost = g.Sum(r => r.Cost),
                        InputTokens = g.Sum(r => r.InputTokens),
                        OutputTokens = g.Sum(r => r.OutputTokens)
                    })
                    .ToListAsync();

                foreach (var modelStat in modelStats)
                {
                    result.ModelUsage[modelStat.ModelName] = new ModelUsage
                    {
                        RequestCount = modelStat.RequestCount,
                        Cost = modelStat.Cost,
                        InputTokens = modelStat.InputTokens,
                        OutputTokens = modelStat.OutputTokens
                    };
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public async Task LogRequestAsync(LogRequestDto request)
        {
            try
            {
                var log = new RequestLog
                {
                    VirtualKeyId = request.VirtualKeyId,
                    ModelName = request.ModelName,
                    RequestType = request.RequestType,
                    InputTokens = request.InputTokens,
                    OutputTokens = request.OutputTokens,
                    Cost = request.Cost,
                    ResponseTimeMs = request.ResponseTimeMs,
                    Timestamp = DateTime.UtcNow,
                    UserId = request.UserId,
                    ClientIp = request.ClientIp,
                    RequestPath = request.RequestPath,
                    StatusCode = request.StatusCode
                };

                _context.RequestLogs.Add(log);
                await _context.SaveChangesAsync();

                // OPTIMIZATION: Use batch spend update service instead of immediate database write
                // This reduces database load from O(n) writes per request to batch updates every 30 seconds
                _logger.LogDebug("Request logged for VirtualKeyId={VirtualKeyId}, Cost={Cost:C}, queuing spend update", 
                    request.VirtualKeyId, request.Cost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error logging request for VirtualKeyId={VirtualKeyId}, Model={Model}, RequestType={RequestType}",
                request.VirtualKeyId,
                request.ModelName.Replace(Environment.NewLine, ""),
                request.RequestType.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <summary>
        /// Optimized method to log request with batched spend updates
        /// </summary>
        /// <param name="request">Request log data</param>
        /// <param name="batchSpendService">Batch spend update service</param>
        /// <returns>Async task</returns>
        public async Task LogRequestWithBatchedSpendAsync(LogRequestDto request, BatchSpendUpdateService batchSpendService)
        {
            try
            {
                var log = new RequestLog
                {
                    VirtualKeyId = request.VirtualKeyId,
                    ModelName = request.ModelName,
                    RequestType = request.RequestType,
                    InputTokens = request.InputTokens,
                    OutputTokens = request.OutputTokens,
                    Cost = request.Cost,
                    ResponseTimeMs = request.ResponseTimeMs,
                    Timestamp = DateTime.UtcNow,
                    UserId = request.UserId,
                    ClientIp = request.ClientIp,
                    RequestPath = request.RequestPath,
                    StatusCode = request.StatusCode
                };

                _context.RequestLogs.Add(log);
                await _context.SaveChangesAsync();

                // Queue spend update for batching instead of immediate database write
                batchSpendService.QueueSpendUpdate(request.VirtualKeyId, request.Cost);

                _logger.LogDebug("Request logged and spend update queued for VirtualKeyId={VirtualKeyId}, Cost={Cost:C}", 
                    request.VirtualKeyId, request.Cost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error logging request for VirtualKeyId={VirtualKeyId}, Model={Model}, RequestType={RequestType}",
                request.VirtualKeyId,
                request.ModelName.Replace(Environment.NewLine, ""),
                request.RequestType.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <summary>
        /// Gets paged request logs for a virtual key
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID</param>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>Paged list of request logs</returns>
        public async Task<(List<RequestLog> Logs, int TotalCount)> GetPagedRequestLogsAsync(
            int virtualKeyId,
            int pageNumber = 1,
            int pageSize = 20)
        {
            var query = _context.RequestLogs
                .AsNoTracking()
                .Where(r => r.VirtualKeyId == virtualKeyId)
                .OrderByDescending(r => r.Timestamp);

            var totalCount = await query.CountAsync();

            var logs = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (logs, totalCount);
        }

        /// <inheritdoc/>
        public async Task<(List<RequestLog> Logs, int TotalCount)> SearchLogsAsync(
            int? virtualKeyId,
            string? modelFilter,
            DateTime startDate,
            DateTime endDate,
            int? statusCode,
            int pageNumber = 1,
            int pageSize = 20)
        {
            try
            {
                var query = _context.RequestLogs
                    .AsNoTracking()
                    .Include(r => r.VirtualKey)
                    .Where(r => r.Timestamp >= startDate && r.Timestamp <= endDate);

                // Apply optional filters
                if (virtualKeyId.HasValue)
                {
                    query = query.Where(r => r.VirtualKeyId == virtualKeyId.Value);
                }

                if (!string.IsNullOrWhiteSpace(modelFilter))
                {
                    query = query.Where(r => r.ModelName.Contains(modelFilter));
                }

                if (statusCode.HasValue)
                {
                    query = query.Where(r => r.StatusCode == statusCode.Value);
                }

                // Get total count before pagination
                var totalCount = await query.CountAsync();

                // Apply sorting and pagination
                var logs = await query
                    .OrderByDescending(r => r.Timestamp)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return (logs, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error searching request logs with filters: VirtualKeyId={VirtualKeyId}, ModelFilter={ModelFilter}, " +
                    "StatusCode={StatusCode}, StartDate={StartDate}, EndDate={EndDate}",
                    virtualKeyId, modelFilter, statusCode, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<LogsSummaryDto> GetLogsSummaryAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var logs = await _context.RequestLogs
                    .AsNoTracking()
                    .Include(r => r.VirtualKey)
                    .Where(r => r.Timestamp >= startDate && r.Timestamp <= endDate)
                    .ToListAsync();

                var summary = new LogsSummaryDto
                {
                    TotalRequests = logs.Count,
                    EstimatedCost = logs.Sum(r => r.Cost),
                    InputTokens = logs.Sum(r => r.InputTokens),
                    OutputTokens = logs.Sum(r => r.OutputTokens),
                    AverageResponseTime = logs.Count() > 0 ? logs.Average(r => r.ResponseTimeMs) : 0,
                    LastRequestDate = logs.Count() > 0 ? logs.Max(r => r.Timestamp) : null
                };

                // Group by model
                var modelGroups = logs
                    .GroupBy(r => r.ModelName)
                    .Select(g => new
                    {
                        ModelName = g.Key,
                        RequestCount = g.Count(),
                        TotalCost = g.Sum(r => r.Cost),
                        InputTokens = g.Sum(r => r.InputTokens),
                        OutputTokens = g.Sum(r => r.OutputTokens)
                    })
                    .OrderByDescending(g => g.RequestCount)
                    .ToList();

                foreach (var model in modelGroups)
                {
                    summary.RequestsByModel[model.ModelName] = model.RequestCount;
                    summary.CostByModel[model.ModelName] = model.TotalCost;
                }

                // Calculate success and failure counts
                summary.SuccessfulRequests = logs.Count(r => r.StatusCode.HasValue && r.StatusCode >= 200 && r.StatusCode < 300);
                summary.FailedRequests = logs.Count(r => r.StatusCode.HasValue && (r.StatusCode < 200 || r.StatusCode >= 300));

                // Group by status
                var statusGroups = logs
                    .Where(r => r.StatusCode.HasValue)
                    .GroupBy(r => r.StatusCode!.Value)
                    .Select(g => new { StatusCode = g.Key, Count = g.Count() })
                    .ToList();

                foreach (var status in statusGroups)
                {
                    summary.RequestsByStatus[status.StatusCode] = status.Count;
                }

                // Group by day and model for daily stats
                var dailyStats = logs
                    .GroupBy(r => new { Date = r.Timestamp.Date, Model = r.ModelName })
                    .Select(g => new DailyUsageStatsDto
                    {
                        Date = g.Key.Date,
                        ModelId = g.Key.Model,
                        RequestCount = g.Count(),
                        InputTokens = g.Sum(r => r.InputTokens),
                        OutputTokens = g.Sum(r => r.OutputTokens),
                        Cost = g.Sum(r => r.Cost)
                    })
                    .OrderBy(s => s.Date)
                    .ThenBy(s => s.ModelId)
                    .ToList();

                summary.DailyStats = dailyStats;

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting logs summary for period {StartDate} to {EndDate}",
                    startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetDistinctModelsAsync()
        {
            try
            {
                return await _context.RequestLogs
                    .AsNoTracking()
                    .Select(r => r.ModelName)
                    .Distinct()
                    .OrderBy(m => m)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error retrieving distinct model names from request logs");
                throw;
            }
        }
    }
}
