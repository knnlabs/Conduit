using System.Collections.Generic;
using System.Linq;

namespace ConduitLLM.Tests.Extensions
{
    /// <summary>
    /// Extension methods for LogsSummaryDto to provide backward compatibility
    /// </summary>
    public static class LogsSummaryDtoExtensions
    {
        /// <summary>
        /// Gets the total input tokens (backward compatibility)
        /// </summary>
        public static int TotalInputTokens(this ConfigDTOs.LogsSummaryDto dto)
        {
            return dto.TotalInputTokens;
        }
        
        /// <summary>
        /// Gets the total output tokens (backward compatibility)
        /// </summary>
        public static int TotalOutputTokens(this ConfigDTOs.LogsSummaryDto dto)
        {
            return dto.TotalOutputTokens;
        }
        
        /// <summary>
        /// Gets the total cost (backward compatibility)
        /// </summary>
        public static decimal TotalCost(this ConfigDTOs.LogsSummaryDto dto)
        {
            return dto.TotalCost;
        }
        
        /// <summary>
        /// Gets the average response time (backward compatibility)
        /// </summary>
        public static double AverageResponseTimeMs(this ConfigDTOs.LogsSummaryDto dto)
        {
            return dto.AverageResponseTimeMs;
        }
        
        /// <summary>
        /// Gets requests by model dictionary (backward compatibility)
        /// </summary>
        public static Dictionary<string, int> RequestsByModel(this ConfigDTOs.LogsSummaryDto dto)
        {
            return dto.RequestsByModel ?? new Dictionary<string, int>();
        }
        
        /// <summary>
        /// Gets requests by status (backward compatibility)
        /// </summary>
        public static Dictionary<int, int> RequestsByStatus(this ConfigDTOs.LogsSummaryDto dto)
        {
            return dto.RequestsByStatus ?? new Dictionary<int, int>();
        }
        
        /// <summary>
        /// Gets cost by model (backward compatibility)
        /// </summary>
        public static Dictionary<string, decimal> CostByModel(this ConfigDTOs.LogsSummaryDto dto)
        {
            return dto.CostByModel ?? new Dictionary<string, decimal>();
        }
    }
}