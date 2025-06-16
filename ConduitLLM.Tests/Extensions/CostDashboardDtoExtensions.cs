using ConduitLLM.Configuration.DTOs.Costs;

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
            // CostTrendDataDto no longer has RequestCount, return 0 for compatibility
            return 0;
        }

        /// <summary>
        /// Gets the model name from a model cost DTO (compatibility method)
        /// </summary>
        public static string Model(this ModelCostDataDto modelCostData)
        {
            return modelCostData.Model;
        }

        /// <summary>
        /// Gets the request count from a model cost DTO (compatibility method)
        /// </summary>
        public static int Requests(this ModelCostDataDto modelCostData)
        {
            return modelCostData.RequestCount;
        }
    }
}
