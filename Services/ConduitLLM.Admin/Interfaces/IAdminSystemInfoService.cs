using ConduitLLM.Configuration.DTOs.Monitoring;

namespace ConduitLLM.Admin.Interfaces;

/// <summary>
/// Service interface for retrieving system information through the Admin API
/// </summary>
public interface IAdminSystemInfoService
{
    /// <summary>
    /// Gets system information
    /// </summary>
    /// <returns>System information details</returns>
    Task<SystemInfoDto> GetSystemInfoAsync();

    /// <summary>
    /// Gets system health status
    /// </summary>
    /// <returns>Health status information</returns>
    Task<HealthStatusDto> GetHealthStatusAsync();
}