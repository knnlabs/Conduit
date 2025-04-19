using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        public async Task<CostDashboardDto> GetCostDashboardDataAsync(
            DateTime startDate,
            DateTime endDate,
            int? virtualKeyId = null,
            string? modelName = null)
        {
            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                
                // Build the query with filters
                var query = dbContext.RequestLogs
                    .AsNoTracking()
                    .Include(r => r.VirtualKey)
                    .Where(r => r.Timestamp >= startDate && r.Timestamp <= endDate);
                
                if (virtualKeyId.HasValue)
                {
                    query = query.Where(r => r.VirtualKeyId == virtualKeyId.Value);
                }
                
                if (!string.IsNullOrWhiteSpace(modelName))
                {
                    query = query.Where(r => r.ModelName == modelName);
                }
                
                // Execute the query
                var logs = await query.ToListAsync();
                
                // Create the dashboard data
                var dashboardData = new CostDashboardDto
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalCost = logs.Sum(r => r.Cost),
                    TotalRequests = (int)logs.LongCount(),
                    TotalInputTokens = logs.Sum(r => r.InputTokens),
                    TotalOutputTokens = logs.Sum(r => r.OutputTokens)
                };
                
                // Group by day
                var dayGroups = logs
                    .GroupBy(r => r.Timestamp.Date)
                    .OrderBy(g => g.Key)
                    .ToList();
                
                foreach (var group in dayGroups)
                {
                    dashboardData.CostByDay[group.Key] = group.Sum(r => r.Cost);
                    dashboardData.RequestsByDay[group.Key] = (int)group.LongCount();
                    dashboardData.TokensByDay[group.Key] = new TokenData
                    {
                        InputTokens = group.Sum(r => r.InputTokens),
                        OutputTokens = group.Sum(r => r.OutputTokens)
                    };
                }
                
                // Group by model
                var modelGroups = logs
                    .GroupBy(r => r.ModelName)
                    .OrderByDescending(g => g.Sum(r => r.Cost))
                    .ToList();
                
                foreach (var group in modelGroups)
                {
                    dashboardData.CostByModel[group.Key] = group.Sum(r => r.Cost);
                }
                
                // Group by key
                var keyGroups = logs
                    .GroupBy(r => r.VirtualKeyId)
                    .OrderByDescending(g => g.Sum(r => r.Cost))
                    .ToList();
                
                foreach (var group in keyGroups)
                {
                    var key = group.FirstOrDefault()?.VirtualKey;
                    dashboardData.CostByKey[group.Key] = new KeyCostData
                    {
                        KeyName = key?.KeyName ?? $"Key ID {group.Key}",
                        TotalCost = group.Sum(r => r.Cost),
                        RequestCount = (int)group.LongCount(),
                        InputTokens = group.Sum(r => r.InputTokens),
                        OutputTokens = group.Sum(r => r.OutputTokens),
                        MaxBudget = key?.MaxBudget
                    };
                }
                
                return dashboardData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cost dashboard data");
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<CostTrendDto> GetCostTrendAsync(
            string period,
            int count,
            int? virtualKeyId = null,
            string? modelName = null)
        {
            try
            {
                DateTime endDate = DateTime.UtcNow;
                DateTime startDate;
                
                // Calculate start date based on period and count
                switch (period.ToLowerInvariant())
                {
                    case "day":
                        startDate = endDate.AddDays(-count + 1).Date;
                        break;
                    case "week":
                        // Start from the beginning of the week count weeks ago
                        startDate = endDate.AddDays(-(int)endDate.DayOfWeek).Date.AddDays(-7 * (count - 1));
                        break;
                    case "month":
                        // Start from the beginning of the month count months ago
                        startDate = new DateTime(endDate.Year, endDate.Month, 1).AddMonths(-count + 1);
                        break;
                    default:
                        throw new ArgumentException($"Invalid period type: {period}");
                }
                
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                
                // Build the query with filters
                var query = dbContext.RequestLogs
                    .AsNoTracking()
                    .Include(r => r.VirtualKey)
                    .Where(r => r.Timestamp >= startDate && r.Timestamp <= endDate);
                
                if (virtualKeyId.HasValue)
                {
                    query = query.Where(r => r.VirtualKeyId == virtualKeyId.Value);
                }
                
                if (!string.IsNullOrWhiteSpace(modelName))
                {
                    query = query.Where(r => r.ModelName == modelName);
                }
                
                // Execute the query
                var logs = await query.ToListAsync();
                
                // Create the trend data
                var trendData = new CostTrendDto
                {
                    PeriodType = period,
                    PeriodCount = count,
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalCost = logs.Sum(r => r.Cost),
                    TotalRequests = (int)logs.LongCount(),
                    VirtualKeyId = virtualKeyId,
                    ModelName = modelName
                };
                
                // Generate periods
                var periods = new List<(DateTime Start, DateTime End, string Label)>();
                
                switch (period.ToLowerInvariant())
                {
                    case "day":
                        for (int i = 0; i < count; i++)
                        {
                            var day = startDate.AddDays(i);
                            periods.Add((
                                day,
                                day.AddDays(1).AddSeconds(-1),
                                day.ToString("MMM dd")
                            ));
                        }
                        break;
                    case "week":
                        for (int i = 0; i < count; i++)
                        {
                            var weekStart = startDate.AddDays(i * 7);
                            var weekEnd = weekStart.AddDays(6);
                            periods.Add((
                                weekStart,
                                weekEnd.AddDays(1).AddSeconds(-1),
                                $"{weekStart:MMM dd} - {weekEnd:MMM dd}"
                            ));
                        }
                        break;
                    case "month":
                        for (int i = 0; i < count; i++)
                        {
                            var monthStart = startDate.AddMonths(i);
                            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                            periods.Add((
                                monthStart,
                                monthEnd.AddDays(1).AddSeconds(-1),
                                monthStart.ToString("MMM yyyy")
                            ));
                        }
                        break;
                }
                
                // Group logs by period
                foreach (var (periodStart, periodEnd, label) in periods)
                {
                    var periodLogs = logs
                        .Where(r => r.Timestamp >= periodStart && r.Timestamp <= periodEnd)
                        .ToList();
                    
                    var periodData = new PeriodCostData
                    {
                        Label = label,
                        StartDate = periodStart,
                        EndDate = periodEnd,
                        Cost = periodLogs.Sum(r => r.Cost),
                        RequestCount = (int)periodLogs.LongCount(),
                        InputTokens = periodLogs.Sum(r => r.InputTokens),
                        OutputTokens = periodLogs.Sum(r => r.OutputTokens)
                    };
                    
                    // Group by model for this period
                    var modelGroups = periodLogs
                        .GroupBy(r => r.ModelName)
                        .OrderByDescending(g => g.Sum(r => r.Cost))
                        .ToList();
                    
                    foreach (var group in modelGroups)
                    {
                        periodData.CostByModel[group.Key] = group.Sum(r => r.Cost);
                    }
                    
                    // Group by key for this period
                    var keyGroups = periodLogs
                        .GroupBy(r => r.VirtualKeyId)
                        .OrderByDescending(g => g.Sum(r => r.Cost))
                        .ToList();
                    
                    foreach (var group in keyGroups)
                    {
                        var key = group.FirstOrDefault()?.VirtualKey;
                        periodData.CostByKey[group.Key] = new KeyPeriodData
                        {
                            KeyName = key?.KeyName ?? $"Key ID {group.Key}",
                            Cost = group.Sum(r => r.Cost)
                        };
                    }
                    
                    trendData.Periods.Add(periodData);
                }
                
                return trendData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cost trend data");
                throw;
            }
        }
    }
}
