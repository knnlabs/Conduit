namespace ConduitLLM.Configuration.DTOs;

/// <summary>
/// Represents counts of virtual keys by status for dashboard and metrics purposes.
/// </summary>
public class VirtualKeyCountsDto
{
    /// <summary>
    /// Number of active (enabled and non-expired) virtual keys.
    /// </summary>
    public int Active { get; set; }

    /// <summary>
    /// Number of disabled virtual keys.
    /// </summary>
    public int Disabled { get; set; }

    /// <summary>
    /// Number of expired virtual keys.
    /// </summary>
    public int Expired { get; set; }

    /// <summary>
    /// Total number of virtual keys (Active + Disabled + Expired).
    /// </summary>
    public int Total => Active + Disabled + Expired;
}
