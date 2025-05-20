// This file provides backward compatibility - use ConduitLLM.Configuration.DTOs.VirtualKey.CreateVirtualKeyRequestDto instead

namespace ConduitLLM.WebUI.DTOs;

/// <summary>
/// Type alias for the CreateVirtualKeyRequestDto from ConduitLLM.Configuration.DTOs.VirtualKey
/// This exists to maintain backward compatibility while consolidating duplicate definitions.
/// </summary>
public class CreateVirtualKeyRequestDto : ConduitLLM.Configuration.DTOs.VirtualKey.CreateVirtualKeyRequestDto
{
    // All functionality is inherited from the base class
}
