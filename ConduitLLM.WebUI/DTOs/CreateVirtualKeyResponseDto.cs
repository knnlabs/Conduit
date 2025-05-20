// This file provides backward compatibility - use ConduitLLM.Configuration.DTOs.VirtualKey.CreateVirtualKeyResponseDto instead

namespace ConduitLLM.WebUI.DTOs;

/// <summary>
/// Type alias for the CreateVirtualKeyResponseDto from ConduitLLM.Configuration.DTOs.VirtualKey
/// This exists to maintain backward compatibility while consolidating duplicate definitions.
/// </summary>
public class CreateVirtualKeyResponseDto : ConduitLLM.Configuration.DTOs.VirtualKey.CreateVirtualKeyResponseDto
{
    // All functionality is inherited from the base class
}
