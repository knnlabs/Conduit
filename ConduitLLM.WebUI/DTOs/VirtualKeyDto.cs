// This file provides backward compatibility - use ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyDto instead

namespace ConduitLLM.WebUI.DTOs;

/// <summary>
/// Type alias for the VirtualKeyDto from ConduitLLM.Configuration.DTOs.VirtualKey
/// This exists to maintain backward compatibility while consolidating duplicate definitions.
/// </summary>
public class VirtualKeyDto : ConduitLLM.Configuration.DTOs.VirtualKey.VirtualKeyDto
{
    // All functionality is inherited from the base class
}
