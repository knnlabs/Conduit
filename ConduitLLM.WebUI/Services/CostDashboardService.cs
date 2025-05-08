using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.DTOs;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service for retrieving cost dashboard data
    /// </summary>
    public class CostDashboardService : ICostDashboardService
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<CostDashboardService> _logger;

        public CostDashboardService(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<CostDashboardService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<CostDashboardDto> GetDashboardDataAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? virtualKeyId = null,
            string? modelName = null)
        {
            try
            {
                // Set default dates if not specified
                var normalizedDates = NormalizeDateRange(startDate, endDate);
                startDate = normalizedDates.startDate;
                endDate = normalizedDates.endDate;
                
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                
                // Get filtered logs
                var logs = await GetFilteredLogsAsync(dbContext, startDate.Value, endDate.Value, virtualKeyId, modelName);
                
                // Create the dashboard data
                var dashboardData = new CostDashboardDto
                {
                    StartDate = startDate.Value,
                    EndDate = endDate.Value
                };
                
                // Calculate summary metrics
                CalculateSummaryMetrics(logs, dashboardData);
                
                // Generate daily cost trends with date filling
                dashboardData.CostTrends = GenerateDailyCostTrends(logs, startDate.Value, endDate.Value);
                
                // Group by model
                dashboardData.CostByModel = CalculateCostBreakdownByModel(logs);
                
                // Group by virtual key
                dashboardData.CostByVirtualKey = CalculateCostBreakdownByVirtualKey(logs);
                
                return dashboardData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cost dashboard data");
                throw;
            }
        }
        
        /// <summary>
        /// Ensures start and end dates are properly set with defaults if not specified.
        /// </summary>
        /// <param name="startDate">The start date or null.</param>
        /// <param name="endDate">The end date or null.</param>
        /// <returns>A tuple with normalized start and end dates.</returns>
        private (DateTime startDate, DateTime endDate) NormalizeDateRange(DateTime? startDate, DateTime? endDate)
        {
            // Default to last 30 days if not specified
            startDate ??= DateTime.UtcNow.AddDays(-30).Date;
            
            // Default to end of current day if not specified
            endDate ??= DateTime.UtcNow.Date.AddDays(1).AddSeconds(-1);
            
            // Ensure both dates are in UTC format to work with PostgreSQL
            if (startDate.Value.Kind != DateTimeKind.Utc)
            {
                startDate = startDate.Value.ToUniversalTime();
            }
            
            if (endDate.Value.Kind != DateTimeKind.Utc)
            {
                endDate = endDate.Value.ToUniversalTime();
            }
            
            return (startDate.Value, endDate.Value);
        }
        
        /// <summary>
        /// Gets request logs filtered by the specified criteria.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="startDate">The start date.</param>
        /// <param name="endDate">The end date.</param>
        /// <param name="virtualKeyId">Optional virtual key ID to filter by.</param>
        /// <param name="modelName">Optional model name to filter by.</param>
        /// <returns>Filtered request logs.</returns>
        private async Task<List<RequestLog>> GetFilteredLogsAsync(
            ConfigurationDbContext dbContext, 
            DateTime startDate, 
            DateTime endDate, 
            int? virtualKeyId = null,
            string? modelName = null)
        {
            if (dbContext == null)
            {
                _logger.LogError("Database context is null in GetFilteredLogsAsync");
                return new List<RequestLog>();
            }

            if (dbContext.RequestLogs == null)
            {
                _logger.LogError("RequestLogs DbSet is null in database context");
                return new List<RequestLog>();
            }
            
            try
            {
                // Ensure dates are in UTC format for PostgreSQL
                var utcStartDate = startDate.Kind != DateTimeKind.Utc ? startDate.ToUniversalTime() : startDate;
                var utcEndDate = endDate.Kind != DateTimeKind.Utc ? endDate.ToUniversalTime() : endDate;
                
                // Build the query with filters
                var query = dbContext.RequestLogs
                    .AsNoTracking()
                    .Include(r => r.VirtualKey)
                    .Where(r => r.Timestamp >= utcStartDate && r.Timestamp <= utcEndDate);
                
                // Apply optional filters
                if (virtualKeyId.HasValue)
                {
                    query = query.Where(r => r.VirtualKeyId == virtualKeyId.Value);
                }
                
                if (!string.IsNullOrWhiteSpace(modelName))
                {
                    query = query.Where(r => r.ModelName == modelName);
                }
                
                // Execute the query
                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing query in GetFilteredLogsAsync");
                return new List<RequestLog>();
            }
        }
        
        /// <summary>
        /// Calculates summary metrics based on the filtered logs.
        /// </summary>
        /// <param name="logs">The filtered request logs.</param>
        /// <param name="dashboardData">The dashboard data to populate.</param>
        private void CalculateSummaryMetrics(List<RequestLog> logs, CostDashboardDto dashboardData)
        {
            dashboardData.TotalCost = logs.Sum(r => r.Cost);
            dashboardData.TotalRequests = logs.Count;
            dashboardData.TotalInputTokens = logs.Sum(r => r.InputTokens);
            dashboardData.TotalOutputTokens = logs.Sum(r => r.OutputTokens);
        }
        
        /// <summary>
        /// Generates daily cost trends with zero values for missing days.
        /// </summary>
        /// <param name="logs">The filtered request logs.</param>
        /// <param name="startDate">The start date of the period.</param>
        /// <param name="endDate">The end date of the period.</param>
        /// <returns>List of daily cost trends.</returns>
        private List<CostTrendDataDto> GenerateDailyCostTrends(
            List<RequestLog> logs, 
            DateTime startDate, 
            DateTime endDate)
        {
            // Ensure dates are in UTC for consistency
            var utcStartDate = startDate.Kind != DateTimeKind.Utc ? startDate.ToUniversalTime() : startDate;
            var utcEndDate = endDate.Kind != DateTimeKind.Utc ? endDate.ToUniversalTime() : endDate;
            
            // Group logs by date and calculate daily metrics
            var dailyCosts = logs
                .GroupBy(r => r.Timestamp.Date)
                .Select(g => new CostTrendDataDto
                {
                    Date = g.Key,
                    Cost = g.Sum(r => r.Cost),
                    Requests = g.Count()
                })
                .ToDictionary(d => d.Date.Date);
            
            // Create a continuous series with all dates in the range
            var result = new List<CostTrendDataDto>();
            var currentDate = startDate.Date;
            
            while (currentDate <= endDate.Date)
            {
                // Use the existing data point or create a zero value one
                if (dailyCosts.TryGetValue(currentDate, out var costData))
                {
                    result.Add(costData);
                }
                else
                {
                    result.Add(new CostTrendDataDto
                    {
                        Date = currentDate,
                        Cost = 0,
                        Requests = 0
                    });
                }
                
                currentDate = currentDate.AddDays(1);
            }
            
            // Ensure all days are in chronological order
            return result.OrderBy(d => d.Date).ToList();
        }
        
        /// <summary>
        /// Calculates cost breakdown by model.
        /// </summary>
        /// <param name="logs">The filtered request logs.</param>
        /// <returns>List of model cost data.</returns>
        private List<ModelCostDataDto> CalculateCostBreakdownByModel(List<RequestLog> logs)
        {
            return logs
                .GroupBy(r => r.ModelName)
                .OrderByDescending(g => g.Sum(r => r.Cost))
                .Select(g => new ModelCostDataDto
                {
                    Model = g.Key,
                    Requests = g.Count(),
                    Cost = g.Sum(r => r.Cost)
                })
                .ToList();
        }
        
        /// <summary>
        /// Calculates cost breakdown by virtual key.
        /// </summary>
        /// <param name="logs">The filtered request logs.</param>
        /// <returns>List of virtual key cost data.</returns>
        private List<VirtualKeyCostDataDto> CalculateCostBreakdownByVirtualKey(List<RequestLog> logs)
        {
            return logs
                .GroupBy(r => new { r.VirtualKeyId, KeyName = r.VirtualKey?.KeyName ?? $"Key ID {r.VirtualKeyId}" })
                .OrderByDescending(g => g.Sum(r => r.Cost))
                .Select(g => new VirtualKeyCostDataDto
                {
                    KeyId = g.Key.VirtualKeyId,
                    KeyName = g.Key.KeyName,
                    Requests = g.Count(),
                    Cost = g.Sum(r => r.Cost)
                })
                .ToList();
        }
        
        /// <inheritdoc/>
        public async Task<List<VirtualKey>> GetVirtualKeysAsync()
        {
            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                
                return await dbContext.VirtualKeys
                    .AsNoTracking()
                    .OrderBy(k => k.KeyName)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting virtual keys");
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<List<string>> GetAvailableModelsAsync()
        {
            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                if (dbContext == null)
                {
                    _logger.LogError("Database context is null in GetAvailableModelsAsync");
                    return new List<string>();
                }

                if (dbContext.RequestLogs == null)
                {
                    _logger.LogError("RequestLogs DbSet is null in database context");
                    return new List<string>();
                }
                
                return await dbContext.RequestLogs
                    .AsNoTracking()
                    .Select(r => r.ModelName)
                    .Distinct()
                    .OrderBy(m => m)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available models");
                return new List<string>(); // Return empty list instead of throwing
            }
        }
        
        /// <inheritdoc/>
        public async Task<List<DetailedCostDataDto>> GetDetailedCostDataAsync(
            DateTime? startDate,
            DateTime? endDate,
            int? virtualKeyId = null,
            string? modelName = null)
        {
            try
            {
                // Normalize date range
                var normalizedDates = NormalizeDateRange(startDate, endDate);
                startDate = normalizedDates.startDate;
                endDate = normalizedDates.endDate;
                
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                
                // Get filtered logs
                var logs = await GetFilteredLogsAsync(dbContext, startDate.Value, endDate.Value, virtualKeyId, modelName);
                
                // Group by date, model, and virtual key
                return CalculateDetailedCostBreakdown(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detailed cost data");
                throw;
            }
        }
        
        /// <summary>
        /// Calculates detailed cost breakdown grouped by date, model, and virtual key.
        /// </summary>
        /// <param name="logs">The filtered request logs.</param>
        /// <returns>List of detailed cost data.</returns>
        private List<DetailedCostDataDto> CalculateDetailedCostBreakdown(List<RequestLog> logs)
        {
            return logs
                .GroupBy(r => new 
                { 
                    Date = r.Timestamp.Date, 
                    r.ModelName, 
                    r.VirtualKeyId, 
                    KeyName = r.VirtualKey != null ? r.VirtualKey.KeyName : $"Key ID {r.VirtualKeyId}" 
                })
                .OrderBy(g => g.Key.Date)
                .ThenBy(g => g.Key.ModelName)
                .ThenBy(g => g.Key.KeyName)
                .Select(g => new DetailedCostDataDto
                {
                    Date = g.Key.Date,
                    Model = g.Key.ModelName,
                    KeyName = g.Key.KeyName,
                    Requests = g.Count(),
                    InputTokens = g.Sum(r => r.InputTokens),
                    OutputTokens = g.Sum(r => r.OutputTokens),
                    Cost = g.Sum(r => r.Cost)
                })
                .ToList();
        }
    }
}