using ConduitLLM.Configuration.DTOs.Costs;
using ConduitLLM.WebUI.DTOs;

namespace ConduitLLM.Tests.Extensions
{
    /// <summary>
    /// Extension methods for VirtualKeyCostDataDto
    /// </summary>
    public static class VirtualKeyCostDataDtoExtensions
    {
        /// <summary>
        /// Converts a Configuration.DTOs.Costs.VirtualKeyCostDataDto to a WebUI.DTOs.VirtualKeyCostDataDto
        /// </summary>
        public static ConduitLLM.WebUI.DTOs.VirtualKeyCostDataDto ToWebUIVirtualKeyCostDataDto(this ConduitLLM.Configuration.DTOs.Costs.VirtualKeyCostDataDto source)
        {
            return new ConduitLLM.WebUI.DTOs.VirtualKeyCostDataDto
            {
                VirtualKeyId = source.VirtualKeyId,
                VirtualKeyName = source.KeyName,
                Cost = source.Cost,
                RequestCount = source.RequestCount,
                InputTokens = 0, // These fields don't exist in the source DTO
                OutputTokens = 0
            };
        }
    }
}