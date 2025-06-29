using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Extensions;

using Microsoft.Extensions.Logging;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;

namespace ConduitLLM.Admin.Services;

/// <summary>
/// Service for managing logs through the Admin API
/// </summary>
public class AdminLogService : IAdminLogService
{
    private readonly IRequestLogRepository _requestLogRepository;
    private readonly IVirtualKeyRepository _virtualKeyRepository;
    private readonly ILogger<AdminLogService> _logger;

    /// <summary>
    /// Initializes a new instance of the AdminLogService class
    /// </summary>
    /// <param name="requestLogRepository">The request log repository</param>
    /// <param name="virtualKeyRepository">The virtual key repository</param>
    /// <param name="logger">The logger</param>
    public AdminLogService(
        IRequestLogRepository requestLogRepository,
        IVirtualKeyRepository virtualKeyRepository,
        ILogger<AdminLogService> logger)
    {
        _requestLogRepository = requestLogRepository ?? throw new ArgumentNullException(nameof(requestLogRepository));
        _virtualKeyRepository = virtualKeyRepository ?? throw new ArgumentNullException(nameof(virtualKeyRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<LogRequestDto>> GetLogsAsync(
        int page = 1,
        int pageSize = 50,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? model = null,
        int? virtualKeyId = null,
        int? status = null)
    {
        try
        {
            _logger.LogInformation(
                "Getting logs with filters - Page: {Page}, PageSize: {PageSize}, StartDate: {StartDate}, EndDate: {EndDate}, HasModel: {HasModel}, HasVirtualKeyId: {HasVirtualKeyId}, HasStatus: {HasStatus}",
                page, pageSize, startDate.HasValue ? startDate.Value : "null", endDate.HasValue ? endDate.Value : "null", !string.IsNullOrEmpty(model), virtualKeyId.HasValue, status.HasValue);

            // Validate page and pageSize
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            // Set default dates if not provided
            startDate ??= DateTime.UtcNow.AddDays(-7);
            endDate ??= DateTime.UtcNow;

            // Get logs with pagination
            // If the method doesn't exist, we can create our own implementation
            var logs = await _requestLogRepository.GetByDateRangeAsync(startDate.Value, endDate.Value);

            // Filter logs based on parameters
            if (!string.IsNullOrEmpty(model))
            {
                logs = logs.Where(l => l.ModelName == model).ToList();
            }

            if (virtualKeyId.HasValue)
            {
                logs = logs.Where(l => l.VirtualKeyId == virtualKeyId.Value).ToList();
            }

            if (status.HasValue)
            {
                logs = logs.Where(l => l.StatusCode == status.Value).ToList();
            }

            // Get total count before pagination
            var totalCount = logs.Count;

            // Apply pagination
            logs = logs
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Map to DTOs
            var logDtos = logs.Select(MapToDto).ToList();

            // Create paged result
            var result = new PagedResult<LogRequestDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                Items = logDtos
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorSecure(ex, "Error getting logs");

            // Return empty result on error
            return new PagedResult<LogRequestDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = 0,
                TotalPages = 0,
                Items = new List<LogRequestDto>()
            };
        }
    }

    /// <inheritdoc/>
    public async Task<LogRequestDto?> GetLogByIdAsync(int id)
    {
        try
        {
            _logger.LogInformationSecure("Getting log with ID: {LogId}", id);

            var log = await _requestLogRepository.GetByIdAsync(id);
            if (log == null)
            {
                _logger.LogWarningSecure("Log with ID {LogId} not found", id);
                return null;
            }

            return MapToDto(log);
        }
        catch (Exception ex)
        {
            _logger.LogErrorSecure(ex, "Error getting log with ID {LogId}", id);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<LogsSummaryDto> GetLogsSummaryAsync(
        string timeframe = "daily",
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        try
        {
_logger.LogInformationSecure("Getting logs summary with timeframe: {Timeframe}", timeframe.Replace(Environment.NewLine, ""));

            // Normalize timeframe (case-insensitive)
            timeframe = timeframe.ToLower() switch
            {
                "daily" => "daily",
                "weekly" => "weekly",
                "monthly" => "monthly",
                _ => "daily" // Default to daily if invalid
            };

            // Set default dates if not provided
            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

            // Get logs within the date range
            var logs = await _requestLogRepository.GetByDateRangeAsync(startDate.Value, endDate.Value);

            // Calculate summary statistics
            var totalRequests = logs.Count;
            var totalCost = logs.Sum(l => l.Cost);
            var totalInputTokens = logs.Sum(l => l.InputTokens);
            var totalOutputTokens = logs.Sum(l => l.OutputTokens);
            var avgResponseTime = logs.Any() ? logs.Average(l => l.ResponseTimeMs) : 0;

            // Group logs by model
            var modelGroups = logs
                .GroupBy(l => l.ModelName)
                .Select(g => new
                {
                    ModelName = g.Key,
                    RequestCount = g.Count(),
                    Cost = g.Sum(l => l.Cost)
                })
                .OrderByDescending(m => m.Cost)
                .Take(10)
                .ToList();

            // Group logs by day
            var dailyStats = logs
                .GroupBy(l => l.Timestamp.Date)
                .Select(g => new DailyStatsRecord
                {
                    Date = g.Key,
                    RequestCount = g.Count(),
                    Cost = g.Sum(l => l.Cost),
                    InputTokens = g.Sum(l => l.InputTokens),
                    OutputTokens = g.Sum(l => l.OutputTokens),
                    AvgResponseTime = g.Average(l => l.ResponseTimeMs)
                })
                .OrderBy(d => d.Date)
                .ToList();

            // Aggregate based on timeframe
            var aggregatedStats = timeframe switch
            {
                "weekly" => AggregateByWeek(dailyStats),
                "monthly" => AggregateByMonth(dailyStats),
                _ => dailyStats.ToList() // Default to daily stats
            };

            // Calculate success/failure metrics
            var successfulRequests = logs.Count(l => l.StatusCode.HasValue && l.StatusCode.Value >= 200 && l.StatusCode.Value < 400);
            var failedRequests = logs.Count(l => l.StatusCode.HasValue && l.StatusCode.Value >= 400);
            var successRate = totalRequests > 0 ? (double)successfulRequests / totalRequests * 100 : 0;

            // Group logs by status code
            var requestsByStatus = logs
                .Where(l => l.StatusCode.HasValue)
                .GroupBy(l => l.StatusCode!.Value)  // Non-null assertion is safe because of the Where clause
                .ToDictionary(g => g.Key, g => g.Count());

            // Create request by model dictionary
            var requestsByModel = new Dictionary<string, int>();
            var costByModel = new Dictionary<string, decimal>();
            foreach (var model in modelGroups)
            {
                requestsByModel[model.ModelName] = model.RequestCount;
                costByModel[model.ModelName] = model.Cost;
            }

            // Create daily usage stats DTOs
            var dailyUsageStats = aggregatedStats.Select(d => new DailyUsageStatsDto
            {
                Date = d.Date,
                RequestCount = d.RequestCount,
                Cost = d.Cost,
                InputTokens = d.InputTokens,
                OutputTokens = d.OutputTokens
            }).ToList();

            // Create logs summary DTO
            var summary = new LogsSummaryDto
            {
                TotalRequests = totalRequests,
                EstimatedCost = totalCost, // Note the property name difference
                InputTokens = totalInputTokens, // Note the property name difference
                OutputTokens = totalOutputTokens, // Note the property name difference
                AverageResponseTime = avgResponseTime, // Note the property name difference
                LastRequestDate = logs.Any() ? logs.Max(l => l.Timestamp) : null,
                SuccessfulRequests = successfulRequests,
                FailedRequests = failedRequests,
                RequestsByModel = requestsByModel,
                CostByModel = costByModel,
                RequestsByStatus = requestsByStatus,
                DailyStats = dailyUsageStats
            };

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogErrorSecure(ex, "Error getting logs summary");

            // Return empty summary on error
            return new LogsSummaryDto
            {
                TotalRequests = 0,
                EstimatedCost = 0,
                InputTokens = 0,
                OutputTokens = 0,
                AverageResponseTime = 0,
                SuccessfulRequests = 0,
                FailedRequests = 0,
                RequestsByModel = new Dictionary<string, int>(),
                CostByModel = new Dictionary<string, decimal>(),
                RequestsByStatus = new Dictionary<int, int>(),
                DailyStats = new List<DailyUsageStatsDto>()
            };
        }
    }

    /// <summary>
    /// Maps a RequestLog entity to a LogRequestDto
    /// </summary>
    /// <param name="log">The log entity to map</param>
    /// <returns>The mapped DTO</returns>
    private static Configuration.DTOs.LogRequestDto MapToDto(Configuration.Entities.RequestLog log)
    {
        return new Configuration.DTOs.LogRequestDto
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
        };
    }

    /// <summary>
    /// Daily statistics record for aggregation
    /// </summary>
    private class DailyStatsRecord
    {
        public DateTime Date { get; set; }
        public int RequestCount { get; set; }
        public decimal Cost { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public double AvgResponseTime { get; set; }
    }

    /// <summary>
    /// Aggregates daily stats by week
    /// </summary>
    /// <param name="dailyStats">The daily stats to aggregate</param>
    /// <returns>The aggregated stats</returns>
    private static List<DailyStatsRecord> AggregateByWeek(List<DailyStatsRecord> dailyStats)
    {
        return dailyStats
            .GroupBy(d => GetStartOfWeek(d.Date))
            .Select(g => new DailyStatsRecord
            {
                Date = g.Key,
                RequestCount = g.Sum(d => d.RequestCount),
                Cost = g.Sum(d => d.Cost),
                InputTokens = g.Sum(d => d.InputTokens),
                OutputTokens = g.Sum(d => d.OutputTokens),
                AvgResponseTime = g.Average(d => d.AvgResponseTime)
            })
            .OrderBy(w => w.Date)
            .ToList();
    }

    /// <summary>
    /// Aggregates daily stats by month
    /// </summary>
    /// <param name="dailyStats">The daily stats to aggregate</param>
    /// <returns>The aggregated stats</returns>
    private static List<DailyStatsRecord> AggregateByMonth(List<DailyStatsRecord> dailyStats)
    {
        return dailyStats
            .GroupBy(d => new DateTime(d.Date.Year, d.Date.Month, 1))
            .Select(g => new DailyStatsRecord
            {
                Date = g.Key,
                RequestCount = g.Sum(d => d.RequestCount),
                Cost = g.Sum(d => d.Cost),
                InputTokens = g.Sum(d => d.InputTokens),
                OutputTokens = g.Sum(d => d.OutputTokens),
                AvgResponseTime = g.Average(d => d.AvgResponseTime)
            })
            .OrderBy(m => m.Date)
            .ToList();
    }

    /// <summary>
    /// Gets the start of the week containing the specified date
    /// </summary>
    /// <param name="date">The date</param>
    /// <returns>The start of the week</returns>
    private static DateTime GetStartOfWeek(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-1 * diff).Date;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> GetDistinctModelsAsync()
    {
        try
        {
            _logger.LogInformationSecure("Getting distinct models from request logs");

            // Get all logs to extract distinct model names
            var logs = await _requestLogRepository.GetAllAsync();
            
            var distinctModels = logs
                .Where(l => !string.IsNullOrEmpty(l.ModelName))
                .Select(l => l.ModelName)
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            _logger.LogInformationSecure("Found {Count} distinct models", distinctModels.Count);
            return distinctModels;
        }
        catch (Exception ex)
        {
            _logger.LogErrorSecure(ex, "Error getting distinct models");
            return Enumerable.Empty<string>();
        }
    }
}
