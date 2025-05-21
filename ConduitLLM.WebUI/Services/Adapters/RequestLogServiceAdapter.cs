using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Services;
using ConduitLLM.WebUI.DTOs;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using WebUIDTOs = ConduitLLM.WebUI.DTOs;
using ConfigDTOs = ConduitLLM.Configuration.DTOs;
using ConfigServiceDTOs = ConduitLLM.Configuration.Services.Dtos;
using IWebUIRequestLogService = ConduitLLM.WebUI.Interfaces.IRequestLogService;
using IConfigRequestLogService = ConduitLLM.Configuration.Services.IRequestLogService;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that implements IRequestLogService using the Admin API client.
    /// Also implements Configuration.Services.IRequestLogService for compatibility.
    /// </summary>
    public class RequestLogServiceAdapter : IWebUIRequestLogService, IConfigRequestLogService
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
        
        #region IWebUIRequestLogService Implementation
        
        async Task<RequestLog> IWebUIRequestLogService.CreateRequestLogAsync(
            int virtualKeyId,
            string modelName,
            string requestType,
            int inputTokens,
            int outputTokens,
            decimal cost,
            double responseTimeMs,
            string? userId,
            string? clientIp,
            string? requestPath,
            int? statusCode,
            CancellationToken cancellationToken)
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

        async Task<(List<RequestLog> Logs, int TotalCount)> IWebUIRequestLogService.GetRequestLogsForKeyAsync(
            int virtualKeyId, 
            int page, 
            int pageSize,
            CancellationToken cancellationToken)
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

        async Task<WebUIDTOs.KeyUsageSummary?> IWebUIRequestLogService.GetKeyUsageSummaryAsync(
            int virtualKeyId, 
            CancellationToken cancellationToken)
        {
            try
            {
                // Get usage statistics for this key from the Admin API
                var keyStats = await _adminApiClient.GetVirtualKeyUsageStatisticsAsync();
                var keyStat = keyStats.FirstOrDefault(k => k.VirtualKeyId == virtualKeyId);
                
                if (keyStat == null)
                {
                    return null;
                }
                
                // Convert to KeyUsageSummary with null checks for missing properties
                return new WebUIDTOs.KeyUsageSummary
                {
                    VirtualKeyId = virtualKeyId,
                    KeyName = keyStat.KeyName,
                    TotalRequests = keyStat.RequestCount,
                    TotalCost = keyStat.TotalCost,
                    // Using extended properties that match the DTO properties
                    AverageResponseTimeMs = keyStat.AverageResponseTimeMs,
                    TotalInputTokens = keyStat.InputTokens,
                    TotalOutputTokens = keyStat.OutputTokens,
                    LastRequestTime = keyStat.LastRequestTime,
                    LastRequestDate = keyStat.LastRequestDate,
                    FirstRequestTime = keyStat.FirstRequestTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting usage summary for key {VirtualKeyId}", virtualKeyId);
                return null;
            }
        }

        async Task<List<WebUIDTOs.KeyAggregateSummary>?> IWebUIRequestLogService.GetAllKeysUsageSummaryAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                // Get usage statistics for all keys
                var keyStats = await _adminApiClient.GetVirtualKeyUsageStatisticsAsync();
                
                // Convert to KeyAggregateSummary list with null/missing property handling
                return keyStats.Select(stat => new WebUIDTOs.KeyAggregateSummary
                {
                    VirtualKeyId = stat.VirtualKeyId,
                    KeyName = stat.KeyName,
                    TotalRequests = stat.RequestCount,
                    TotalCost = stat.TotalCost,
                    // Get properties that are available in the DTO
                    AverageResponseTime = stat.AverageResponseTimeMs,
                    RecentRequests = stat.LastDayRequests
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting usage summary for all keys");
                return null;
            }
        }

        async Task<List<WebUIDTOs.DailyUsageSummary>?> IWebUIRequestLogService.GetDailyUsageStatsAsync(
            int? virtualKeyId,
            DateTime? startDate,
            DateTime? endDate,
            CancellationToken cancellationToken)
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
                    return new List<WebUIDTOs.DailyUsageSummary>();
                }
                
                // Convert to DailyUsageSummary list
                return result.Select(dto => new WebUIDTOs.DailyUsageSummary
                {
                    Date = dto.Date,
                    RequestCount = dto.RequestCount,
                    TotalTokens = dto.InputTokens + dto.OutputTokens,
                    InputTokens = dto.InputTokens,
                    OutputTokens = dto.OutputTokens,
                    TotalCost = dto.Cost,
                    VirtualKeyId = virtualKeyId
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting daily usage stats");
                return new List<WebUIDTOs.DailyUsageSummary>();
            }
        }

        async Task<(List<RequestLog> Logs, int TotalCount)> IWebUIRequestLogService.SearchLogsAsync(
            int? virtualKeyId,
            string? modelFilter,
            DateTime startDate,
            DateTime endDate,
            int? statusCode,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken)
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

        async Task<WebUIDTOs.LogsSummaryDto> IWebUIRequestLogService.GetLogsSummaryAsync(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken)
        {
            try
            {
                // Calculate days between dates for the Admin API
                int days = (int)Math.Ceiling((endDate - startDate).TotalDays);
                if (days <= 0) days = 1;

                var result = await _adminApiClient.GetLogsSummaryAsync(days);

                if (result == null)
                {
                    return new WebUIDTOs.LogsSummaryDto
                    {
                        StartDate = startDate,
                        EndDate = endDate
                    };
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
                    StartDate = startDate,
                    EndDate = endDate,
                    SuccessRate = result.SuccessRate,
                    SuccessfulRequests = result.SuccessfulRequests,
                    FailedRequests = result.FailedRequests
                };
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

        async Task<List<string>> IWebUIRequestLogService.GetDistinctModelsAsync(
            CancellationToken cancellationToken)
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

        Stopwatch IWebUIRequestLogService.StartRequestTimer()
        {
            return Stopwatch.StartNew();
        }
        
        #endregion
        
        #region IConfigRequestLogService Implementation
        
        async Task IConfigRequestLogService.LogRequestAsync(LogRequestDto request)
        {
            try
            {
                await _adminApiClient.CreateRequestLogAsync(new ConfigDTOs.RequestLogDto
                {
                    VirtualKeyId = request.VirtualKeyId,
                    ModelName = request.ModelName,
                    RequestType = request.RequestType,
                    InputTokens = request.InputTokens,
                    OutputTokens = request.OutputTokens,
                    Cost = request.Cost,
                    ResponseTimeMs = request.ResponseTimeMs,
                    UserId = request.UserId,
                    ClientIp = request.ClientIp,
                    RequestPath = request.RequestPath,
                    StatusCode = request.StatusCode,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging request");
            }
        }

        async Task<int?> IConfigRequestLogService.GetVirtualKeyIdFromKeyValueAsync(string keyValue)
        {
            try 
            {
                // Admin API doesn't have this functionality - we'd need direct database access
                // Log a warning and return null
                _logger.LogWarning("Cannot get virtual key ID from key value in API mode - operation requires direct database access");
                
                // Add await to make this properly async
                await Task.CompletedTask;
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting virtual key ID from value");
                return null;
            }
        }

        (int InputTokens, int OutputTokens) IConfigRequestLogService.EstimateTokens(string requestContent, string responseContent)
        {
            // Simple estimation based on character count
            // In a real implementation, you'd use a proper tokenizer
            const int charsPerToken = 4;
            
            int inputTokens = string.IsNullOrEmpty(requestContent) 
                ? 0 
                : (int)Math.Ceiling(requestContent.Length / (double)charsPerToken);
                
            int outputTokens = string.IsNullOrEmpty(responseContent) 
                ? 0 
                : (int)Math.Ceiling(responseContent.Length / (double)charsPerToken);
                
            return (inputTokens, outputTokens);
        }

        decimal IConfigRequestLogService.CalculateCost(string modelName, int inputTokens, int outputTokens)
        {
            try
            {
                // Use default costs for simplification
                decimal inputCostPer1K = 0.01m;  // Default $0.01 per 1K tokens
                decimal outputCostPer1K = 0.03m; // Default $0.03 per 1K tokens
                
                // Default model prices based on model name patterns
                if (modelName.Contains("gpt-3.5", StringComparison.OrdinalIgnoreCase))
                {
                    inputCostPer1K = 0.001m;   // $0.001 per 1K tokens
                    outputCostPer1K = 0.002m;  // $0.002 per 1K tokens
                }
                else if (modelName.Contains("gpt-4", StringComparison.OrdinalIgnoreCase))
                {
                    inputCostPer1K = 0.03m;   // $0.03 per 1K tokens
                    outputCostPer1K = 0.06m;  // $0.06 per 1K tokens
                }
                else if (modelName.Contains("claude-3", StringComparison.OrdinalIgnoreCase) || 
                         modelName.Contains("anthropic", StringComparison.OrdinalIgnoreCase))
                {
                    inputCostPer1K = 0.015m;  // $0.015 per 1K tokens
                    outputCostPer1K = 0.075m; // $0.075 per 1K tokens
                }
                else if (modelName.Contains("gemini", StringComparison.OrdinalIgnoreCase))
                {
                    inputCostPer1K = 0.0025m;  // $0.0025 per 1K tokens
                    outputCostPer1K = 0.0075m; // $0.0075 per 1K tokens
                }
                
                // Calculate costs
                decimal inputCost = inputTokens * (inputCostPer1K / 1000);
                decimal outputCost = outputTokens * (outputCostPer1K / 1000);
                
                return inputCost + outputCost;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating cost for model {ModelName}", modelName);
                
                // Fallback to simple estimation
                return (inputTokens + outputTokens * 3) * 0.00002m;
            }
        }

        async Task<ConfigDTOs.UsageStatisticsDto> IConfigRequestLogService.GetUsageStatisticsAsync(int virtualKeyId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var summary = await _adminApiClient.GetLogsSummaryAsync(
                    (int)Math.Ceiling((endDate - startDate).TotalDays), 
                    virtualKeyId);
                    
                if (summary == null)
                {
                    return new ConfigDTOs.UsageStatisticsDto
                    {
                        TotalRequests = 0,
                        TotalCost = 0,
                        TotalInputTokens = 0,
                        TotalOutputTokens = 0,
                        AverageResponseTimeMs = 0,
                        ModelUsage = new Dictionary<string, ConfigDTOs.ModelUsage>()
                    };
                }
                
                return new ConfigDTOs.UsageStatisticsDto
                {
                    TotalRequests = summary.TotalRequests,
                    TotalCost = summary.EstimatedCost,
                    TotalInputTokens = summary.InputTokens,
                    TotalOutputTokens = summary.OutputTokens,
                    AverageResponseTimeMs = summary.AverageResponseTime,
                    ModelUsage = summary.RequestsByModel.ToDictionary(
                        m => m.Key, 
                        m => new ConfigDTOs.ModelUsage 
                        { 
                            RequestCount = m.Value,
                            Cost = summary.CostByModel.ContainsKey(m.Key) ? summary.CostByModel[m.Key] : 0m,
                            InputTokens = 0, // Not available in the summary
                            OutputTokens = 0 // Not available in the summary
                        }
                    )
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting usage statistics for key {VirtualKeyId}", virtualKeyId);
                return new ConfigDTOs.UsageStatisticsDto
                {
                    TotalRequests = 0,
                    TotalCost = 0,
                    TotalInputTokens = 0,
                    TotalOutputTokens = 0,
                    AverageResponseTimeMs = 0,
                    ModelUsage = new Dictionary<string, ConfigDTOs.ModelUsage>()
                };
            }
        }

        async Task<(List<RequestLog> Logs, int TotalCount)> IConfigRequestLogService.GetPagedRequestLogsAsync(
            int virtualKeyId,
            int pageNumber,
            int pageSize)
        {
            try
            {
                var result = await _adminApiClient.GetRequestLogsAsync(
                    pageNumber, pageSize, virtualKeyId, null, null, null);
                    
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
                _logger.LogError(ex, "Error getting paged request logs for key {VirtualKeyId}", virtualKeyId);
                return (new List<RequestLog>(), 0);
            }
        }

        async Task<(List<RequestLog> Logs, int TotalCount)> IConfigRequestLogService.SearchLogsAsync(
            int? virtualKeyId,
            string? modelFilter,
            DateTime startDate,
            DateTime endDate,
            int? statusCode,
            int pageNumber,
            int pageSize)
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

        async Task<ConfigDTOs.LogsSummaryDto> IConfigRequestLogService.GetLogsSummaryAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Calculate days between dates for the Admin API
                int days = (int)Math.Ceiling((endDate - startDate).TotalDays);
                if (days <= 0) days = 1;
                
                var adminResult = await _adminApiClient.GetLogsSummaryAsync(days);
                
                if (adminResult == null)
                {
                    return new ConfigDTOs.LogsSummaryDto
                    {
                        LastRequestDate = null,
                        TotalRequests = 0,
                        InputTokens = 0,
                        OutputTokens = 0,
                        EstimatedCost = 0,
                        AverageResponseTime = 0,
                        SuccessfulRequests = 0,
                        FailedRequests = 0
                    };
                }
                
                // Convert to DTOs.LogsSummaryDto
                var result = new ConfigDTOs.LogsSummaryDto
                {
                    TotalRequests = adminResult.TotalRequests,
                    InputTokens = adminResult.InputTokens,
                    OutputTokens = adminResult.OutputTokens,
                    EstimatedCost = adminResult.EstimatedCost,
                    AverageResponseTime = adminResult.AverageResponseTime,
                    LastRequestDate = adminResult.LastRequestDate,
                    SuccessfulRequests = adminResult.SuccessfulRequests,
                    FailedRequests = adminResult.FailedRequests
                };
                
                // Copy dictionaries
                foreach (var entry in adminResult.RequestsByModel)
                {
                    result.RequestsByModel[entry.Key] = entry.Value;
                }
                
                foreach (var entry in adminResult.CostByModel)
                {
                    result.CostByModel[entry.Key] = entry.Value;
                }
                
                foreach (var entry in adminResult.RequestsByStatus)
                {
                    result.RequestsByStatus[entry.Key] = entry.Value;
                }
                
                // Convert daily stats if available
                if (adminResult.DailyStats != null)
                {
                    foreach (var stat in adminResult.DailyStats)
                    {
                        result.DailyStats.Add(new ConfigDTOs.DailyUsageStatsDto
                        {
                            Date = stat.Date,
                            ModelId = stat.ModelId,
                            RequestCount = stat.RequestCount,
                            InputTokens = stat.InputTokens,
                            OutputTokens = stat.OutputTokens,
                            Cost = stat.Cost
                        });
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting logs summary for date range {StartDate} to {EndDate}", 
                    startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));
                    
                return new ConfigDTOs.LogsSummaryDto
                {
                    LastRequestDate = null,
                    TotalRequests = 0,
                    InputTokens = 0,
                    OutputTokens = 0,
                    EstimatedCost = 0,
                    AverageResponseTime = 0,
                    SuccessfulRequests = 0,
                    FailedRequests = 0
                };
            }
        }

        async Task<List<string>> IConfigRequestLogService.GetDistinctModelsAsync()
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
        
        #endregion
    }
}