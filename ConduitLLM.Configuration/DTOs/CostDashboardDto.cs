using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for cost dashboard statistics
    /// </summary>
    /// <remarks>
    /// This DTO is being consolidated with ConduitLLM.Configuration.DTOs.Costs.CostDashboardDto.
    /// It provides backward compatibility with code that expects the old structure while supporting
    /// the new features of the Costs namespace version.
    /// 
    /// For new code, use ConduitLLM.Configuration.DTOs.Costs.CostDashboardDto instead.
    /// </remarks>
    [Obsolete("This class is being consolidated with ConduitLLM.Configuration.DTOs.Costs.CostDashboardDto. Use that version for new code.")]
    public class CostDashboardDto
    {
        /// <summary>
        /// Start date of the period
        /// </summary>
        public DateTime StartDate { get; set; }
        
        /// <summary>
        /// End date of the period
        /// </summary>
        public DateTime EndDate { get; set; }
        
        /// <summary>
        /// Total cost across all requests in the period
        /// </summary>
        public decimal TotalCost { get; set; }
        
        /// <summary>
        /// Total number of requests in the period
        /// </summary>
        public int TotalRequests { get; set; }
        
        /// <summary>
        /// Total input tokens across all requests
        /// </summary>
        public int TotalInputTokens { get; set; }
        
        /// <summary>
        /// Total output tokens across all requests
        /// </summary>
        public int TotalOutputTokens { get; set; }
        
        /// <summary>
        /// Cost trend data for daily costs
        /// </summary>
        public List<CostTrendDataDto> CostTrends { get; set; } = new List<CostTrendDataDto>();
        
        /// <summary>
        /// Cost data by model
        /// </summary>
        public List<ModelCostDataDto> CostByModel { get; set; } = new List<ModelCostDataDto>();
        
        /// <summary>
        /// Cost data by virtual key
        /// </summary>
        public List<VirtualKeyCostDataDto> CostByVirtualKey { get; set; } = new List<VirtualKeyCostDataDto>();

        /// <summary>
        /// Creates a legacy CostDashboardDto from a Costs.CostDashboardDto
        /// </summary>
        /// <param name="costsDto">The source Costs DTO</param>
        /// <returns>A new CostDashboardDto with properties populated from the input, or null if the input is null</returns>
        public static CostDashboardDto? FromCostsDto(Costs.CostDashboardDto? costsDto)
        {
            if (costsDto == null)
                return null;

            var result = new CostDashboardDto
            {
                StartDate = costsDto.StartDate,
                EndDate = costsDto.EndDate,
                TotalCost = costsDto.TotalCost,
                // Other properties will need to be mapped manually or left as defaults
            };

            // Convert DetailedCostDataDto to ModelCostDataDto (simplified example)
            foreach (var model in costsDto.TopModelsBySpend)
            {
                result.CostByModel.Add(new ModelCostDataDto
                {
                    ModelName = model.Name,
                    Cost = model.Cost,
                    RequestCount = 0 // Cannot access Count property as it doesn't exist in DetailedCostDataDto
                });
            }

            // Convert DetailedCostDataDto to VirtualKeyCostDataDto (simplified example)
            foreach (var key in costsDto.TopVirtualKeysBySpend)
            {
                result.CostByVirtualKey.Add(new VirtualKeyCostDataDto
                {
                    VirtualKeyId = 0, // Cannot access Id property as it doesn't exist in DetailedCostDataDto
                    KeyName = key.Name,
                    Cost = key.Cost
                });
            }

            return result;
        }

        /// <summary>
        /// Converts this DTO to a Costs.CostDashboardDto
        /// </summary>
        /// <returns>A new Costs.CostDashboardDto with properties populated from this instance</returns>
        public Costs.CostDashboardDto ToCostsDto()
        {
            var result = new Costs.CostDashboardDto
            {
                StartDate = this.StartDate,
                EndDate = this.EndDate,
                TotalCost = this.TotalCost,
                TimeFrame = "custom",
                Last24HoursCost = 0, // Defaults for properties that don't exist in this version
                Last7DaysCost = 0,
                Last30DaysCost = 0
            };

            // Convert ModelCostDataDto to DetailedCostDataDto
            foreach (var model in this.CostByModel)
            {
                result.TopModelsBySpend.Add(new Costs.DetailedCostDataDto
                {
                    Name = model.ModelName,
                    Cost = model.Cost,
                    Percentage = this.TotalCost > 0 ? (model.Cost / this.TotalCost) * 100 : 0
                });
            }

            // Convert VirtualKeyCostDataDto to DetailedCostDataDto
            foreach (var key in this.CostByVirtualKey)
            {
                result.TopVirtualKeysBySpend.Add(new Costs.DetailedCostDataDto
                {
                    Name = key.KeyName,
                    Cost = key.Cost,
                    Percentage = this.TotalCost > 0 ? (key.Cost / this.TotalCost) * 100 : 0
                });
            }

            return result;
        }
    }
}