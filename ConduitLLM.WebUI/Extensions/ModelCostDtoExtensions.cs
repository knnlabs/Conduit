using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.WebUI.Extensions;

/// <summary>
/// Extension methods for the ModelCostDto classes
/// </summary>
public static class ModelCostDtoExtensions
{
    /// <summary>
    /// Extension method to provide backward compatibility for Description property on CreateModelCostDto
    /// </summary>
    /// <param name="dto">The DTO to extend</param>
    /// <param name="value">The description value to set</param>
    public static void SetDescription(this CreateModelCostDto dto, string value)
    {
        // This is a no-op since we're not storing the value anywhere
        // It just exists to prevent compilation errors
    }
    
    /// <summary>
    /// Extension method to provide backward compatibility for Description property on UpdateModelCostDto
    /// </summary>
    /// <param name="dto">The DTO to extend</param>
    /// <param name="value">The description value to set</param>
    public static void SetDescription(this UpdateModelCostDto dto, string value)
    {
        // This is a no-op since we're not storing the value anywhere
        // It just exists to prevent compilation errors
    }
}