using ConduitLLM.Configuration.DTOs.VirtualKey;

namespace ConduitLLM.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for CreateVirtualKeyResponseDto
    /// </summary>
    public static class CreateVirtualKeyResponseDtoExtensions
    {
        /// <summary>
        /// Gets the key from the response (backward compatibility)
        /// </summary>
        /// <param name="dto">The response DTO</param>
        /// <returns>The key value</returns>
        public static string? Key(this CreateVirtualKeyResponseDto? dto)
        {
            return dto?.VirtualKey;
        }
        
        /// <summary>
        /// Gets the virtual key info from the response (backward compatibility)
        /// </summary>
        /// <param name="dto">The response DTO</param>
        /// <returns>The virtual key DTO</returns>
        public static VirtualKeyDto? VirtualKeyInfo(this CreateVirtualKeyResponseDto? dto)
        {
            if (dto == null)
            {
                return null;
            }
            
            return dto.KeyInfo;
        }
    }
}