// This file provides backward compatibility - use ConduitLLM.Configuration.DTOs.VirtualKey.UpdateVirtualKeyRequestDto instead

namespace ConduitLLM.WebUI.DTOs;

/// <summary>
/// Type alias for the UpdateVirtualKeyRequestDto from ConduitLLM.Configuration.DTOs.VirtualKey
/// This exists to maintain backward compatibility while consolidating duplicate definitions.
/// </summary>
public class UpdateVirtualKeyRequestDto : ConduitLLM.Configuration.DTOs.VirtualKey.UpdateVirtualKeyRequestDto
{
    // All functionality is inherited from the base class
}
