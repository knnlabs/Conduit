using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.DTOs;
using ConduitLLM.WebUI.Interfaces;

using ConfigDTO = ConduitLLM.Configuration.DTOs;
using ConfigServiceDTOs = ConduitLLM.Configuration.Services.Dtos;

namespace ConduitLLM.WebUI.Services
{
    public partial class AdminApiClient : IRequestLogService
    {
        #region IRequestLogService Implementation

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
                // Create a RequestLogDto to send to the Admin API
                var requestLogDto = new ConfigDTO.RequestLogDto
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

                // Call the Admin API to create the log
                var createdLog = await CreateRequestLogAsync(requestLogDto);

                if (createdLog == null)
                {
                    _logger.LogError("Failed to create request log for virtual key {VirtualKeyId}", virtualKeyId);
                    throw new InvalidOperationException($"Failed to create request log for virtual key {virtualKeyId}");
                }

                // Map the DTO back to the entity for returning
                var result = new RequestLog
                {
                    Id = createdLog.Id,
                    VirtualKeyId = createdLog.VirtualKeyId,
                    ModelName = createdLog.ModelName,
                    RequestType = createdLog.RequestType,
                    InputTokens = createdLog.InputTokens,
                    OutputTokens = createdLog.OutputTokens,
                    Cost = createdLog.Cost,
                    ResponseTimeMs = createdLog.ResponseTimeMs,
                    UserId = createdLog.UserId,
                    ClientIp = createdLog.ClientIp,
                    RequestPath = createdLog.RequestPath,
                    StatusCode = createdLog.StatusCode,
                    Timestamp = createdLog.Timestamp
                };

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating request log for virtual key {VirtualKeyId}", virtualKeyId);
                throw;
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
                // Call the Admin API to get the logs
                var result = await GetRequestLogsAsync(page, pageSize, virtualKeyId);

                if (result == null)
                {
                    return (new List<RequestLog>(), 0);
                }

                // Map the DTOs to entities
                var logs = new List<RequestLog>();
                foreach (var log in result.Items)
                {
                    logs.Add(new RequestLog
                    {
                        Id = log.Id,
                        VirtualKeyId = log.VirtualKeyId,
                        ModelName = log.ModelName,
                        RequestType = log.RequestType,
                        InputTokens = log.InputTokens,
                        OutputTokens = log.OutputTokens,
                        Cost = log.Cost,
                        ResponseTimeMs = log.ResponseTimeMs,
                        UserId = log.UserId,
                        ClientIp = log.ClientIp,
                        RequestPath = log.RequestPath,
                        StatusCode = log.StatusCode,
                        Timestamp = log.Timestamp
                    });
                }

                return (logs, result.TotalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting request logs for virtual key {VirtualKeyId}", virtualKeyId);
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
                // Get the key usage statistics
                var statistics = await GetVirtualKeyUsageStatisticsAsync(virtualKeyId);

                if (statistics == null || !statistics.Any())
                {
                    return null;
                }

                var keyData = statistics.FirstOrDefault();
                if (keyData == null)
                {
                    return null;
                }

                // Map to KeyUsageSummary
                return new KeyUsageSummary
                {
                    VirtualKeyId = keyData.VirtualKeyId,
                    KeyName = keyData.KeyName,
                    TotalRequests = keyData.RequestCount,
                    TotalCost = keyData.Cost,
                    TotalInputTokens = keyData.InputTokens,
                    TotalOutputTokens = keyData.OutputTokens,
                    AverageResponseTimeMs = keyData.AverageResponseTimeMs,
                    LastUsed = keyData.LastUsedAt,
                    CreatedAt = keyData.CreatedAt,
                    RequestsLast24Hours = keyData.LastDayRequests
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting key usage summary for virtual key {VirtualKeyId}", virtualKeyId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<List<KeyAggregateSummary>?> GetAllKeysUsageSummaryAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get all virtual key usage statistics
                var statistics = await GetVirtualKeyUsageStatisticsAsync();

                if (statistics == null || !statistics.Any())
                {
                    return null;
                }

                // Map to KeyAggregateSummary list
                var summaries = new List<KeyAggregateSummary>();
                foreach (var keyData in statistics)
                {
                    summaries.Add(new KeyAggregateSummary
                    {
                        VirtualKeyId = keyData.VirtualKeyId,
                        KeyName = keyData.KeyName,
                        TotalRequests = keyData.RequestCount,
                        TotalCost = keyData.Cost,
                        TotalInputTokens = keyData.InputTokens,
                        TotalOutputTokens = keyData.OutputTokens
                    });
                }

                return summaries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all keys usage summary");
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
                // Set default date range if not provided
                var start = startDate ?? DateTime.UtcNow.AddDays(-30);
                var end = endDate ?? DateTime.UtcNow;

                // Get daily usage stats from Admin API
                var stats = await GetDailyUsageStatsAsync(start, end, virtualKeyId);

                if (stats == null || !stats.Any())
                {
                    return null;
                }

                // Map to DailyUsageSummary list
                var summaries = new List<DailyUsageSummary>();
                foreach (var stat in stats)
                {
                    summaries.Add(new DailyUsageSummary
                    {
                        Date = stat.Date,
                        RequestCount = stat.RequestCount,
                        InputTokens = stat.InputTokens,
                        OutputTokens = stat.OutputTokens,
                        TotalCost = stat.Cost,
                        ModelName = stat.ModelName
                    });
                }

                return summaries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting daily usage stats");
                return null;
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
                // Call the Admin API to get the logs with filtering
                var result = await GetRequestLogsAsync(
                    pageNumber,
                    pageSize,
                    virtualKeyId,
                    modelFilter,
                    startDate,
                    endDate);

                if (result == null)
                {
                    return (new List<RequestLog>(), 0);
                }

                // Map the DTOs to entities
                var logs = new List<RequestLog>();
                foreach (var log in result.Items)
                {
                    // Filter by status code if provided (not in the API, so we do it here)
                    if (statusCode.HasValue && log.StatusCode != statusCode.Value)
                    {
                        continue;
                    }

                    logs.Add(new RequestLog
                    {
                        Id = log.Id,
                        VirtualKeyId = log.VirtualKeyId,
                        ModelName = log.ModelName,
                        RequestType = log.RequestType,
                        InputTokens = log.InputTokens,
                        OutputTokens = log.OutputTokens,
                        Cost = log.Cost,
                        ResponseTimeMs = log.ResponseTimeMs,
                        UserId = log.UserId,
                        ClientIp = log.ClientIp,
                        RequestPath = log.RequestPath,
                        StatusCode = log.StatusCode,
                        Timestamp = log.Timestamp
                    });
                }

                return (logs, result.TotalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching logs");
                return (new List<RequestLog>(), 0);
            }
        }

        /// <inheritdoc />
        public async Task<LogsSummaryDto> GetLogsSummaryAsync(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Calculate days between start and end dates
                var days = (int)Math.Ceiling((endDate - startDate).TotalDays);

                // Call the Admin API to get logs summary
                var summary = await GetLogsSummaryAsync(days);

                // Return summary or create new WebUI DTO if null  
                if (summary == null)
                {
                    return new LogsSummaryDto();
                }

                // Map Configuration DTO to WebUI DTO
                var webUiSummary = new LogsSummaryDto
                {
                    TotalRequests = summary.TotalRequests,
                    TotalInputTokens = summary.TotalInputTokens,
                    TotalOutputTokens = summary.TotalOutputTokens,
                    TotalCost = summary.TotalCost,
                    AverageResponseTimeMs = summary.AverageResponseTimeMs,
                    SuccessfulRequests = summary.SuccessfulRequests,
                    FailedRequests = summary.FailedRequests,
                    ModelBreakdown = new List<RequestsByModelDto>()
                };

                // Map RequestsByModel dictionary to ModelBreakdown list
                if (summary.RequestsByModel != null)
                {
                    foreach (var entry in summary.RequestsByModel)
                    {
                        var cost = summary.CostByModel?.ContainsKey(entry.Key) == true ? summary.CostByModel[entry.Key] : 0m;
                        webUiSummary.ModelBreakdown.Add(new RequestsByModelDto
                        {
                            ModelName = entry.Key,
                            RequestCount = entry.Value,
                            Cost = cost
                        });
                    }
                }

                return webUiSummary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting logs summary");
                return new LogsSummaryDto();
            }
        }

        /// <inheritdoc />
        public async Task<List<string>> GetDistinctModelsAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Call the Admin API to get distinct models
                var models = await GetDistinctModelsAsync();

                return models?.ToList() ?? new List<string>();
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

        #endregion
    }
}
