using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebUIDTOs = ConduitLLM.WebUI.DTOs;
using ConfigDTOs = ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.WebUI.Services.Providers
{
    /// <summary>
    /// Implementation of IRequestLogService that uses IAdminApiClient to interact with the Admin API.
    /// </summary>
    public class RequestLogServiceProvider : IRequestLogService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<RequestLogServiceProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestLogServiceProvider"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public RequestLogServiceProvider(
            IAdminApiClient adminApiClient,
            ILogger<RequestLogServiceProvider> logger)
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
                // Create a RequestLogDto to send to the Admin API
                var logDto = new RequestLogDto
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

                var result = await _adminApiClient.CreateRequestLogAsync(logDto);
                
                if (result == null)
                {
                    _logger.LogWarning("Failed to create request log for virtual key {VirtualKeyId} and model {ModelName}", virtualKeyId, modelName);
                    throw new InvalidOperationException("Failed to create request log");
                }
                
                // Convert the DTO back to an entity
                return new RequestLog
                {
                    Id = result.Id,
                    VirtualKeyId = result.VirtualKeyId,
                    ModelName = result.ModelName,
                    RequestType = result.RequestType,
                    InputTokens = result.InputTokens,
                    OutputTokens = result.OutputTokens,
                    Cost = result.Cost,
                    ResponseTimeMs = result.ResponseTimeMs,
                    UserId = result.UserId,
                    ClientIp = result.ClientIp,
                    RequestPath = result.RequestPath,
                    StatusCode = result.StatusCode,
                    Timestamp = result.Timestamp
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating request log for virtual key {VirtualKeyId} and model {ModelName}", virtualKeyId, modelName);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<WebUIDTOs.KeyAggregateSummary>?> GetAllKeysUsageSummaryAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Get all virtual key usage statistics from the Admin API
                var keyUsageStats = await _adminApiClient.GetVirtualKeyUsageStatisticsAsync();
                
                if (keyUsageStats == null)
                {
                    return null;
                }
                
                // Convert to KeyAggregateSummary
                return keyUsageStats.Select(dto => new WebUIDTOs.KeyAggregateSummary
                {
                    VirtualKeyId = dto.VirtualKeyId,
                    KeyName = dto.KeyName,
                    TotalRequests = dto.RequestCount,
                    TotalCost = dto.Cost, // Using ConfigDTO property
                    TotalInputTokens = dto.InputTokens,
                    TotalOutputTokens = dto.OutputTokens,
                    LastUsed = dto.LastUsedAt
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving usage summary for all keys from Admin API");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<List<WebUIDTOs.DailyUsageSummary>?> GetDailyUsageStatsAsync(
            int? virtualKeyId = null, 
            DateTime? startDate = null, 
            DateTime? endDate = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Set default dates if not provided
                startDate ??= DateTime.UtcNow.AddDays(-30);
                endDate ??= DateTime.UtcNow;
                
                // Get daily usage stats from Admin API
                var dailyStats = await _adminApiClient.GetDailyUsageStatsAsync(
                    startDate.Value, 
                    endDate.Value,
                    virtualKeyId);
                
                if (dailyStats == null)
                {
                    return null;
                }
                
                // Convert to DailyUsageSummary
                return dailyStats.Select(dto => new WebUIDTOs.DailyUsageSummary
                {
                    Date = dto.Date,
                    RequestCount = dto.RequestCount,
                    InputTokens = dto.InputTokens,
                    OutputTokens = dto.OutputTokens,
                    TotalCost = dto.Cost, // Using ConfigDTO property
                    ModelName = dto.ModelName
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving daily usage statistics from Admin API");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<List<string>> GetDistinctModelsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Get distinct models from Admin API
                var models = await _adminApiClient.GetDistinctModelsAsync();
                return models.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving distinct models from Admin API");
                return new List<string>();
            }
        }

        /// <inheritdoc />
        public async Task<WebUIDTOs.KeyUsageSummary?> GetKeyUsageSummaryAsync(int virtualKeyId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Get virtual key by ID
                var virtualKey = await _adminApiClient.GetVirtualKeyByIdAsync(virtualKeyId);
                
                if (virtualKey == null)
                {
                    return null;
                }
                
                // Get usage statistics for this key
                var keyUsageStats = (await _adminApiClient.GetVirtualKeyUsageStatisticsAsync(virtualKeyId)).FirstOrDefault();
                
                if (keyUsageStats == null)
                {
                    return new WebUIDTOs.KeyUsageSummary
                    {
                        VirtualKeyId = virtualKeyId,
                        KeyName = virtualKey.Name,
                        TotalRequests = 0,
                        TotalCost = 0,
                        TotalInputTokens = 0,
                        TotalOutputTokens = 0,
                        AverageResponseTimeMs = 0,
                        CreatedAt = virtualKey.CreatedAt,
                        LastRequestDate = null,
                        RequestsLast24Hours = 0,
                        RequestsLast7Days = 0,
                        RequestsLast30Days = 0
                    };
                }
                
                // Get logs summary over the last 7 days to calculate daily average
                var summary = await _adminApiClient.GetLogsSummaryAsync(7, virtualKeyId);
                
                // Calculate some stats based on the summary
                int days = 7; // Default to 7 days for the period we're looking at
                double dailyAvg = summary != null && days > 0 ? 
                    (double)summary.TotalRequests / days : 0;
                
                // Get logs summary for last 24 hours
                var last24HoursSummary = await _adminApiClient.GetLogsSummaryAsync(1, virtualKeyId);
                int requestsLast24Hours = last24HoursSummary?.TotalRequests ?? 0;
                
                // Get logs summary for last 30 days
                var last30DaysSummary = await _adminApiClient.GetLogsSummaryAsync(30, virtualKeyId);
                int requestsLast30Days = last30DaysSummary?.TotalRequests ?? 0;
                
                // Return the summary
                return new WebUIDTOs.KeyUsageSummary
                {
                    VirtualKeyId = virtualKeyId,
                    KeyName = virtualKey.Name,
                    TotalRequests = keyUsageStats.RequestCount,
                    TotalCost = keyUsageStats.Cost,
                    TotalInputTokens = keyUsageStats.InputTokens,
                    TotalOutputTokens = keyUsageStats.OutputTokens,
                    AverageResponseTimeMs = keyUsageStats.AverageResponseTimeMs,
                    CreatedAt = virtualKey.CreatedAt,
                    LastRequestDate = keyUsageStats.LastUsedAt,
                    RequestsLast24Hours = requestsLast24Hours,
                    RequestsLast7Days = summary?.TotalRequests ?? 0,
                    RequestsLast30Days = requestsLast30Days
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving usage summary for key {VirtualKeyId} from Admin API", virtualKeyId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<WebUIDTOs.LogsSummaryDto> GetLogsSummaryAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                // Calculate the number of days between start and end dates
                int days = (int)(endDate - startDate).TotalDays + 1;
                
                // Get logs summary from Admin API
                ConfigDTOs.LogsSummaryDto? configSummary = await _adminApiClient.GetLogsSummaryAsync(days);
                
                // Convert to WebUI DTO
                var webUiSummary = new WebUIDTOs.LogsSummaryDto
                {
                    TotalRequests = configSummary?.TotalRequests ?? 0,
                    TotalCost = configSummary?.TotalCost ?? 0,
                    TotalInputTokens = configSummary?.TotalInputTokens ?? 0,
                    TotalOutputTokens = configSummary?.TotalOutputTokens ?? 0,
                    AverageResponseTimeMs = configSummary?.AverageResponseTimeMs ?? 0,
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalDays = days,
                    DailyBreakdown = new List<WebUIDTOs.DailyStatsDto>(),
                    ModelBreakdown = new List<WebUIDTOs.RequestsByModelDto>(),
                    TopModel = null
                };
                
                // If we got data back, populate the detailed breakdowns
                if (configSummary != null)
                {
                    // Create daily breakdown if available
                    if (configSummary.DailyStats != null && configSummary.DailyStats.Count > 0)
                    {
                        foreach (var dailyStat in configSummary.DailyStats)
                        {
                            webUiSummary.DailyBreakdown.Add(new WebUIDTOs.DailyStatsDto
                            {
                                Date = dailyStat.Date,
                                RequestCount = dailyStat.RequestCount,
                                Cost = dailyStat.Cost,
                                InputTokens = dailyStat.InputTokens,
                                OutputTokens = dailyStat.OutputTokens,
                                AverageResponseTimeMs = 0 // Not available in this DTO
                            });
                        }
                    }
                    
                    // Create model breakdown if available
                    string? topModel = null;
                    int topModelCount = 0;
                    
                    foreach (var modelEntry in configSummary.RequestsByModel)
                    {
                        var modelData = new WebUIDTOs.RequestsByModelDto
                        {
                            ModelName = modelEntry.Key,
                            RequestCount = modelEntry.Value,
                            Cost = configSummary.CostByModel.ContainsKey(modelEntry.Key) ? 
                                configSummary.CostByModel[modelEntry.Key] : 0,
                            InputTokens = 0, // Not available
                            OutputTokens = 0, // Not available
                            AverageResponseTimeMs = 0 // Not available
                        };
                        
                        webUiSummary.ModelBreakdown.Add(modelData);
                        
                        // Track the most used model
                        if (modelData.RequestCount > topModelCount)
                        {
                            topModelCount = modelData.RequestCount;
                            topModel = modelData.ModelName;
                        }
                    }
                    
                    webUiSummary.TopModel = topModel;
                }
                
                return webUiSummary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving logs summary from Admin API");
                
                // Return an empty summary
                return new WebUIDTOs.LogsSummaryDto
                {
                    TotalRequests = 0,
                    TotalCost = 0,
                    TotalInputTokens = 0,
                    TotalOutputTokens = 0,
                    AverageResponseTimeMs = 0,
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalDays = (int)(endDate - startDate).TotalDays + 1,
                    DailyBreakdown = new List<WebUIDTOs.DailyStatsDto>(),
                    ModelBreakdown = new List<WebUIDTOs.RequestsByModelDto>(),
                    TopModel = null
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
                // Get paged request logs from Admin API
                var result = await _adminApiClient.GetRequestLogsAsync(
                    page,
                    pageSize,
                    virtualKeyId);
                
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
                _logger.LogError(ex, "Error retrieving request logs for key {VirtualKeyId} from Admin API", virtualKeyId);
                return (new List<RequestLog>(), 0);
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
                // Search logs from Admin API
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
                
                // Convert DTOs to entities and filter by status code if provided
                var logs = result.Items
                    .Where(dto => statusCode == null || dto.StatusCode == statusCode)
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
                
                // Adjust total count for status code filtering
                int totalCount = statusCode != null
                    ? logs.Count // If we filtered by status code, the total count is the filtered count
                    : result.TotalCount;
                
                return (logs, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching logs from Admin API");
                return (new List<RequestLog>(), 0);
            }
        }

        /// <inheritdoc />
        public Stopwatch StartRequestTimer()
        {
            // Create and start a new stopwatch
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            return stopwatch;
        }
    }
}