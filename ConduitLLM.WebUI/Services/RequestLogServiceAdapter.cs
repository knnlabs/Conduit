using System.Diagnostics;
using ConduitLLM.Configuration.Entities;
using WebUIDTOs = ConduitLLM.WebUI.DTOs;
using ConfigDTOs = ConduitLLM.Configuration.DTOs;
using ConfigServiceDTOs = ConduitLLM.Configuration.Services.Dtos;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Options;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Adapter service for request logs that can use either direct repository access or the Admin API
/// </summary>
public class RequestLogServiceAdapter : IRequestLogService
{
    private readonly RequestLogService _repositoryService;
    private readonly IAdminApiClient _adminApiClient;
    private readonly AdminApiOptions _adminApiOptions;
    private readonly ILogger<RequestLogServiceAdapter> _logger;
    
    /// <summary>
    /// Initializes a new instance of the RequestLogServiceAdapter class
    /// </summary>
    /// <param name="repositoryService">The repository-based request log service</param>
    /// <param name="adminApiClient">The Admin API client</param>
    /// <param name="adminApiOptions">The Admin API options</param>
    /// <param name="logger">The logger</param>
    public RequestLogServiceAdapter(
        RequestLogService repositoryService,
        IAdminApiClient adminApiClient,
        IOptions<AdminApiOptions> adminApiOptions,
        ILogger<RequestLogServiceAdapter> logger)
    {
        _repositoryService = repositoryService ?? throw new ArgumentNullException(nameof(repositoryService));
        _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
        _adminApiOptions = adminApiOptions?.Value ?? throw new ArgumentNullException(nameof(adminApiOptions));
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
        // Always use repository service for creating logs as it's a high-frequency operation
        // and requires direct access to update virtual key spending
        return await _repositoryService.CreateRequestLogAsync(
            virtualKeyId,
            modelName,
            requestType,
            inputTokens,
            outputTokens,
            cost,
            responseTimeMs,
            userId,
            clientIp,
            requestPath,
            statusCode,
            cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<(List<RequestLog> Logs, int TotalCount)> GetRequestLogsForKeyAsync(
        int virtualKeyId, 
        int page = 1, 
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        // Use repository service for key-specific logs as the Admin API doesn't have
        // a dedicated endpoint for this specific query pattern
        return await _repositoryService.GetRequestLogsForKeyAsync(
            virtualKeyId,
            page,
            pageSize,
            cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<WebUIDTOs.KeyUsageSummary?> GetKeyUsageSummaryAsync(
        int virtualKeyId,
        CancellationToken cancellationToken = default)
    {
        // Use repository service for key usage summary as the Admin API doesn't have
        // a dedicated endpoint for this specific query pattern
        return await _repositoryService.GetKeyUsageSummaryAsync(virtualKeyId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<WebUIDTOs.KeyAggregateSummary>?> GetAllKeysUsageSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        // Use repository service for aggregate summaries as the Admin API doesn't have
        // a dedicated endpoint for this specific query pattern
        return await _repositoryService.GetAllKeysUsageSummaryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<WebUIDTOs.DailyUsageSummary>?> GetDailyUsageStatsAsync(
        int? virtualKeyId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        // Use repository service for daily usage stats as the Admin API doesn't have
        // a dedicated endpoint for this specific query pattern
        return await _repositoryService.GetDailyUsageStatsAsync(
            virtualKeyId,
            startDate,
            endDate,
            cancellationToken);
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
        if (_adminApiOptions.Enabled)
        {
            try
            {
                var pagedResult = await _adminApiClient.GetRequestLogsAsync(
                    pageNumber,
                    pageSize,
                    virtualKeyId,
                    modelFilter,
                    startDate,
                    endDate);
                
                // Convert DTOs to entities
                var logs = pagedResult.Items.Select(dto => new RequestLog
                {
                    Id = dto.Id,
                    VirtualKeyId = dto.VirtualKeyId,
                    ModelName = dto.ModelId,
                    RequestType = dto.RequestType,
                    InputTokens = dto.InputTokens,
                    OutputTokens = dto.OutputTokens,
                    Cost = dto.Cost,
                    ResponseTimeMs = dto.ResponseTimeMs,
                    Timestamp = dto.Timestamp,
                    UserId = dto.UserId,
                    ClientIp = dto.ClientIp,
                    RequestPath = dto.RequestPath,
                    StatusCode = dto.StatusCode
                }).ToList();
                
                return (logs, pagedResult.TotalItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching logs through Admin API, falling back to repository");
                return await _repositoryService.SearchLogsAsync(
                    virtualKeyId,
                    modelFilter,
                    startDate,
                    endDate,
                    statusCode,
                    pageNumber,
                    pageSize,
                    cancellationToken);
            }
        }
        
        return await _repositoryService.SearchLogsAsync(
            virtualKeyId,
            modelFilter,
            startDate,
            endDate,
            statusCode,
            pageNumber,
            pageSize,
            cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<WebUIDTOs.LogsSummaryDto> GetLogsSummaryAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                // Calculate days between dates for the Admin API
                int days = (int)Math.Ceiling((endDate - startDate).TotalDays);
                if (days <= 0) days = 1;

                // Call the version that returns LogsSummaryDto with days parameter only
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
                var webUiSummary = new WebUIDTOs.LogsSummaryDto
                {
                    TotalRequests = result.TotalRequests,
                    EstimatedCost = result.EstimatedCost,
                    InputTokens = result.InputTokens,
                    OutputTokens = result.OutputTokens,
                    AverageResponseTime = result.AverageResponseTime,
                    StartDate = startDate,
                    EndDate = endDate,
                    SuccessfulRequests = result.SuccessfulRequests,
                    FailedRequests = result.FailedRequests,
                    LastRequestDate = result.LastRequestDate
                };

                return webUiSummary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting logs summary through Admin API, falling back to repository");
                return await _repositoryService.GetLogsSummaryAsync(startDate, endDate, cancellationToken);
            }
        }

        return await _repositoryService.GetLogsSummaryAsync(startDate, endDate, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<List<string>> GetDistinctModelsAsync(
        CancellationToken cancellationToken = default)
    {
        // Use repository service for distinct models as the Admin API doesn't have
        // a dedicated endpoint for this specific query
        return await _repositoryService.GetDistinctModelsAsync(cancellationToken);
    }
    
    /// <inheritdoc />
    public Stopwatch StartRequestTimer()
    {
        // Always use repository service for this method as it's a simple utility method
        return _repositoryService.StartRequestTimer();
    }
}