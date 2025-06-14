using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs.Costs;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Extensions;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Services;

/// <summary>
/// Service for cost dashboard functionality through the Admin API
/// </summary>
public class AdminCostDashboardService : IAdminCostDashboardService
{
    private readonly IRequestLogRepository _requestLogRepository;
    private readonly IVirtualKeyRepository _virtualKeyRepository;
    private readonly ILogger<AdminCostDashboardService> _logger;

    /// <summary>
    /// Initializes a new instance of the AdminCostDashboardService class
    /// </summary>
    /// <param name="requestLogRepository">The request log repository</param>
    /// <param name="virtualKeyRepository">The virtual key repository</param>
    /// <param name="logger">The logger</param>
    public AdminCostDashboardService(
        IRequestLogRepository requestLogRepository,
        IVirtualKeyRepository virtualKeyRepository,
        ILogger<AdminCostDashboardService> logger)
    {
        _requestLogRepository = requestLogRepository ?? throw new ArgumentNullException(nameof(requestLogRepository));
        _virtualKeyRepository = virtualKeyRepository ?? throw new ArgumentNullException(nameof(virtualKeyRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<CostDashboardDto> GetCostSummaryAsync(
        string timeframe = "daily",
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        try
        {
            _logger.LogInformationSecure("Getting cost summary with timeframe: {Timeframe}", timeframe);

            // Normalize timeframe (case-insensitive)
            timeframe = timeframe.ToLower() switch
            {
                "daily" => "daily",
                "weekly" => "weekly",
                "monthly" => "monthly",
                _ => "daily" // Default to daily if invalid
            };

            // Use default dates if not provided
            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

            // Get logs within the date range
            var logs = await _requestLogRepository.GetByDateRangeAsync(startDate.Value, endDate.Value);

            // Calculate daily costs
            var dailyCosts = logs
                .GroupBy(l => l.Timestamp.Date)
                .Select(g => new { Date = g.Key, Cost = g.Sum(l => l.Cost) })
                .OrderBy(d => d.Date)
                .Select(d => (d.Date, d.Cost))
                .ToList();

            // Calculate model costs
            var modelCosts = logs
                .GroupBy(l => l.ModelName)
                .Select(g => new { Model = g.Key, Cost = g.Sum(l => l.Cost), Count = g.Count() })
                .OrderByDescending(m => m.Cost)
                .ToList();

            // Extract provider from model name (assuming format like "openai/gpt-4")
            var providerCosts = logs
                .GroupBy(l => l.ModelName.Contains('/') ? l.ModelName.Split('/')[0] : "unknown")
                .Select(g => new { Provider = g.Key, Cost = g.Sum(l => l.Cost), Count = g.Count() })
                .OrderByDescending(p => p.Cost)
                .ToList();

            // Calculate virtual key costs
            var virtualKeyCosts = logs
                .GroupBy(l => l.VirtualKeyId)
                .Select(g => new { VirtualKeyId = g.Key, Cost = g.Sum(l => l.Cost), Count = g.Count() })
                .OrderByDescending(v => v.Cost)
                .ToList();

            // Calculate last 24 hours cost
            var last24HoursCost = dailyCosts
                .Where(d => d.Date >= DateTime.UtcNow.AddDays(-1))
                .Sum(d => d.Cost);

            // Calculate last 7 days cost
            var last7DaysCost = dailyCosts
                .Where(d => d.Date >= DateTime.UtcNow.AddDays(-7))
                .Sum(d => d.Cost);

            // Calculate last 30 days cost
            var last30DaysCost = dailyCosts
                .Where(d => d.Date >= DateTime.UtcNow.AddDays(-30))
                .Sum(d => d.Cost);

            // Calculate total cost
            var totalCost = dailyCosts.Sum(d => d.Cost);

            // Calculate cost by model
            var topModelsBySpend = modelCosts
                .OrderByDescending(m => m.Cost)
                .Take(5)
                .Select(m => new DetailedCostDataDto
                {
                    Name = m.Model,
                    Cost = m.Cost,
                    Percentage = totalCost > 0
                        ? Math.Round(m.Cost / totalCost * 100, 2)
                        : 0
                })
                .ToList();

            // Calculate cost by provider
            var topProvidersBySpend = providerCosts
                .OrderByDescending(p => p.Cost)
                .Take(5)
                .Select(p => new DetailedCostDataDto
                {
                    Name = p.Provider,
                    Cost = p.Cost,
                    Percentage = totalCost > 0
                        ? Math.Round(p.Cost / totalCost * 100, 2)
                        : 0
                })
                .ToList();

            // Calculate cost by virtual key
            var topVirtualKeysBySpend = new List<DetailedCostDataDto>();

            // Get all virtual keys to get their names
            var virtualKeys = await _virtualKeyRepository.GetAllAsync();

            // Calculate costs by virtual key and join with names
            topVirtualKeysBySpend = virtualKeyCosts
                .OrderByDescending(v => v.Cost)
                .Take(5)
                .Select(v => new DetailedCostDataDto
                {
                    Name = virtualKeys.FirstOrDefault(k => k.Id == v.VirtualKeyId)?.KeyName ?? $"Key ID: {v.VirtualKeyId}",
                    Cost = v.Cost,
                    Percentage = totalCost > 0
                        ? Math.Round(v.Cost / totalCost * 100, 2)
                        : 0
                })
                .ToList();

            // Map to DTO
            var costDashboard = new CostDashboardDto
            {
                TimeFrame = timeframe,
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                Last24HoursCost = last24HoursCost,
                Last7DaysCost = last7DaysCost,
                Last30DaysCost = last30DaysCost,
                TotalCost = totalCost,
                TopModelsBySpend = topModelsBySpend,
                TopProvidersBySpend = topProvidersBySpend,
                TopVirtualKeysBySpend = topVirtualKeysBySpend
            };

            return costDashboard;
        }
        catch (Exception ex)
        {
            _logger.LogErrorSecure(ex, "Error getting cost summary");

            // Return empty summary on error
            return new CostDashboardDto
            {
                TimeFrame = timeframe,
                StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
                EndDate = endDate ?? DateTime.UtcNow,
                Last24HoursCost = 0,
                Last7DaysCost = 0,
                Last30DaysCost = 0,
                TotalCost = 0,
                TopModelsBySpend = new List<DetailedCostDataDto>(),
                TopProvidersBySpend = new List<DetailedCostDataDto>(),
                TopVirtualKeysBySpend = new List<DetailedCostDataDto>()
            };
        }
    }

    /// <inheritdoc/>
    public async Task<CostTrendDto> GetCostTrendsAsync(
        string period = "daily",
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        try
        {
            _logger.LogInformationSecure("Getting cost trends with period: {Period}", period);

            // Normalize period (case-insensitive)
            period = period.ToLower() switch
            {
                "daily" => "daily",
                "weekly" => "weekly",
                "monthly" => "monthly",
                _ => "daily" // Default to daily if invalid
            };

            // Use default dates if not provided
            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

            // Get daily costs using extension method
            var dailyCosts = await _requestLogRepository.GetDailyCostsAsync(startDate.Value, endDate.Value);

            // Aggregate based on period
            var aggregatedCosts = AggregateCosts(dailyCosts, period);

            // Map to DTO
            var costTrend = new CostTrendDto
            {
                Period = period,
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                Data = aggregatedCosts
                    .Select(d => new CostTrendDataDto
                    {
                        Date = d.Date,
                        Cost = d.Cost
                    })
                    .ToList()
            };

            return costTrend;
        }
        catch (Exception ex)
        {
            _logger.LogErrorSecure(ex, "Error getting cost trends");

            // Return empty trends on error
            return new CostTrendDto
            {
                Period = period,
                StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
                EndDate = endDate ?? DateTime.UtcNow,
                Data = new List<CostTrendDataDto>()
            };
        }
    }

    /// <inheritdoc/>
    public async Task<List<ModelCostDataDto>> GetModelCostsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        try
        {
            _logger.LogInformationSecure("Getting model costs");

            // Use default dates if not provided
            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

            // Get logs within the date range
            var logs = await _requestLogRepository.GetByDateRangeAsync(startDate.Value, endDate.Value);

            // Calculate model costs
            var modelCosts = logs
                .GroupBy(l => l.ModelName)
                .Select(g => new
                {
                    Model = g.Key,
                    Cost = g.Sum(l => l.Cost),
                    Count = g.Count()
                })
                .ToList();

            // Calculate model tokens
            var modelTokens = logs
                .GroupBy(l => l.ModelName)
                .Select(g => new
                {
                    Model = g.Key,
                    TotalTokens = g.Sum(l => l.InputTokens + l.OutputTokens)
                })
                .ToList();

            // Join the data
            var result = modelCosts
                .Select(cost => new
                {
                    Model = cost.Model,
                    Cost = cost.Cost,
                    Tokens = modelTokens.FirstOrDefault(t => t.Model == cost.Model)?.TotalTokens ?? 0,
                    Requests = cost.Count
                })
                .OrderByDescending(m => m.Cost)
                .Select(m => new ModelCostDataDto
                {
                    Model = m.Model,
                    Cost = m.Cost,
                    TotalTokens = m.Tokens,
                    RequestCount = m.Requests,
                    CostPerToken = m.Tokens > 0
                        ? Math.Round(m.Cost / m.Tokens * 1000, 4) // Cost per 1K tokens
                        : 0,
                    AverageCostPerRequest = m.Requests > 0
                        ? Math.Round(m.Cost / m.Requests, 4)
                        : 0
                })
                .ToList();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorSecure(ex, "Error getting model costs");
            return new List<ModelCostDataDto>();
        }
    }

    /// <inheritdoc/>
    public async Task<List<VirtualKeyCostDataDto>> GetVirtualKeyCostsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        try
        {
            _logger.LogInformationSecure("Getting virtual key costs");

            // Use default dates if not provided
            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

            // Get logs within the date range
            var logs = await _requestLogRepository.GetByDateRangeAsync(startDate.Value, endDate.Value);

            // Calculate virtual key costs
            var virtualKeyCosts = logs
                .GroupBy(l => l.VirtualKeyId)
                .Select(g => new
                {
                    VirtualKeyId = g.Key,
                    Cost = g.Sum(l => l.Cost),
                    Count = g.Count()
                })
                .ToList();

            // Get virtual keys
            var virtualKeys = await _virtualKeyRepository.GetAllAsync();

            // Join the data to get key names
            var result = virtualKeyCosts
                .Select(cost => new
                {
                    VirtualKeyId = cost.VirtualKeyId,
                    KeyName = virtualKeys.FirstOrDefault(k => k.Id == cost.VirtualKeyId)?.KeyName ?? "Unknown",
                    Cost = cost.Cost,
                    Requests = cost.Count
                })
                .OrderByDescending(v => v.Cost)
                .Select(v => new VirtualKeyCostDataDto
                {
                    VirtualKeyId = v.VirtualKeyId,
                    KeyName = v.KeyName,
                    Cost = v.Cost,
                    RequestCount = v.Requests,
                    AverageCostPerRequest = v.Requests > 0
                        ? Math.Round(v.Cost / v.Requests, 4)
                        : 0
                })
                .ToList();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogErrorSecure(ex, "Error getting virtual key costs");
            return new List<VirtualKeyCostDataDto>();
        }
    }

    private static List<(DateTime Date, decimal Cost)> AggregateCosts(
        IEnumerable<(DateTime Date, decimal Cost)> dailyCosts,
        string period)
    {
        if (dailyCosts == null || !dailyCosts.Any())
        {
            return new List<(DateTime, decimal)>();
        }

        return period.ToLower() switch
        {
            "weekly" => dailyCosts
                .GroupBy(d => GetStartOfWeek(d.Date))
                .Select(g => (g.Key, g.Sum(d => d.Cost)))
                .OrderBy(d => d.Key)
                .ToList(),

            "monthly" => dailyCosts
                .GroupBy(d => new DateTime(d.Date.Year, d.Date.Month, 1))
                .Select(g => (g.Key, g.Sum(d => d.Cost)))
                .OrderBy(d => d.Key)
                .ToList(),

            _ => dailyCosts.OrderBy(d => d.Date).ToList() // Default: return daily costs
        };
    }

    private static DateTime GetStartOfWeek(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-1 * diff).Date;
    }
}
