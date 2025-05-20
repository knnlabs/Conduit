using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Tests.Extensions
{
    /// <summary>
    /// Extension methods for cost dashboard related DTOs
    /// </summary>
    public static class CostDashboardDtoExtensions
    {
        /// <summary>
        /// Gets the request count from a cost trend DTO (compatibility method)
        /// </summary>
        public static int Requests(this CostTrendDataDto costTrendData)
        {
            return costTrendData.RequestCount;
        }
        
        /// <summary>
        /// Gets the model name from a model cost DTO (compatibility method)
        /// </summary>
        public static string Model(this ConduitLLM.Configuration.DTOs.ModelCostDataDto modelCostData)
        {
            return modelCostData.ModelName;
        }
        
        /// <summary>
        /// Gets the request count from a model cost DTO (compatibility method)
        /// </summary>
        public static int Requests(this ConduitLLM.Configuration.DTOs.ModelCostDataDto modelCostData)
        {
            return modelCostData.RequestCount;
        }
    }
}