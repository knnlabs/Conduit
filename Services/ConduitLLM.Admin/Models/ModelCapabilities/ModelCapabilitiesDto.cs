namespace ConduitLLM.Admin.Models.ModelCapabilities
{
    /// <summary>
    /// Alias for CapabilitiesDto to maintain backward compatibility with ModelController.
    /// </summary>
    /// <remarks>
    /// This class exists for backward compatibility. Use CapabilitiesDto for new code.
    /// Both names refer to the same concept - model capabilities configuration.
    /// </remarks>
    public class ModelCapabilitiesDto : CapabilitiesDto
    {
    }
}