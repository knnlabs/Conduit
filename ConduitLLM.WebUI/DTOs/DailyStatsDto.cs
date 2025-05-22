using System;
using ConfigDto = ConduitLLM.Configuration.Services.Dtos;

namespace ConduitLLM.WebUI.DTOs
{
    /// <summary>
    /// Type alias for the DailyStatsDto from ConduitLLM.Configuration.Services.Dtos
    /// This exists to maintain backward compatibility while consolidating duplicate definitions.
    /// </summary>
    public class DailyStatsDto : ConfigDto.DailyStatsDto
    {
        /// <summary>
        /// Average response time in milliseconds (compatibility property)
        /// </summary>
        public double AverageResponseTimeMs
        {
            get => AverageResponseTime;
            set => AverageResponseTime = value;
        }
    }
}