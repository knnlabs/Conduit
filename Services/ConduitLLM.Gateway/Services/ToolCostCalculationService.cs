using System.Text.Json;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Gateway.Middleware;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Service for calculating costs of tool usage across different providers.
    /// </summary>
    public interface IToolCostCalculationService
    {
        /// <summary>
        /// Calculates the total cost for tool usage based on provider configuration.
        /// </summary>
        /// <param name="toolUsage">Tool usage data extracted from provider response</param>
        /// <param name="providerType">The provider type to look up tool costs</param>
        /// <returns>Total cost for all tool usage, or -1 if calculation failed</returns>
        Task<decimal> CalculateToolCostsAsync(ToolUsageData toolUsage, ProviderType providerType);

        /// <summary>
        /// Serializes tool usage data to JSON for storage in BillingAuditEvent.
        /// </summary>
        /// <param name="toolUsage">Tool usage data to serialize</param>
        /// <returns>JSON string representation of tool usage</returns>
        string SerializeToolUsage(ToolUsageData toolUsage);
    }

    /// <summary>
    /// Implementation of tool cost calculation service.
    /// Uses IProviderToolCache for high-performance lookups when available,
    /// falls back to direct database queries otherwise.
    /// </summary>
    public class ToolCostCalculationService : IToolCostCalculationService
    {
        private readonly IDbContextFactory<ConduitDbContext> _contextFactory;
        private readonly IProviderToolCache? _cache;
        private readonly ILogger<ToolCostCalculationService> _logger;

        /// <summary>
        /// Initializes a new instance of the ToolCostCalculationService.
        /// </summary>
        public ToolCostCalculationService(
            IDbContextFactory<ConduitDbContext> contextFactory,
            ILogger<ToolCostCalculationService> logger,
            IProviderToolCache? cache = null)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _cache = cache;
        }

        /// <inheritdoc/>
        public async Task<decimal> CalculateToolCostsAsync(ToolUsageData toolUsage, ProviderType providerType)
        {
            if (toolUsage?.Tools == null || toolUsage.Tools.Count == 0)
                return 0;

            try
            {
                // Batch-load all active tools for this provider (eliminates N+1)
                var providerTools = await GetActiveToolsForProviderAsync(providerType);

                var totalCost = 0m;

                foreach (var toolUsageItem in toolUsage.Tools)
                {
                    var providerTool = providerTools
                        .Find(pt => pt.ToolName == toolUsageItem.ToolName);

                    if (providerTool?.CostPerUnit.HasValue == true)
                    {
                        var usage = CalculateUsageAmount(toolUsageItem, providerTool.BillingUnit);
                        var cost = providerTool.CostPerUnit.Value * usage;

                        totalCost += cost;

                        _logger.LogDebug("Tool cost calculated: {ToolName} = {Usage} {BillingUnit} × ${CostPerUnit} = ${Cost}",
                            toolUsageItem.ToolName, usage, providerTool.BillingUnit, providerTool.CostPerUnit, cost);
                    }
                    else
                    {
                        _logger.LogWarning("No cost configuration found for tool {ToolName} on provider {ProviderType}",
                            toolUsageItem.ToolName, providerType);
                    }
                }

                return totalCost;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate tool costs for provider {ProviderType}. " +
                    "Tool usage will be recorded but cost may be inaccurate.", providerType);
                // Return -1 to signal calculation failure to the caller,
                // distinguishing it from a legitimate zero cost
                return -1;
            }
        }

        /// <inheritdoc/>
        public string SerializeToolUsage(ToolUsageData toolUsage)
        {
            try
            {
                if (toolUsage == null)
                {
                    return "{}";
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    WriteIndented = false
                };

                return JsonSerializer.Serialize(toolUsage, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to serialize tool usage data");
                return "{}";
            }
        }

        /// <summary>
        /// Gets all active tools for a provider, using cache when available.
        /// </summary>
        private async Task<List<ProviderTool>> GetActiveToolsForProviderAsync(ProviderType providerType)
        {
            if (_cache != null)
            {
                return await _cache.GetActiveToolsForProviderAsync(
                    providerType,
                    LoadToolsFromDatabaseAsync);
            }

            return await LoadToolsFromDatabaseAsync(providerType);
        }

        /// <summary>
        /// Loads active tools from the database for a given provider.
        /// Used as the cache fallback function.
        /// </summary>
        private async Task<List<ProviderTool>> LoadToolsFromDatabaseAsync(ProviderType providerType)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.ProviderTools
                .Where(pt => pt.Provider == providerType && pt.IsActive)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Calculates the usage amount based on the tool's billing unit.
        /// Supports DurationSeconds from provider responses with automatic unit conversion.
        /// </summary>
        private static decimal CalculateUsageAmount(ToolUsageItem toolUsageItem, string? billingUnit)
        {
            var unit = billingUnit?.ToLowerInvariant();
            return unit switch
            {
                ProviderToolBillingUnits.Hours => GetDurationInHours(toolUsageItem),
                ProviderToolBillingUnits.Minutes => GetDurationInMinutes(toolUsageItem),
                ProviderToolBillingUnits.Requests => toolUsageItem.Count,
                ProviderToolBillingUnits.Searches => toolUsageItem.Count,
                ProviderToolBillingUnits.Executions => toolUsageItem.Count,
                ProviderToolBillingUnits.Characters => toolUsageItem.Count,
                ProviderToolBillingUnits.Tokens => toolUsageItem.Count,
                null or "" => toolUsageItem.Count,
                _ => toolUsageItem.Count // Validated at save time, but defensive fallback
            };
        }

        /// <summary>
        /// Gets duration in hours, converting from DurationSeconds if available.
        /// Falls back to Duration (already in hours), then to Count.
        /// </summary>
        private static decimal GetDurationInHours(ToolUsageItem item)
        {
            if (item.DurationSeconds.HasValue)
                return item.DurationSeconds.Value / 3600m;
            return item.Duration ?? item.Count;
        }

        /// <summary>
        /// Gets duration in minutes, converting from DurationSeconds if available.
        /// Falls back to Duration (already in minutes), then to Count.
        /// </summary>
        private static decimal GetDurationInMinutes(ToolUsageItem item)
        {
            if (item.DurationSeconds.HasValue)
                return item.DurationSeconds.Value / 60m;
            return item.Duration ?? item.Count;
        }
    }
}
