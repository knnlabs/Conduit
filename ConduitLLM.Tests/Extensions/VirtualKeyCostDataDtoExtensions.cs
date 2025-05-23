using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.DTOs;

namespace ConduitLLM.Tests.Extensions
{
    /// <summary>
    /// Extension methods for VirtualKeyCostDataDto
    /// </summary>
    public static class VirtualKeyCostDataDtoExtensions
    {
        /// <summary>
        /// Converts a Configuration.DTOs.VirtualKeyCostDataDto to a WebUI.DTOs.VirtualKeyCostDataDto
        /// </summary>
        public static ConduitLLM.WebUI.DTOs.VirtualKeyCostDataDto ToWebUIVirtualKeyCostDataDto(this ConduitLLM.Configuration.DTOs.VirtualKeyCostDataDto source)
        {
            return new ConduitLLM.WebUI.DTOs.VirtualKeyCostDataDto
            {
                VirtualKeyId = source.VirtualKeyId,
                VirtualKeyName = source.VirtualKeyName,
                Cost = source.TotalCost,
                RequestCount = source.RequestCount,
                InputTokens = 0, // These fields don't exist in the source DTO
                OutputTokens = 0
            };
        }
    }
}