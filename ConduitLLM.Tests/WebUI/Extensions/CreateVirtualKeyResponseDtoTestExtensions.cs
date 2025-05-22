using ConduitLLM.Configuration.DTOs.VirtualKey;

namespace ConduitLLM.Tests.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for CreateVirtualKeyResponseDto for test compatibility
    /// </summary>
    public static class CreateVirtualKeyResponseDtoTestExtensions
    {
        /// <summary>
        /// Gets the key value from the response
        /// </summary>
        /// <param name="dto">The DTO to extend</param>
        /// <returns>The key value</returns>
        public static string Key(this CreateVirtualKeyResponseDto dto)
        {
            return dto.VirtualKey;
        }

        /// <summary>
        /// Gets the virtual key info from the response
        /// </summary>
        /// <param name="dto">The DTO to extend</param>
        /// <returns>The virtual key info</returns>
        public static VirtualKeyDto VirtualKeyInfo(this CreateVirtualKeyResponseDto dto)
        {
            return dto.KeyInfo;
        }
    }
}