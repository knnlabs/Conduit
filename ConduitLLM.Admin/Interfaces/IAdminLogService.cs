using System;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Admin.Interfaces;

/// <summary>
/// Service interface for managing logs through the Admin API
/// </summary>
public interface IAdminLogService
{
    /// <summary>
    /// Gets paginated request logs
    /// </summary>
    /// <param name="page">The page number (1-based)</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="startDate">Optional filter by start date</param>
    /// <param name="endDate">Optional filter by end date</param>
    /// <param name="model">Optional filter by model</param>
    /// <param name="virtualKeyId">Optional filter by virtual key ID</param>
    /// <param name="status">Optional filter by status code</param>
    /// <returns>A paged result containing the request logs</returns>
    Task<PagedResult<LogRequestDto>> GetLogsAsync(
        int page = 1,
        int pageSize = 50,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? model = null,
        int? virtualKeyId = null,
        int? status = null);

    /// <summary>
    /// Gets a single log entry by ID
    /// </summary>
    /// <param name="id">The ID of the log to retrieve</param>
    /// <returns>The log entry, or null if not found</returns>
    Task<LogRequestDto?> GetLogByIdAsync(int id);

    /// <summary>
    /// Gets logs summarized by the specified timeframe
    /// </summary>
    /// <param name="timeframe">The timeframe for the summary (daily, weekly, monthly)</param>
    /// <param name="startDate">The start date for the summary</param>
    /// <param name="endDate">The end date for the summary</param>
    /// <returns>The logs summary</returns>
    Task<LogsSummaryDto> GetLogsSummaryAsync(
        string timeframe = "daily",
        DateTime? startDate = null,
        DateTime? endDate = null);

    /// <summary>
    /// Gets a list of distinct model names from request logs
    /// </summary>
    /// <returns>A collection of distinct model names</returns>
    Task<IEnumerable<string>> GetDistinctModelsAsync();
}
