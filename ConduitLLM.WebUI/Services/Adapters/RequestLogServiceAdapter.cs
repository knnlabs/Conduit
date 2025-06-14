using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.DTOs;
using ConduitLLM.WebUI.Interfaces;

using Microsoft.Extensions.Logging;

using ConfigDTOs = ConduitLLM.Configuration.DTOs;
using WebUIDTOs = ConduitLLM.WebUI.DTOs;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that bridges the IRequestLogService interface with the Admin API client
    /// </summary>
    public class RequestLogServiceAdapter : IRequestLogService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<RequestLogServiceAdapter> _logger;

        public RequestLogServiceAdapter(IAdminApiClient adminApiClient, ILogger<RequestLogServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient;
            _logger = logger;
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
            var requestLogDto = new ConfigDTOs.RequestLogDto
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

            var createdLog = await _adminApiClient.CreateRequestLogAsync(requestLogDto);

            if (createdLog == null)
            {
                throw new InvalidOperationException($"Failed to create request log for virtual key {virtualKeyId}");
            }

            return new RequestLog
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
        }

        /// <inheritdoc />
        public async Task<(List<RequestLog> Logs, int TotalCount)> GetRequestLogsForKeyAsync(
            int virtualKeyId,
            int page = 1,
            int pageSize = 100,
            CancellationToken cancellationToken = default)
        {
            var result = await _adminApiClient.GetRequestLogsAsync(page, pageSize, virtualKeyId);

            if (result == null)
            {
                return (new List<RequestLog>(), 0);
            }

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

        /// <inheritdoc />
        public async Task<KeyUsageSummary?> GetKeyUsageSummaryAsync(
            int virtualKeyId,
            CancellationToken cancellationToken = default)
        {
            var statistics = await _adminApiClient.GetVirtualKeyUsageStatisticsAsync();
            var keyData = statistics?.FirstOrDefault(k => k.VirtualKeyId == virtualKeyId);

            if (keyData == null)
            {
                return null;
            }

            return new KeyUsageSummary
            {
                VirtualKeyId = keyData.VirtualKeyId,
                KeyName = keyData.KeyName,
                TotalRequests = keyData.RequestCount,
                TotalCost = keyData.TotalCost,
                TotalInputTokens = keyData.InputTokens,
                TotalOutputTokens = keyData.OutputTokens,
                AverageResponseTimeMs = keyData.AverageResponseTimeMs,
                LastUsed = keyData.LastUsedAt,
                CreatedAt = keyData.CreatedAt,
                RequestsLast24Hours = keyData.LastDayRequests
            };
        }

        /// <inheritdoc />
        public async Task<List<KeyAggregateSummary>?> GetAllKeysUsageSummaryAsync(
            CancellationToken cancellationToken = default)
        {
            var statistics = await _adminApiClient.GetVirtualKeyUsageStatisticsAsync();

            if (statistics == null || !statistics.Any())
            {
                return null;
            }

            return statistics.Select(keyData => new KeyAggregateSummary
            {
                VirtualKeyId = keyData.VirtualKeyId,
                KeyName = keyData.KeyName,
                TotalRequests = keyData.RequestCount,
                TotalCost = keyData.TotalCost,
                TotalInputTokens = keyData.InputTokens,
                TotalOutputTokens = keyData.OutputTokens
            }).ToList();
        }

        /// <inheritdoc />
        public async Task<List<DailyUsageSummary>?> GetDailyUsageStatsAsync(
            int? virtualKeyId = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            CancellationToken cancellationToken = default)
        {
            var start = startDate ?? DateTime.UtcNow.AddDays(-30);
            var end = endDate ?? DateTime.UtcNow;

            var stats = await _adminApiClient.GetDailyUsageStatsAsync(start, end, virtualKeyId);

            if (stats == null || !stats.Any())
            {
                return null;
            }

            return stats.Select(stat => new DailyUsageSummary
            {
                Date = stat.Date,
                RequestCount = stat.RequestCount,
                InputTokens = stat.InputTokens,
                OutputTokens = stat.OutputTokens,
                TotalCost = stat.Cost,
                ModelName = stat.ModelName
            }).ToList();
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
            var result = await _adminApiClient.GetRequestLogsAsync(
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

            var logs = result.Items
                .Where(log => !statusCode.HasValue || log.StatusCode == statusCode.Value)
                .Select(dto => new RequestLog
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

        /// <inheritdoc />
        public async Task<WebUIDTOs.LogsSummaryDto> GetLogsSummaryAsync(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            var days = (int)Math.Ceiling((endDate - startDate).TotalDays);
            var summary = await _adminApiClient.GetLogsSummaryAsync(days);

            if (summary == null)
            {
                return new WebUIDTOs.LogsSummaryDto();
            }

            var webUiSummary = new WebUIDTOs.LogsSummaryDto
            {
                TotalRequests = summary.TotalRequests,
                TotalInputTokens = summary.InputTokens,
                TotalOutputTokens = summary.OutputTokens,
                TotalCost = summary.EstimatedCost,
                AverageResponseTimeMs = summary.AverageResponseTime,
                StartDate = startDate,
                EndDate = endDate,
                SuccessfulRequests = summary.SuccessfulRequests,
                FailedRequests = summary.FailedRequests,
                ModelBreakdown = new List<RequestsByModelDto>()
            };

            if (summary.RequestsByModel != null)
            {
                foreach (var entry in summary.RequestsByModel)
                {
                    var cost = summary.CostByModel?.ContainsKey(entry.Key) == true
                        ? summary.CostByModel[entry.Key]
                        : 0m;

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

        /// <inheritdoc />
        public async Task<List<string>> GetDistinctModelsAsync(
            CancellationToken cancellationToken = default)
        {
            var models = await _adminApiClient.GetDistinctModelsAsync();
            return models?.ToList() ?? new List<string>();
        }

        /// <inheritdoc />
        public Stopwatch StartRequestTimer()
        {
            return Stopwatch.StartNew();
        }
    }
}
