using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services.Dtos;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Service for logging and retrieving API requests made using virtual keys
    /// </summary>
    public class RequestLogService : IRequestLogService
    {
        private readonly ConfigurationDbContext _context;
        private readonly ILogger<RequestLogService> _logger;
        
        /// <summary>
        /// Initializes a new instance of the RequestLogService
        /// </summary>
        /// <param name="context">Database context</param>
        /// <param name="logger">Logger instance</param>
        public RequestLogService(ConfigurationDbContext context, ILogger<RequestLogService> logger)
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
                    AverageResponseTime = g.Any() ? g.Average(r => r.ResponseTimeMs) : 0
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
            // Check if the database provider supports transactions
            bool supportsTransactions = !(_context.Database.ProviderName?.Contains("InMemory") ?? false);
            
            // Create the transaction only if supported
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            if (supportsTransactions)
            {
                transaction = await _context.Database.BeginTransactionAsync();
            }
            
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
                
                // Also update the virtual key's current spend - retrieve just what we need
                var key = await _context.VirtualKeys
                    .Where(k => k.Id == request.VirtualKeyId)
                    .FirstOrDefaultAsync();
                    
                if (key != null)
                {
                    key.CurrentSpend += request.Cost;
                    key.UpdatedAt = DateTime.UtcNow;
                }
                
                await _context.SaveChangesAsync();
                
                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
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
                _logger.LogError(ex, "Error searching request logs");
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
                    TotalCost = logs.Sum(r => r.Cost),
                    TotalInputTokens = logs.Sum(r => r.InputTokens),
                    TotalOutputTokens = logs.Sum(r => r.OutputTokens),
                    AverageResponseTimeMs = logs.Any() ? logs.Average(r => r.ResponseTimeMs) : 0,
                    StartDate = startDate,
                    EndDate = endDate
                };

                // Group by model
                var modelGroups = logs
                    .GroupBy(r => r.ModelName)
                    .Select(g => new
                    {
                        ModelName = g.Key,
                        RequestCount = g.Count(),
                        TotalCost = g.Sum(r => r.Cost)
                    })
                    .OrderByDescending(g => g.RequestCount)
                    .ToList();

                foreach (var model in modelGroups)
                {
                    summary.RequestsByModel[model.ModelName] = model.RequestCount;
                    summary.CostByModel[model.ModelName] = model.TotalCost;
                }

                // Group by key
                var keyGroups = logs
                    .GroupBy(r => r.VirtualKeyId)
                    .Select(g => new
                    {
                        KeyId = g.Key,
                        KeyName = g.First().VirtualKey?.KeyName ?? "Unknown",
                        RequestCount = g.Count(),
                        TotalCost = g.Sum(r => r.Cost)
                    })
                    .ToList();

                foreach (var key in keyGroups)
                {
                    summary.RequestsByKey[key.KeyId] = new KeySummary
                    {
                        KeyName = key.KeyName,
                        RequestCount = key.RequestCount,
                        TotalCost = key.TotalCost
                    };
                }

                // Calculate success rate and group by status
                var successCount = logs.Count(r => r.StatusCode.HasValue && r.StatusCode >= 200 && r.StatusCode < 300);
                summary.SuccessRate = logs.Any() ? (double)successCount / logs.Count * 100 : 0;

                var statusGroups = logs
                    .Where(r => r.StatusCode.HasValue)
                    .GroupBy(r => r.StatusCode!.Value)
                    .Select(g => new { StatusCode = g.Key, Count = g.Count() })
                    .ToList();

                foreach (var status in statusGroups)
                {
                    summary.RequestsByStatus[status.StatusCode] = status.Count;
                }

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting logs summary");
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
                _logger.LogError(ex, "Error getting distinct models");
                throw;
            }
        }
    }
}
