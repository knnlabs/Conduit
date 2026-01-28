using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Utilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for request logs using Entity Framework Core
    /// </summary>
    public class RequestLogRepository : IRequestLogRepository
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
        private readonly ILogger<RequestLogRepository> _logger;

        /// <summary>
        /// Creates a new instance of the repository
        /// </summary>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        public RequestLogRepository(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
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
                _logger.LogError(ex, "Error getting request log with ID {LogId}", LogSanitizer.SanitizeObject(id));
                throw;
            }
        }

        /// <inheritdoc/>
        [Obsolete("Use GetPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
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
        [Obsolete("Use GetByVirtualKeyIdPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
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
                _logger.LogError(ex, "Error getting request logs for virtual key ID {VirtualKeyId}", LogSanitizer.SanitizeObject(virtualKeyId));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<(List<RequestLog> Logs, int TotalCount)> GetByVirtualKeyIdPaginatedAsync(
            int virtualKeyId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            if (pageNumber < 1)
            {
                throw new ArgumentException("Page number must be greater than or equal to 1", nameof(pageNumber));
            }

            if (pageSize < 1)
            {
                throw new ArgumentException("Page size must be greater than or equal to 1", nameof(pageSize));
            }

            const int maxPageSize = 100;
            if (pageSize > maxPageSize)
            {
                _logger.LogWarning("Requested page size {RequestedPageSize} exceeds maximum allowed {MaxPageSize}, limiting to maximum",
                    LogSanitizer.SanitizeObject(pageSize), LogSanitizer.SanitizeObject(maxPageSize));
                pageSize = maxPageSize;
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                var query = dbContext.RequestLogs
                    .AsNoTracking()
                    .Where(r => r.VirtualKeyId == virtualKeyId);

                var totalCount = await query.CountAsync(cancellationToken);

                var logs = await query
                    .OrderByDescending(r => r.Timestamp)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

                return (logs, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated request logs for virtual key ID {VirtualKeyId}, page {PageNumber}, size {PageSize}",
                    LogSanitizer.SanitizeObject(virtualKeyId), LogSanitizer.SanitizeObject(pageNumber), LogSanitizer.SanitizeObject(pageSize));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<RequestLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                // Ensure dates are UTC for PostgreSQL timestamp with time zone
                var utcStartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
                var utcEndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);
                
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.RequestLogs
                    .AsNoTracking()
                    .Where(r => r.Timestamp >= utcStartDate && r.Timestamp <= utcEndDate)
                    .OrderByDescending(r => r.Timestamp)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting request logs for date range {StartDate} to {EndDate}",
                    LogSanitizer.SanitizeObject(startDate), LogSanitizer.SanitizeObject(endDate));
                throw;
            }
        }

        /// <inheritdoc/>
        [Obsolete("Use GetByModelPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
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
                _logger.LogError(ex, "Error getting request logs for model {ModelName}", LogSanitizer.SanitizeObject(modelName));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<(List<RequestLog> Logs, int TotalCount)> GetByModelPaginatedAsync(
            string modelName,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(modelName))
            {
                throw new ArgumentException("Model name cannot be null or empty", nameof(modelName));
            }

            if (pageNumber < 1)
            {
                throw new ArgumentException("Page number must be greater than or equal to 1", nameof(pageNumber));
            }

            if (pageSize < 1)
            {
                throw new ArgumentException("Page size must be greater than or equal to 1", nameof(pageSize));
            }

            const int maxPageSize = 100;
            if (pageSize > maxPageSize)
            {
                _logger.LogWarning("Requested page size {RequestedPageSize} exceeds maximum allowed {MaxPageSize}, limiting to maximum",
                    LogSanitizer.SanitizeObject(pageSize), LogSanitizer.SanitizeObject(maxPageSize));
                pageSize = maxPageSize;
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                var query = dbContext.RequestLogs
                    .AsNoTracking()
                    .Where(r => r.ModelName == modelName);

                var totalCount = await query.CountAsync(cancellationToken);

                var logs = await query
                    .OrderByDescending(r => r.Timestamp)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

                return (logs, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated request logs for model {ModelName}, page {PageNumber}, size {PageSize}",
                    LogSanitizer.SanitizeObject(modelName), LogSanitizer.SanitizeObject(pageNumber), LogSanitizer.SanitizeObject(pageSize));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetDistinctModelsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await dbContext.RequestLogs
                    .AsNoTracking()
                    .Where(r => r.ModelName != null && r.ModelName != "")
                    .Select(r => r.ModelName!)
                    .Distinct()
                    .OrderBy(m => m)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting distinct models from request logs");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<(List<RequestLog> Logs, int TotalCount)> GetByDateRangePaginatedAsync(
            DateTime startDate, 
            DateTime endDate, 
            int pageNumber, 
            int pageSize, 
            CancellationToken cancellationToken = default)
        {
            if (pageNumber < 1)
            {
                throw new ArgumentException("Page number must be greater than or equal to 1", nameof(pageNumber));
            }

            if (pageSize < 1)
            {
                throw new ArgumentException("Page size must be greater than or equal to 1", nameof(pageSize));
            }

            // Add upper bound to prevent resource exhaustion
            const int maxPageSize = 1000;
            if (pageSize > maxPageSize)
            {
                _logger.LogWarning("Requested page size {RequestedPageSize} exceeds maximum allowed {MaxPageSize}, limiting to maximum", 
                    LogSanitizer.SanitizeObject(pageSize), LogSanitizer.SanitizeObject(maxPageSize));
                pageSize = maxPageSize;
            }

            try
            {
                // Ensure dates are UTC for PostgreSQL timestamp with time zone
                var utcStartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
                var utcEndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);
                
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Build the query with date range filter
                var query = dbContext.RequestLogs
                    .AsNoTracking()
                    .Where(r => r.Timestamp >= utcStartDate && r.Timestamp <= utcEndDate);

                // Get total count
                var totalCount = await query.CountAsync(cancellationToken);

                // Get paginated data
                var logs = await query
                    .OrderByDescending(r => r.Timestamp)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

                return (logs, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated request logs for date range {StartDate} to {EndDate}, page {PageNumber}, size {PageSize}",
                    LogSanitizer.SanitizeObject(startDate), LogSanitizer.SanitizeObject(endDate),
                    LogSanitizer.SanitizeObject(pageNumber), LogSanitizer.SanitizeObject(pageSize));
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

            // Add upper bound to prevent resource exhaustion
            const int maxPageSize = 1000;
            if (pageSize > maxPageSize)
            {
                _logger.LogWarning("Requested page size {RequestedPageSize} exceeds maximum allowed {MaxPageSize}, limiting to maximum", LogSanitizer.SanitizeObject(pageSize), LogSanitizer.SanitizeObject(maxPageSize));
                pageSize = maxPageSize;
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
                    LogSanitizer.SanitizeObject(pageNumber), LogSanitizer.SanitizeObject(pageSize));
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
                    LogSanitizer.SanitizeObject(requestLog.RequestPath ?? "unknown"));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating request log for endpoint '{RequestPath}'",
                    LogSanitizer.SanitizeObject(requestLog.RequestPath ?? "unknown"));
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
                _logger.LogError(ex, "Concurrency error updating request log with ID {LogId}", LogSanitizer.SanitizeObject(requestLog.Id));

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
                    _logger.LogError(retryEx, "Error during retry of request log update with ID {LogId}", LogSanitizer.SanitizeObject(requestLog.Id));
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating request log with ID {LogId}",
                    LogSanitizer.SanitizeObject(requestLog.Id));
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
                _logger.LogError(ex, "Error deleting request log with ID {LogId}", LogSanitizer.SanitizeObject(id));
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
                    LogSanitizer.SanitizeObject(startDate), LogSanitizer.SanitizeObject(endDate));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateCostByTaskIdAsync(
            string taskId,
            decimal cost,
            string? modelName = null,
            double? durationSeconds = null,
            string? resolution = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(taskId))
            {
                throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));
            }

            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                // Find the request log by task ID in the metadata JSONB column
                // Using PostgreSQL JSONB ->> operator to extract text value
                var requestLog = await dbContext.RequestLogs
                    .FromSqlRaw(
                        @"SELECT * FROM ""RequestLogs"" WHERE ""Metadata"" ->> 'taskId' = {0} LIMIT 1",
                        taskId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (requestLog == null)
                {
                    _logger.LogWarning("Request log not found for task ID {TaskId}", LogSanitizer.SanitizeObject(taskId));
                    return false;
                }

                // Update the cost
                requestLog.Cost = cost;

                // Update model name if provided and different
                if (!string.IsNullOrEmpty(modelName) && modelName != "unknown")
                {
                    requestLog.ModelName = modelName;
                }

                // Update metadata with actual values
                if (!string.IsNullOrEmpty(requestLog.Metadata))
                {
                    try
                    {
                        using var jsonDoc = System.Text.Json.JsonDocument.Parse(requestLog.Metadata);
                        var root = jsonDoc.RootElement;

                        // Build updated metadata
                        var updatedMetadata = new Dictionary<string, object?>();

                        // Copy existing properties
                        foreach (var prop in root.EnumerateObject())
                        {
                            updatedMetadata[prop.Name] = GetJsonElementValue(prop.Value);
                        }

                        // Update with actual values
                        if (durationSeconds.HasValue)
                        {
                            updatedMetadata["durationSeconds"] = durationSeconds.Value;
                        }
                        if (!string.IsNullOrEmpty(resolution))
                        {
                            updatedMetadata["resolution"] = resolution;
                        }
                        updatedMetadata["costCorrected"] = true;
                        updatedMetadata["costCorrectedAt"] = DateTime.UtcNow.ToString("O");

                        requestLog.Metadata = System.Text.Json.JsonSerializer.Serialize(updatedMetadata);
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse metadata for task ID {TaskId}, skipping metadata update",
                            LogSanitizer.SanitizeObject(taskId));
                    }
                }

                // Save changes
                dbContext.RequestLogs.Update(requestLog);
                var rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Updated request log for task {TaskId}: Cost=${Cost}, Model={Model}, Duration={Duration}s",
                    LogSanitizer.SanitizeObject(taskId), cost, modelName ?? requestLog.ModelName, durationSeconds);

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating request log for task ID {TaskId}", LogSanitizer.SanitizeObject(taskId));
                throw;
            }
        }

        /// <summary>
        /// Helper method to extract value from JsonElement for metadata reconstruction
        /// </summary>
        private static object? GetJsonElementValue(System.Text.Json.JsonElement element)
        {
            return element.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => element.GetString(),
                System.Text.Json.JsonValueKind.Number => element.TryGetInt64(out var longVal) ? longVal : element.GetDouble(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                System.Text.Json.JsonValueKind.Null => null,
                System.Text.Json.JsonValueKind.Array => element.EnumerateArray().Select(GetJsonElementValue).ToArray(),
                _ => element.GetRawText()
            };
        }
    }
}
