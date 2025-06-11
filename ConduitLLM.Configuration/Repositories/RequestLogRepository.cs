using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for request logs using Entity Framework Core
    /// </summary>
    public class RequestLogRepository : IRequestLogRepository
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<RequestLogRepository> _logger;

        /// <summary>
        /// Creates a new instance of the repository
        /// </summary>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        public RequestLogRepository(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<RequestLogRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<RequestLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.RequestLogs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting request log with ID {LogId}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<RequestLog>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.RequestLogs
                    .AsNoTracking()
                    .OrderByDescending(r => r.Timestamp)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all request logs");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<RequestLog>> GetByVirtualKeyIdAsync(int virtualKeyId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.RequestLogs
                    .AsNoTracking()
                    .Where(r => r.VirtualKeyId == virtualKeyId)
                    .OrderByDescending(r => r.Timestamp)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting request logs for virtual key ID {VirtualKeyId}", virtualKeyId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<RequestLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.RequestLogs
                    .AsNoTracking()
                    .Where(r => r.Timestamp >= startDate && r.Timestamp <= endDate)
                    .OrderByDescending(r => r.Timestamp)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting request logs for date range {StartDate} to {EndDate}",
                    startDate, endDate);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<RequestLog>> GetByModelAsync(string modelName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(modelName))
            {
                throw new ArgumentException("Model name cannot be null or empty", nameof(modelName));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.RequestLogs
                    .AsNoTracking()
                    .Where(r => r.ModelName == modelName)
                    .OrderByDescending(r => r.Timestamp)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting request logs for model {ModelName}", modelName);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<(List<RequestLog> Logs, int TotalCount)> GetPaginatedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            if (pageNumber < 1)
            {
                throw new ArgumentException("Page number must be greater than or equal to 1", nameof(pageNumber));
            }

            if (pageSize < 1)
            {
                throw new ArgumentException("Page size must be greater than or equal to 1", nameof(pageSize));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Get total count
                var totalCount = await dbContext.RequestLogs.CountAsync(cancellationToken);

                // Get paginated data
                var logs = await dbContext.RequestLogs
                    .AsNoTracking()
                    .OrderByDescending(r => r.Timestamp)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

                return (logs, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated request logs for page {PageNumber}, size {PageSize}",
                    pageNumber, pageSize);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> CreateAsync(RequestLog requestLog, CancellationToken cancellationToken = default)
        {
            if (requestLog == null)
            {
                throw new ArgumentNullException(nameof(requestLog));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Ensure timestamp is set
                if (requestLog.Timestamp == default)
                {
                    requestLog.Timestamp = DateTime.UtcNow;
                }

                dbContext.RequestLogs.Add(requestLog);
                await dbContext.SaveChangesAsync(cancellationToken);
                return requestLog.Id;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating request log for endpoint '{RequestPath}'",
                    requestLog.RequestPath ?? "unknown");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating request log for endpoint '{RequestPath}'",
                    requestLog.RequestPath ?? "unknown");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAsync(RequestLog requestLog, CancellationToken cancellationToken = default)
        {
            if (requestLog == null)
            {
                throw new ArgumentNullException(nameof(requestLog));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Ensure the entity is tracked
                dbContext.RequestLogs.Update(requestLog);

                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error updating request log with ID {LogId}", requestLog.Id);

                // Handle concurrency issues by reloading and reapplying changes if needed
                try
                {
                    using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                    var existingEntity = await dbContext.RequestLogs.FindAsync(new object[] { requestLog.Id }, cancellationToken);

                    if (existingEntity == null)
                    {
                        return false;
                    }

                    // Update properties
                    dbContext.Entry(existingEntity).CurrentValues.SetValues(requestLog);

                    int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                    return rowsAffected > 0;
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx, "Error during retry of request log update with ID {LogId}", requestLog.Id);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating request log with ID {LogId}",
                    requestLog.Id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var requestLog = await dbContext.RequestLogs.FindAsync(new object[] { id }, cancellationToken);

                if (requestLog == null)
                {
                    return false;
                }

                dbContext.RequestLogs.Remove(requestLog);
                int rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting request log with ID {LogId}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<UsageStatisticsDto> GetUsageStatisticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                var logs = await dbContext.RequestLogs
                    .AsNoTracking()
                    .Where(r => r.Timestamp >= startDate && r.Timestamp <= endDate)
                    .ToListAsync(cancellationToken);

                // Calculate statistics
                var totalRequests = logs.Count;
                var totalInputTokens = logs.Sum(r => r.InputTokens);
                var totalOutputTokens = logs.Sum(r => r.OutputTokens);
                var totalCost = logs.Sum(r => r.Cost);

                // Get model usage
                var modelUsageDict = logs
                    .GroupBy(r => r.ModelName)
                    .ToDictionary(
                        g => g.Key ?? "Unknown",
                        g => new ModelUsage
                        {
                            RequestCount = g.Count(),
                            Cost = g.Sum(r => r.Cost),
                            InputTokens = g.Sum(r => r.InputTokens),
                            OutputTokens = g.Sum(r => r.OutputTokens)
                        }
                    );

                // Create result
                var result = new UsageStatisticsDto
                {
                    TotalRequests = totalRequests,
                    TotalCost = totalCost,
                    AverageResponseTimeMs = logs.Any() ? logs.Average(r => r.ResponseTimeMs) : 0,
                    TotalInputTokens = logs.Sum(r => r.InputTokens),
                    TotalOutputTokens = logs.Sum(r => r.OutputTokens),
                    ModelUsage = modelUsageDict
                };

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting usage statistics for date range {StartDate} to {EndDate}",
                    startDate, endDate);
                throw;
            }
        }
    }
}
