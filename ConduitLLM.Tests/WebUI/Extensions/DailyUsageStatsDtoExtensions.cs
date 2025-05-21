using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.Tests.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for converting between Configuration and WebUI DailyUsageStatsDto
    /// </summary>
    public static class DailyUsageStatsDtoExtensions
    {
        /// <summary>
        /// Converts Configuration DailyUsageStatsDto to WebUI DailyUsageStatsDto
        /// </summary>
        public static Task<IEnumerable<ConduitLLM.WebUI.DTOs.DailyUsageStatsDto>> ToWebUIDailyUsageStatsDtos(
            this Task<IEnumerable<ConduitLLM.Configuration.DTOs.DailyUsageStatsDto>> configDtosTask)
        {
            return configDtosTask.ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    return Enumerable.Empty<ConduitLLM.WebUI.DTOs.DailyUsageStatsDto>();
                }

                var configDtos = task.Result;
                return configDtos.Select(configDto => new ConduitLLM.WebUI.DTOs.DailyUsageStatsDto
                {
                    Date = configDto.Date,
                    ModelName = configDto.ModelName,
                    TotalCost = configDto.TotalCost,
                    RequestCount = configDto.RequestCount,
                    InputTokens = configDto.InputTokens,
                    OutputTokens = configDto.OutputTokens
                });
            });
        }
        
        /// <summary>
        /// Converts Configuration DetailedCostDataDto to WebUI DetailedCostDataDto
        /// </summary>
        public static Task<List<ConduitLLM.WebUI.DTOs.DetailedCostDataDto>> ToWebUIDetailedCostDataDtos(
            this Task<List<ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto>> configDtosTask)
        {
            return configDtosTask.ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    return new List<ConduitLLM.WebUI.DTOs.DetailedCostDataDto>();
                }

                var configDtos = task.Result;
                return configDtos.Select(configDto => new ConduitLLM.WebUI.DTOs.DetailedCostDataDto
                {
                    Name = configDto.Name,
                    Cost = configDto.Cost,
                    Percentage = configDto.Percentage
                }).ToList();
            });
        }
    }
}