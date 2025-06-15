using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Audio;
using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for audio usage logging.
    /// </summary>
    public class AudioUsageLogRepository : IAudioUsageLogRepository
    {
        private readonly IConfigurationDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioUsageLogRepository"/> class.
        /// </summary>
        public AudioUsageLogRepository(IConfigurationDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public async Task<AudioUsageLog> CreateAsync(AudioUsageLog log)
        {
            log.Timestamp = DateTime.UtcNow;

            _context.AudioUsageLogs.Add(log);
            await _context.SaveChangesAsync();

            return log;
        }

        /// <inheritdoc/>
        public async Task<PagedResult<AudioUsageLog>> GetPagedAsync(AudioUsageQueryDto query)
        {
            // Ensure page size is within bounds (even though DTO validates this)
            const int maxPageSize = 1000;
            if (query.PageSize > maxPageSize)
            {
                query.PageSize = maxPageSize;
            }

            var queryable = _context.AudioUsageLogs.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(query.VirtualKey))
                queryable = queryable.Where(l => l.VirtualKey == query.VirtualKey);

            if (!string.IsNullOrEmpty(query.Provider))
                queryable = queryable.Where(l => l.Provider.ToLower() == query.Provider.ToLower());

            if (!string.IsNullOrEmpty(query.OperationType))
                queryable = queryable.Where(l => l.OperationType.ToLower() == query.OperationType.ToLower());

            if (query.StartDate.HasValue)
                queryable = queryable.Where(l => l.Timestamp >= query.StartDate.Value);

            if (query.EndDate.HasValue)
                queryable = queryable.Where(l => l.Timestamp <= query.EndDate.Value);

            if (query.OnlyErrors)
                queryable = queryable.Where(l => l.StatusCode == null || l.StatusCode >= 400);

            // Get total count
            var totalCount = await queryable.CountAsync();

            // Apply pagination
            var items = await queryable
                .OrderByDescending(l => l.Timestamp)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            return new PagedResult<AudioUsageLog>
            {
                Items = items,
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize)
            };
        }

        /// <inheritdoc/>
        public async Task<AudioUsageSummaryDto> GetUsageSummaryAsync(DateTime startDate, DateTime endDate, string? virtualKey = null, string? provider = null)
        {
            // Ensure dates are in UTC for PostgreSQL
            var utcStartDate = startDate.Kind == DateTimeKind.Utc ? startDate : DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var utcEndDate = endDate.Kind == DateTimeKind.Utc ? endDate : DateTime.SpecifyKind(endDate, DateTimeKind.Utc);
            
            var query = _context.AudioUsageLogs
                .Where(l => l.Timestamp >= utcStartDate && l.Timestamp <= utcEndDate);

            if (!string.IsNullOrEmpty(virtualKey))
                query = query.Where(l => l.VirtualKey == virtualKey);

            if (!string.IsNullOrEmpty(provider))
                query = query.Where(l => l.Provider.ToLower() == provider.ToLower());

            var logs = await query.ToListAsync();

            var summary = new AudioUsageSummaryDto
            {
                StartDate = utcStartDate,
                EndDate = utcEndDate,
                TotalOperations = logs.Count,
                SuccessfulOperations = logs.Count(l => l.StatusCode == null || (l.StatusCode >= 200 && l.StatusCode < 300)),
                FailedOperations = logs.Count(l => l.StatusCode >= 400),
                TotalCost = logs.Sum(l => l.Cost),
                TotalDurationSeconds = logs.Where(l => l.DurationSeconds.HasValue).Sum(l => l.DurationSeconds!.Value),
                TotalCharacters = logs.Where(l => l.CharacterCount.HasValue).Sum(l => (long)l.CharacterCount!.Value),
                TotalInputTokens = logs.Where(l => l.InputTokens.HasValue).Sum(l => (long)l.InputTokens!.Value),
                TotalOutputTokens = logs.Where(l => l.OutputTokens.HasValue).Sum(l => (long)l.OutputTokens!.Value)
            };

            // Get operation breakdown
            summary.OperationBreakdown = await GetOperationBreakdownAsync(startDate, endDate, virtualKey);

            // Get provider breakdown
            summary.ProviderBreakdown = await GetProviderBreakdownAsync(startDate, endDate, virtualKey);

            // Get virtual key breakdown (if not filtering by a specific key)
            if (string.IsNullOrEmpty(virtualKey))
            {
                summary.VirtualKeyBreakdown = await GetVirtualKeyBreakdownAsync(startDate, endDate, provider);
            }

            return summary;
        }

        /// <inheritdoc/>
        public async Task<List<AudioUsageLog>> GetByVirtualKeyAsync(string virtualKey, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.AudioUsageLogs.Where(l => l.VirtualKey == virtualKey);

            if (startDate.HasValue)
                query = query.Where(l => l.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(l => l.Timestamp <= endDate.Value);

            return await query.OrderByDescending(l => l.Timestamp).ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<List<AudioUsageLog>> GetByProviderAsync(string provider, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.AudioUsageLogs.Where(l => l.Provider.ToLower() == provider.ToLower());

            if (startDate.HasValue)
                query = query.Where(l => l.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(l => l.Timestamp <= endDate.Value);

            return await query.OrderByDescending(l => l.Timestamp).ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<List<AudioUsageLog>> GetBySessionIdAsync(string sessionId)
        {
            return await _context.AudioUsageLogs
                .Where(l => l.SessionId == sessionId)
                .OrderBy(l => l.Timestamp)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<decimal> GetTotalCostAsync(string virtualKey, DateTime startDate, DateTime endDate)
        {
            // Ensure dates are in UTC for PostgreSQL
            var utcStartDate = startDate.Kind == DateTimeKind.Utc ? startDate : DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var utcEndDate = endDate.Kind == DateTimeKind.Utc ? endDate : DateTime.SpecifyKind(endDate, DateTimeKind.Utc);
            
            return await _context.AudioUsageLogs
                .Where(l => l.VirtualKey == virtualKey &&
                           l.Timestamp >= utcStartDate &&
                           l.Timestamp <= utcEndDate)
                .SumAsync(l => l.Cost);
        }

        /// <inheritdoc/>
        public async Task<List<OperationTypeBreakdown>> GetOperationBreakdownAsync(DateTime startDate, DateTime endDate, string? virtualKey = null)
        {
            // Ensure dates are in UTC for PostgreSQL
            var utcStartDate = startDate.Kind == DateTimeKind.Utc ? startDate : DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var utcEndDate = endDate.Kind == DateTimeKind.Utc ? endDate : DateTime.SpecifyKind(endDate, DateTimeKind.Utc);
            
            var query = _context.AudioUsageLogs
                .Where(l => l.Timestamp >= utcStartDate && l.Timestamp <= utcEndDate);

            if (!string.IsNullOrEmpty(virtualKey))
                query = query.Where(l => l.VirtualKey == virtualKey);

            var breakdown = await query
                .GroupBy(l => l.OperationType)
                .Select(g => new OperationTypeBreakdown
                {
                    OperationType = g.Key,
                    Count = g.Count(),
                    TotalCost = g.Sum(l => l.Cost),
                    AverageCost = g.Average(l => l.Cost)
                })
                .OrderByDescending(b => b.TotalCost)
                .ToListAsync();

            return breakdown;
        }

        /// <inheritdoc/>
        public async Task<List<ProviderBreakdown>> GetProviderBreakdownAsync(DateTime startDate, DateTime endDate, string? virtualKey = null)
        {
            // Ensure dates are in UTC for PostgreSQL
            var utcStartDate = startDate.Kind == DateTimeKind.Utc ? startDate : DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var utcEndDate = endDate.Kind == DateTimeKind.Utc ? endDate : DateTime.SpecifyKind(endDate, DateTimeKind.Utc);
            
            var query = _context.AudioUsageLogs
                .Where(l => l.Timestamp >= utcStartDate && l.Timestamp <= utcEndDate);

            if (!string.IsNullOrEmpty(virtualKey))
                query = query.Where(l => l.VirtualKey == virtualKey);

            var breakdown = await query
                .GroupBy(l => l.Provider)
                .Select(g => new
                {
                    Provider = g.Key,
                    Count = g.Count(),
                    TotalCost = g.Sum(l => l.Cost),
                    SuccessCount = g.Count(l => l.StatusCode == null || (l.StatusCode >= 200 && l.StatusCode < 300))
                })
                .ToListAsync();

            return breakdown.Select(b => new ProviderBreakdown
            {
                Provider = b.Provider,
                Count = b.Count,
                TotalCost = b.TotalCost,
                SuccessRate = b.Count > 0 ? (b.SuccessCount / (double)b.Count) * 100 : 0
            })
            .OrderByDescending(b => b.TotalCost)
            .ToList();
        }

        /// <inheritdoc/>
        public async Task<List<VirtualKeyBreakdown>> GetVirtualKeyBreakdownAsync(DateTime startDate, DateTime endDate, string? provider = null)
        {
            var query = _context.AudioUsageLogs
                .Where(l => l.Timestamp >= startDate && l.Timestamp <= endDate);

            if (!string.IsNullOrEmpty(provider))
                query = query.Where(l => l.Provider.ToLower() == provider.ToLower());

            var breakdown = await query
                .GroupBy(l => l.VirtualKey)
                .Select(g => new VirtualKeyBreakdown
                {
                    VirtualKey = g.Key,
                    Count = g.Count(),
                    TotalCost = g.Sum(l => l.Cost)
                })
                .OrderByDescending(b => b.TotalCost)
                .Take(20) // Top 20 keys by cost
                .ToListAsync();

            // Optionally fetch key names from VirtualKeys table
            var keyHashes = breakdown.Select(b => b.VirtualKey).ToList();
            var keyNames = await _context.VirtualKeys
                .Where(k => keyHashes.Contains(k.KeyHash))
                .Select(k => new { k.KeyHash, k.KeyName })
                .ToDictionaryAsync(k => k.KeyHash, k => k.KeyName);

            foreach (var item in breakdown)
            {
                if (keyNames.TryGetValue(item.VirtualKey, out var name))
                {
                    item.KeyName = name;
                }
            }

            return breakdown;
        }

        /// <inheritdoc/>
        public async Task<int> DeleteOldLogsAsync(DateTime cutoffDate)
        {
            var logsToDelete = await _context.AudioUsageLogs
                .Where(l => l.Timestamp < cutoffDate)
                .ToListAsync();

            _context.AudioUsageLogs.RemoveRange(logsToDelete);
            await _context.SaveChangesAsync();

            return logsToDelete.Count;
        }
    }
}
