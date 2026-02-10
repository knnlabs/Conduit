using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Utilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for request logs using Entity Framework Core.
    /// Extends RepositoryBase for standard CRUD operations.
    /// </summary>
    public class RequestLogRepository : RepositoryBase<RequestLog, int>, IRequestLogRepository
    {
        /// <summary>
        /// Maximum page size for request log queries
        /// </summary>
        protected override int MaxPageSize => 1000;

        /// <summary>
        /// Creates a new instance of the repository
        /// </summary>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        public RequestLogRepository(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<RequestLogRepository> logger)
            : base(dbContextFactory, logger)
        {
        }

        /// <inheritdoc/>
        protected override DbSet<RequestLog> GetDbSet(ConduitDbContext context)
        {
            return context.RequestLogs;
        }

        /// <inheritdoc/>
        protected override IQueryable<RequestLog> ApplyDefaultOrdering(IQueryable<RequestLog> query)
        {
            return query.OrderByDescending(r => r.Timestamp);
        }

        /// <inheritdoc/>
        protected override void OnBeforeCreate(RequestLog entity)
        {
            base.OnBeforeCreate(entity);

            // Ensure timestamp is set
            if (entity.Timestamp == default)
            {
                entity.Timestamp = DateTime.UtcNow;
            }
        }

        /// <inheritdoc/>
        [Obsolete("Use GetPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        public async Task<List<RequestLog>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteAsync(async context =>
                {
                    return await context.RequestLogs
                        .AsNoTracking()
                        .OrderByDescending(r => r.Timestamp)
                        .ToListAsync(cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting all request logs");
                throw;
            }
        }

        /// <inheritdoc/>
        [Obsolete("Use GetByVirtualKeyIdPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        public async Task<List<RequestLog>> GetByVirtualKeyIdAsync(int virtualKeyId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteAsync(async context =>
                {
                    return await context.RequestLogs
                        .AsNoTracking()
                        .Where(r => r.VirtualKeyId == virtualKeyId)
                        .OrderByDescending(r => r.Timestamp)
                        .ToListAsync(cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting request logs for virtual key ID {VirtualKeyId}", LoggingSanitizer.S(virtualKeyId));
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

            if (pageSize > MaxPageSize)
            {
                Logger.LogWarning("Requested page size {RequestedPageSize} exceeds maximum allowed {MaxPageSize}, limiting to maximum",
                    LoggingSanitizer.S(pageSize), LoggingSanitizer.S(MaxPageSize));
                pageSize = MaxPageSize;
            }

            try
            {
                return await ExecuteAsync(async context =>
                {
                    var query = context.RequestLogs
                        .AsNoTracking()
                        .Where(r => r.VirtualKeyId == virtualKeyId);

                    var totalCount = await query.CountAsync(cancellationToken);

                    var logs = await query
                        .OrderByDescending(r => r.Timestamp)
                        .Skip((pageNumber - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync(cancellationToken);

                    return (logs, totalCount);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting paginated request logs for virtual key ID {VirtualKeyId}, page {PageNumber}, size {PageSize}",
                    LoggingSanitizer.S(virtualKeyId), LoggingSanitizer.S(pageNumber), LoggingSanitizer.S(pageSize));
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

                return await ExecuteAsync(async context =>
                {
                    return await context.RequestLogs
                        .AsNoTracking()
                        .Where(r => r.Timestamp >= utcStartDate && r.Timestamp <= utcEndDate)
                        .OrderByDescending(r => r.Timestamp)
                        .ToListAsync(cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting request logs for date range {StartDate} to {EndDate}",
                    LoggingSanitizer.S(startDate), LoggingSanitizer.S(endDate));
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
                return await ExecuteAsync(async context =>
                {
                    return await context.RequestLogs
                        .AsNoTracking()
                        .Where(r => r.ModelName == modelName)
                        .OrderByDescending(r => r.Timestamp)
                        .ToListAsync(cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting request logs for model {ModelName}", LoggingSanitizer.S(modelName));
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

            if (pageSize > MaxPageSize)
            {
                Logger.LogWarning("Requested page size {RequestedPageSize} exceeds maximum allowed {MaxPageSize}, limiting to maximum",
                    LoggingSanitizer.S(pageSize), LoggingSanitizer.S(MaxPageSize));
                pageSize = MaxPageSize;
            }

            try
            {
                return await ExecuteAsync(async context =>
                {
                    var query = context.RequestLogs
                        .AsNoTracking()
                        .Where(r => r.ModelName == modelName);

                    var totalCount = await query.CountAsync(cancellationToken);

                    var logs = await query
                        .OrderByDescending(r => r.Timestamp)
                        .Skip((pageNumber - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync(cancellationToken);

                    return (logs, totalCount);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting paginated request logs for model {ModelName}, page {PageNumber}, size {PageSize}",
                    LoggingSanitizer.S(modelName), LoggingSanitizer.S(pageNumber), LoggingSanitizer.S(pageSize));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetDistinctModelsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteAsync(async context =>
                {
                    return await context.RequestLogs
                        .AsNoTracking()
                        .Where(r => r.ModelName != null && r.ModelName != "")
                        .Select(r => r.ModelName!)
                        .Distinct()
                        .OrderBy(m => m)
                        .ToListAsync(cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting distinct models from request logs");
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

            if (pageSize > MaxPageSize)
            {
                Logger.LogWarning("Requested page size {RequestedPageSize} exceeds maximum allowed {MaxPageSize}, limiting to maximum",
                    LoggingSanitizer.S(pageSize), LoggingSanitizer.S(MaxPageSize));
                pageSize = MaxPageSize;
            }

            try
            {
                // Ensure dates are UTC for PostgreSQL timestamp with time zone
                var utcStartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
                var utcEndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

                return await ExecuteAsync(async context =>
                {
                    // Build the query with date range filter
                    var query = context.RequestLogs
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
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting paginated request logs for date range {StartDate} to {EndDate}, page {PageNumber}, size {PageSize}",
                    LoggingSanitizer.S(startDate), LoggingSanitizer.S(endDate),
                    LoggingSanitizer.S(pageNumber), LoggingSanitizer.S(pageSize));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<UsageStatisticsDto> GetUsageStatisticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var utcStartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
                var utcEndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

                // Get summary and model breakdown via database-level aggregation
                var summaryTask = GetSummaryAsync(utcStartDate, utcEndDate, cancellationToken);
                var modelTask = GetAggregatedByModelAsync(utcStartDate, utcEndDate, cancellationToken);
                await Task.WhenAll(summaryTask, modelTask);

                var summary = summaryTask.Result;
                var modelAggregations = modelTask.Result;

                var modelUsageDict = modelAggregations.ToDictionary(
                    m => m.ModelName,
                    m => new ModelUsage
                    {
                        RequestCount = m.RequestCount,
                        Cost = m.TotalCost,
                        InputTokens = (int)Math.Min(m.InputTokens, int.MaxValue),
                        OutputTokens = (int)Math.Min(m.OutputTokens, int.MaxValue)
                    }
                );

                return new UsageStatisticsDto
                {
                    TotalRequests = summary.TotalRequests,
                    TotalCost = summary.TotalCost,
                    AverageResponseTimeMs = summary.AverageResponseTimeMs,
                    TotalInputTokens = (int)Math.Min(summary.TotalInputTokens, int.MaxValue),
                    TotalOutputTokens = (int)Math.Min(summary.TotalOutputTokens, int.MaxValue),
                    ModelUsage = modelUsageDict
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting usage statistics for date range {StartDate} to {EndDate}",
                    LoggingSanitizer.S(startDate), LoggingSanitizer.S(endDate));
                throw;
            }
        }

        #region Database-Level Aggregation Methods

        /// <inheritdoc/>
        public async Task<List<DateCostAggregation>> GetCostsByDateAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var utcStartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
                var utcEndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

                return await ExecuteAsync(async context =>
                {
                    return await context.RequestLogs
                        .AsNoTracking()
                        .Where(r => r.Timestamp >= utcStartDate && r.Timestamp <= utcEndDate)
                        .GroupBy(r => r.Timestamp.Date)
                        .Select(g => new DateCostAggregation
                        {
                            Date = g.Key,
                            TotalCost = g.Sum(r => r.Cost),
                            RequestCount = g.Count()
                        })
                        .OrderBy(d => d.Date)
                        .ToListAsync(cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting daily cost aggregations for date range {StartDate} to {EndDate}",
                    LoggingSanitizer.S(startDate), LoggingSanitizer.S(endDate));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<ModelAggregation>> GetAggregatedByModelAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var utcStartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
                var utcEndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

                return await ExecuteAsync(async context =>
                {
                    return await context.RequestLogs
                        .AsNoTracking()
                        .Where(r => r.Timestamp >= utcStartDate && r.Timestamp <= utcEndDate)
                        .GroupBy(r => r.ModelName)
                        .Select(g => new ModelAggregation
                        {
                            ModelName = g.Key ?? "Unknown",
                            TotalCost = g.Sum(r => r.Cost),
                            RequestCount = g.Count(),
                            InputTokens = g.Sum(r => (long)r.InputTokens),
                            OutputTokens = g.Sum(r => (long)r.OutputTokens)
                        })
                        .OrderByDescending(m => m.TotalCost)
                        .ToListAsync(cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting model aggregations for date range {StartDate} to {EndDate}",
                    LoggingSanitizer.S(startDate), LoggingSanitizer.S(endDate));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<ModelAggregation>> GetAggregatedByModelForVirtualKeyAsync(
            int virtualKeyId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var utcStartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
                var utcEndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

                return await ExecuteAsync(async context =>
                {
                    return await context.RequestLogs
                        .AsNoTracking()
                        .Where(r => r.Timestamp >= utcStartDate && r.Timestamp <= utcEndDate && r.VirtualKeyId == virtualKeyId)
                        .GroupBy(r => r.ModelName)
                        .Select(g => new ModelAggregation
                        {
                            ModelName = g.Key ?? "Unknown",
                            TotalCost = g.Sum(r => r.Cost),
                            RequestCount = g.Count(),
                            InputTokens = g.Sum(r => (long)r.InputTokens),
                            OutputTokens = g.Sum(r => (long)r.OutputTokens)
                        })
                        .OrderByDescending(m => m.TotalCost)
                        .ToListAsync(cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting model aggregations for virtual key {VirtualKeyId}, date range {StartDate} to {EndDate}",
                    LoggingSanitizer.S(virtualKeyId), LoggingSanitizer.S(startDate), LoggingSanitizer.S(endDate));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<VirtualKeyAggregation>> GetAggregatedByVirtualKeyAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var utcStartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
                var utcEndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

                return await ExecuteAsync(async context =>
                {
                    return await context.RequestLogs
                        .AsNoTracking()
                        .Where(r => r.Timestamp >= utcStartDate && r.Timestamp <= utcEndDate)
                        .GroupBy(r => r.VirtualKeyId)
                        .Select(g => new VirtualKeyAggregation
                        {
                            VirtualKeyId = g.Key,
                            TotalCost = g.Sum(r => r.Cost),
                            RequestCount = g.Count(),
                            LastUsed = g.Max(r => r.Timestamp),
                            UniqueModels = g.Select(r => r.ModelName).Distinct().Count()
                        })
                        .OrderByDescending(v => v.TotalCost)
                        .ToListAsync(cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting virtual key aggregations for date range {StartDate} to {EndDate}",
                    LoggingSanitizer.S(startDate), LoggingSanitizer.S(endDate));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<RequestLogSummary> GetSummaryAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var utcStartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
                var utcEndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

                return await ExecuteAsync(async context =>
                {
                    var summary = await context.RequestLogs
                        .AsNoTracking()
                        .Where(r => r.Timestamp >= utcStartDate && r.Timestamp <= utcEndDate)
                        .GroupBy(r => 1) // Single group for whole-set aggregation
                        .Select(g => new RequestLogSummary
                        {
                            TotalRequests = g.Count(),
                            TotalCost = g.Sum(r => r.Cost),
                            TotalInputTokens = g.Sum(r => (long)r.InputTokens),
                            TotalOutputTokens = g.Sum(r => (long)r.OutputTokens),
                            AverageResponseTimeMs = g.Average(r => r.ResponseTimeMs),
                            SuccessCount = g.Sum(r => (r.StatusCode ?? 0) >= 200 && (r.StatusCode ?? 0) < 300 ? 1 : 0),
                            ErrorCount = g.Sum(r => (r.StatusCode ?? 0) >= 400 ? 1 : 0)
                        })
                        .FirstOrDefaultAsync(cancellationToken);

                    return summary ?? new RequestLogSummary();
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting summary for date range {StartDate} to {EndDate}",
                    LoggingSanitizer.S(startDate), LoggingSanitizer.S(endDate));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<RequestLogSummary> GetSummaryForVirtualKeyAsync(
            int virtualKeyId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var utcStartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
                var utcEndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

                return await ExecuteAsync(async context =>
                {
                    var summary = await context.RequestLogs
                        .AsNoTracking()
                        .Where(r => r.Timestamp >= utcStartDate && r.Timestamp <= utcEndDate && r.VirtualKeyId == virtualKeyId)
                        .GroupBy(r => 1)
                        .Select(g => new RequestLogSummary
                        {
                            TotalRequests = g.Count(),
                            TotalCost = g.Sum(r => r.Cost),
                            TotalInputTokens = g.Sum(r => (long)r.InputTokens),
                            TotalOutputTokens = g.Sum(r => (long)r.OutputTokens),
                            AverageResponseTimeMs = g.Average(r => r.ResponseTimeMs),
                            SuccessCount = g.Sum(r => (r.StatusCode ?? 0) >= 200 && (r.StatusCode ?? 0) < 300 ? 1 : 0),
                            ErrorCount = g.Sum(r => (r.StatusCode ?? 0) >= 400 ? 1 : 0)
                        })
                        .FirstOrDefaultAsync(cancellationToken);

                    return summary ?? new RequestLogSummary();
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting summary for virtual key {VirtualKeyId}, date range {StartDate} to {EndDate}",
                    LoggingSanitizer.S(virtualKeyId), LoggingSanitizer.S(startDate), LoggingSanitizer.S(endDate));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<DailyStatisticsAggregation>> GetDailyStatisticsAsync(
            DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var utcStartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
                var utcEndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

                return await ExecuteAsync(async context =>
                {
                    return await context.RequestLogs
                        .AsNoTracking()
                        .Where(r => r.Timestamp >= utcStartDate && r.Timestamp <= utcEndDate)
                        .GroupBy(r => r.Timestamp.Date)
                        .Select(g => new DailyStatisticsAggregation
                        {
                            Date = g.Key,
                            RequestCount = g.Count(),
                            Cost = g.Sum(r => r.Cost),
                            InputTokens = g.Sum(r => (long)r.InputTokens),
                            OutputTokens = g.Sum(r => (long)r.OutputTokens),
                            AverageResponseTime = g.Average(r => r.ResponseTimeMs),
                            ErrorCount = g.Sum(r => (r.StatusCode ?? 0) >= 400 ? 1 : 0)
                        })
                        .OrderBy(s => s.Date)
                        .ToListAsync(cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting daily statistics for date range {StartDate} to {EndDate}",
                    LoggingSanitizer.S(startDate), LoggingSanitizer.S(endDate));
                throw;
            }
        }

        #endregion

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
                return await ExecuteAsync(async context =>
                {
                    // Find the request log by task ID in the metadata JSONB column
                    // Using PostgreSQL JSONB ->> operator to extract text value
                    var requestLog = await context.RequestLogs
                        .FromSqlRaw(
                            @"SELECT * FROM ""RequestLogs"" WHERE ""Metadata"" ->> 'taskId' = {0} LIMIT 1",
                            taskId)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (requestLog == null)
                    {
                        Logger.LogWarning("Request log not found for task ID {TaskId}", LoggingSanitizer.S(taskId));
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
                            Logger.LogWarning(ex, "Failed to parse metadata for task ID {TaskId}, skipping metadata update",
                                LoggingSanitizer.S(taskId));
                        }
                    }

                    // Save changes
                    context.RequestLogs.Update(requestLog);
                    var rowsAffected = await context.SaveChangesAsync(cancellationToken);

                    Logger.LogInformation(
                        "Updated request log for task {TaskId}: Cost=${Cost}, Model={Model}, Duration={Duration}s",
                        LoggingSanitizer.S(taskId), cost, modelName ?? requestLog.ModelName, durationSeconds);

                    return rowsAffected > 0;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error updating request log for task ID {TaskId}", LoggingSanitizer.S(taskId));
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
