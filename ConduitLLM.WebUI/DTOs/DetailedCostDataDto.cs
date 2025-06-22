using System;

using ConfigDto = ConduitLLM.Configuration.DTOs.Costs;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Type alias for the DetailedCostDataDto from ConduitLLM.Configuration.DTOs.Costs
    /// This exists to maintain backward compatibility while consolidating duplicate definitions.
    /// </summary>
    public class DetailedCostDataDto : ConfigDto.DetailedCostDataDto
    {
        /// <summary>
        /// Number of requests (WebUI-specific property)
        /// </summary>
        public int RequestCount { get; set; }
    }
}