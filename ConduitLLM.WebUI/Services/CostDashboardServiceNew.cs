using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.WebUI.DTOs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Repository-based service for retrieving cost dashboard data
    /// </summary>
    public class CostDashboardServiceNew : ICostDashboardService
    {
        private readonly IRequestLogRepository _requestLogRepository;
        private readonly IVirtualKeyRepository _virtualKeyRepository;
        private readonly ILogger<CostDashboardServiceNew> _logger;

        public CostDashboardServiceNew(
            IRequestLogRepository requestLogRepository,
            IVirtualKeyRepository virtualKeyRepository,
            ILogger<CostDashboardServiceNew> logger)
        {
            _requestLogRepository = requestLogRepository;
            _virtualKeyRepository = virtualKeyRepository;
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
                startDate ??= DateTime.UtcNow.AddDays(-30).Date;
                endDate ??= DateTime.UtcNow.Date.AddDays(1).AddSeconds(-1);
                
                // Get logs for the date range
                var logs = await _requestLogRepository.GetByDateRangeAsync(startDate.Value, endDate.Value);
                
                // Apply additional filters
                if (virtualKeyId.HasValue)
                {
                    logs = logs.Where(r => r.VirtualKeyId == virtualKeyId.Value).ToList();
                }
                
                if (!string.IsNullOrWhiteSpace(modelName))
                {
                    logs = logs.Where(r => r.ModelName == modelName).ToList();
                }
                
                // Create the dashboard data
                var dashboardData = new CostDashboardDto
                {
                    StartDate = startDate.Value,
                    EndDate = endDate.Value,
                    TotalCost = logs.Sum(r => r.Cost),
                    TotalRequests = logs.Count,
                    TotalInputTokens = logs.Sum(r => r.InputTokens),
                    TotalOutputTokens = logs.Sum(r => r.OutputTokens)
                };
                
                // Generate cost trends (daily costs)
                var dailyCosts = logs
                    .GroupBy(r => r.Timestamp.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new CostTrendDataDto
                    {
                        Date = g.Key,
                        Cost = g.Sum(r => r.Cost),
                        Requests = g.Count()
                    })
                    .ToList();
                
                // If there are missing days in the date range, add them with zero values
                // for a complete continuous chart
                var currentDate = startDate.Value.Date;
                while (currentDate <= endDate.Value.Date)
                {
                    if (!dailyCosts.Any(d => d.Date.Date == currentDate))
                    {
                        dailyCosts.Add(new CostTrendDataDto
                        {
                            Date = currentDate,
                            Cost = 0,
                            Requests = 0
                        });
                    }
                    
                    currentDate = currentDate.AddDays(1);
                }
                
                // Sort all days in order
                dashboardData.CostTrends = dailyCosts.OrderBy(d => d.Date).ToList();
                
                // Group by model
                dashboardData.CostByModel = logs
                    .GroupBy(r => r.ModelName)
                    .OrderByDescending(g => g.Sum(r => r.Cost))
                    .Select(g => new ModelCostDataDto
                    {
                        Model = g.Key,
                        Requests = g.Count(),
                        Cost = g.Sum(r => r.Cost)
                    })
                    .ToList();
                
                // Prepare lookup dictionary of virtual keys for efficiency
                var virtualKeys = await GetVirtualKeysAsync();
                var keyDict = virtualKeys.ToDictionary(k => k.Id, k => k.KeyName);
                
                // Group by virtual key
                dashboardData.CostByVirtualKey = logs
                    .GroupBy(r => r.VirtualKeyId)
                    .OrderByDescending(g => g.Sum(r => r.Cost))
                    .Select(g => new VirtualKeyCostDataDto
                    {
                        KeyId = g.Key,
                        KeyName = keyDict.ContainsKey(g.Key) ? keyDict[g.Key] : $"Key ID {g.Key}",
                        Requests = g.Count(),
                        Cost = g.Sum(r => r.Cost)
                    })
                    .ToList();
                
                return dashboardData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cost dashboard data");
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<List<VirtualKey>> GetVirtualKeysAsync()
        {
            try
            {
                return await _virtualKeyRepository.GetAllAsync();
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
                // Get all logs and extract distinct model names
                var logs = await _requestLogRepository.GetAllAsync();
                return logs.Select(r => r.ModelName)
                    .Distinct()
                    .OrderBy(m => m)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available models");
                throw;
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
                // Set default dates if not specified
                startDate ??= DateTime.UtcNow.AddDays(-30).Date;
                endDate ??= DateTime.UtcNow.Date.AddDays(1).AddSeconds(-1);
                
                // Get logs for the date range
                var logs = await _requestLogRepository.GetByDateRangeAsync(startDate.Value, endDate.Value);
                
                // Apply additional filters
                if (virtualKeyId.HasValue)
                {
                    logs = logs.Where(r => r.VirtualKeyId == virtualKeyId.Value).ToList();
                }
                
                if (!string.IsNullOrWhiteSpace(modelName))
                {
                    logs = logs.Where(r => r.ModelName == modelName).ToList();
                }
                
                // Prepare lookup dictionary of virtual keys for efficiency
                var virtualKeys = await GetVirtualKeysAsync();
                var keyDict = virtualKeys.ToDictionary(k => k.Id, k => k.KeyName);
                
                // Group by date, model, and virtual key
                var detailedData = logs
                    .GroupBy(r => new 
                    { 
                        Date = r.Timestamp.Date, 
                        r.ModelName, 
                        r.VirtualKeyId
                    })
                    .OrderBy(g => g.Key.Date)
                    .ThenBy(g => g.Key.ModelName)
                    .ThenBy(g => g.Key.VirtualKeyId)
                    .Select(g => new DetailedCostDataDto
                    {
                        Date = g.Key.Date,
                        Model = g.Key.ModelName,
                        KeyName = keyDict.ContainsKey(g.Key.VirtualKeyId) ? keyDict[g.Key.VirtualKeyId] : $"Key ID {g.Key.VirtualKeyId}",
                        Requests = g.Count(),
                        InputTokens = g.Sum(r => r.InputTokens),
                        OutputTokens = g.Sum(r => r.OutputTokens),
                        Cost = g.Sum(r => r.Cost)
                    })
                    .ToList();
                
                return detailedData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detailed cost data");
                throw;
            }
        }
    }
}