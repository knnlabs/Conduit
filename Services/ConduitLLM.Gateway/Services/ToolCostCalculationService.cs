using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ConduitLLM.Configuration;
using ConduitLLM.Gateway.Middleware;

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
        /// <returns>Total cost for all tool usage</returns>
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
    /// </summary>
    public class ToolCostCalculationService : IToolCostCalculationService
    {
        private readonly ConduitDbContext _context;
        private readonly ILogger<ToolCostCalculationService> _logger;

        /// <summary>
        /// Initializes a new instance of the ToolCostCalculationService.
        /// </summary>
        /// <param name="context">Database context for accessing tool configurations</param>
        /// <param name="logger">Logger for error reporting</param>
        public ToolCostCalculationService(ConduitDbContext context, ILogger<ToolCostCalculationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<decimal> CalculateToolCostsAsync(ToolUsageData toolUsage, ProviderType providerType)
        {
            if (toolUsage?.Tools == null || !toolUsage.Tools.Any())
                return 0;

            try
            {
                var totalCost = 0m;

                foreach (var toolUsageItem in toolUsage.Tools)
                {
                    var providerTool = await _context.ProviderTools
                        .FirstOrDefaultAsync(pt => 
                            pt.Provider == providerType && 
                            pt.ToolName == toolUsageItem.ToolName && 
                            pt.IsActive);

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
                _logger.LogError(ex, "Failed to calculate tool costs for provider {ProviderType}", providerType);
                return 0;
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
        /// Calculates the usage amount based on the tool's billing unit.
        /// </summary>
        /// <param name="toolUsageItem">The tool usage item</param>
        /// <param name="billingUnit">The billing unit for this tool (requests, hours, etc.)</param>
        /// <returns>The usage amount for cost calculation</returns>
        private static decimal CalculateUsageAmount(ToolUsageItem toolUsageItem, string? billingUnit)
        {
            return billingUnit?.ToLowerInvariant() switch
            {
                "hours" => toolUsageItem.Duration ?? toolUsageItem.Count,
                "requests" => toolUsageItem.Count,
                "minutes" => toolUsageItem.Duration ?? toolUsageItem.Count,
                _ => toolUsageItem.Count // Default to count-based billing
            };
        }
    }
}