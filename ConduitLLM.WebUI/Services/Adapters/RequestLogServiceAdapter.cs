using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.DTOs;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using WebUIDTOs = ConduitLLM.WebUI.DTOs;
using ConfigDTOs = ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that implements <see cref="IRequestLogService"/> using the Admin API client.
    /// </summary>
    public class RequestLogServiceAdapter : IRequestLogService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<RequestLogServiceAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestLogServiceAdapter"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public RequestLogServiceAdapter(
            IAdminApiClient adminApiClient,
            ILogger<RequestLogServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
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
                // Convert parameters to DTO
                var logDto = new ConfigDTOs.RequestLogDto
                {
                    VirtualKeyId = virtualKeyId,
                    ModelName = modelName,
                    RequestType = requestType,
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    Cost = cost,
                    ResponseTimeMs = responseTimeMs,
                    UserId = userId,
                    ClientIp = clientIp,
                    RequestPath = requestPath,
                    StatusCode = statusCode,
                    Timestamp = DateTime.UtcNow
                };

                // Call Admin API to create log
                var result = await _adminApiClient.CreateRequestLogAsync(logDto);
                
                // Convert back to entity for legacy compatibility
                return new RequestLog
                {
                    Id = result?.Id ?? 0,
                    VirtualKeyId = virtualKeyId,
                    ModelName = modelName,
                    RequestType = requestType,
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    Cost = cost,
                    ResponseTimeMs = responseTimeMs,
                    UserId = userId,
                    ClientIp = clientIp,
                    RequestPath = requestPath,
                    StatusCode = statusCode,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating request log");
                
                // Return a minimal entity for resilience
                return new RequestLog
                {
                    VirtualKeyId = virtualKeyId,
                    ModelName = modelName,
                    RequestType = requestType,
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    Cost = cost,
                    ResponseTimeMs = responseTimeMs,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <inheritdoc />
        public async Task<(List<RequestLog> Logs, int TotalCount)> GetRequestLogsForKeyAsync(
            int virtualKeyId, 
            int page = 1, 
            int pageSize = 100,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _adminApiClient.GetRequestLogsAsync(
                    page, pageSize, virtualKeyId, null, null, null);

                if (result == null)
                {
                    return (new List<RequestLog>(), 0);
                }

                // Convert DTOs to entities
                var logs = result.Items.Select(dto => new RequestLog
                {
                    Id = dto.Id,
                    VirtualKeyId = dto.VirtualKeyId,
                    ModelName = dto.ModelName,
                    RequestType = dto.RequestType,
                    InputTokens = dto.InputTokens,
                    OutputTokens = dto.OutputTokens,
                    Cost = dto.Cost,
                    ResponseTimeMs = dto.ResponseTimeMs,
                    UserId = dto.UserId,
                    ClientIp = dto.ClientIp,
                    RequestPath = dto.RequestPath,
                    StatusCode = dto.StatusCode,
                    Timestamp = dto.Timestamp
                }).ToList();

                return (logs, result.TotalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting request logs for key {VirtualKeyId}", virtualKeyId);
                return (new List<RequestLog>(), 0);
            }
        }

        /// <inheritdoc />
        public async Task<KeyUsageSummary?> GetKeyUsageSummaryAsync(
            int virtualKeyId, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get logs summary for this key
                var summary = await _adminApiClient.GetLogsSummaryAsync(7, virtualKeyId);
                if (summary == null)
                {
                    return null;
                }

                // Convert to KeyUsageSummary
                return new KeyUsageSummary
                {
                    VirtualKeyId = virtualKeyId,
                    TotalRequests = summary.TotalRequests,
                    TotalCost = summary.EstimatedCost,
                    TotalInputTokens = summary.InputTokens,
                    TotalOutputTokens = summary.OutputTokens,
                    LastUsed = summary.LastRequestDate
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting usage summary for key {VirtualKeyId}", virtualKeyId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<List<KeyAggregateSummary>?> GetAllKeysUsageSummaryAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get usage statistics for all keys
                var keyStats = await _adminApiClient.GetVirtualKeyUsageStatisticsAsync();
                
                // Convert to KeyAggregateSummary list
                return keyStats.Select(stat => new KeyAggregateSummary
                {
                    VirtualKeyId = stat.VirtualKeyId,
                    VirtualKeyName = stat.VirtualKeyName,
                    TotalRequests = stat.RequestCount,
                    TotalCost = stat.TotalCost,
                    IsActive = stat.IsActive
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting usage summary for all keys");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<List<DailyUsageSummary>?> GetDailyUsageStatsAsync(
            int? virtualKeyId = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Default date range if not provided
                var start = startDate ?? DateTime.UtcNow.AddDays(-30);
                var end = endDate ?? DateTime.UtcNow;
                
                // Get daily stats from Admin API
                var result = await _adminApiClient.GetDailyUsageStatsAsync(start, end, virtualKeyId);
                
                if (result == null || !result.Any())
                {
                    return new List<DailyUsageSummary>();
                }
                
                // Convert to DailyUsageSummary list
                return result.Select(dto => new DailyUsageSummary
                {
                    Date = dto.Date,
                    RequestCount = dto.RequestCount,
                    TotalTokens = dto.InputTokens + dto.OutputTokens,
                    InputTokens = dto.InputTokens,
                    OutputTokens = dto.OutputTokens,
                    TotalCost = dto.Cost
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting daily usage stats");
                return new List<DailyUsageSummary>();
            }
        }

        /// <inheritdoc />
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
                var result = await _adminApiClient.GetRequestLogsAsync(
                    pageNumber, pageSize, virtualKeyId, modelFilter, startDate, endDate);

                if (result == null)
                {
                    return (new List<RequestLog>(), 0);
                }

                // Filter by status code if provided
                var items = result.Items;
                if (statusCode.HasValue)
                {
                    items = items.Where(log => log.StatusCode == statusCode.Value).ToList();
                }

                // Convert DTOs to entities
                var logs = items.Select(dto => new RequestLog
                {
                    Id = dto.Id,
                    VirtualKeyId = dto.VirtualKeyId,
                    ModelName = dto.ModelName,
                    RequestType = dto.RequestType,
                    InputTokens = dto.InputTokens,
                    OutputTokens = dto.OutputTokens,
                    Cost = dto.Cost,
                    ResponseTimeMs = dto.ResponseTimeMs,
                    UserId = dto.UserId,
                    ClientIp = dto.ClientIp,
                    RequestPath = dto.RequestPath,
                    StatusCode = dto.StatusCode,
                    Timestamp = dto.Timestamp
                }).ToList();

                return (logs, result.TotalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching logs");
                return (new List<RequestLog>(), 0);
            }
        }

        /// <inheritdoc />
        public async Task<WebUIDTOs.LogsSummaryDto> GetLogsSummaryAsync(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Calculate days between dates for the Admin API
                int days = (int)Math.Ceiling((endDate - startDate).TotalDays);
                if (days <= 0) days = 1;

                // Use the other overload method that returns LogsSummaryDto
                var result = await GetLogsSummaryAsync(days);

                // If no result from API, return empty summary
                if (result == null)
                {
                    return new WebUIDTOs.LogsSummaryDto
                    {
                        StartDate = startDate,
                        EndDate = endDate
                    };
                }

                result.StartDate = startDate;
                result.EndDate = endDate;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting logs summary");
                return new WebUIDTOs.LogsSummaryDto
                {
                    StartDate = startDate,
                    EndDate = endDate
                };
            }
        }

        /// <inheritdoc />
        public async Task<Configuration.DTOs.PagedResult<Configuration.DTOs.RequestLogDto>> GetRequestLogsAsync(
            int page = 1,
            int pageSize = 20,
            int? virtualKeyId = null,
            string? modelId = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var result = await _adminApiClient.GetRequestLogsAsync(page, pageSize, virtualKeyId, modelId, startDate, endDate);
            return result ?? new Configuration.DTOs.PagedResult<Configuration.DTOs.RequestLogDto>
            {
                Items = new List<Configuration.DTOs.RequestLogDto>(),
                TotalCount = 0,
                PageSize = pageSize,
                CurrentPage = page,
                TotalPages = 0
            };
        }

        /// <inheritdoc />
        public async Task<WebUIDTOs.LogsSummaryDto?> GetLogsSummaryAsync(int days = 7, int? virtualKeyId = null)
        {
            var result = await _adminApiClient.GetLogsSummaryAsync(days, virtualKeyId);
            if (result == null)
            {
                return null;
            }

            // Convert from ConfigDTOs.LogsSummaryDto to WebUIDTOs.LogsSummaryDto
            return new WebUIDTOs.LogsSummaryDto
            {
                TotalRequests = result.TotalRequests,
                InputTokens = result.InputTokens,
                OutputTokens = result.OutputTokens,
                EstimatedCost = result.EstimatedCost,
                AverageResponseTime = result.AverageResponseTime,
                LastRequestDate = result.LastRequestDate,
                SuccessRate = result.SuccessRate,
                SuccessfulRequests = result.SuccessfulRequests,
                FailedRequests = result.FailedRequests
            };
        }

        /// <inheritdoc />
        public async Task<List<string>> GetDistinctModelsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _adminApiClient.GetDistinctModelsAsync();
                return result?.ToList() ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting distinct models");
                return new List<string>();
            }
        }

        /// <inheritdoc />
        public Stopwatch StartRequestTimer()
        {
            return Stopwatch.StartNew();
        }
    }
}