using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.WebUI.Models;

namespace ConduitLLM.WebUI.Interfaces;

/// <summary>
/// Service interface for managing IP filters
/// </summary>
public interface IIpFilterService
{
    /// <summary>
    /// Gets all IP filters
    /// </summary>
    /// <returns>Collection of IP filters</returns>
    Task<IEnumerable<IpFilterDto>> GetAllFiltersAsync();

    /// <summary>
    /// Gets all enabled IP filters
    /// </summary>
    /// <returns>Collection of enabled IP filters</returns>
    Task<IEnumerable<IpFilterDto>> GetEnabledFiltersAsync();

    /// <summary>
    /// Gets an IP filter by ID
    /// </summary>
    /// <param name="id">The ID of the filter to get</param>
    /// <returns>The IP filter if found, null otherwise</returns>
    Task<IpFilterDto?> GetFilterByIdAsync(int id);

    /// <summary>
    /// Creates a new IP filter
    /// </summary>
    /// <param name="filter">The filter to create</param>
    /// <returns>Success result with the created filter or error message</returns>
    Task<(bool Success, string? ErrorMessage, IpFilterDto? Filter)> CreateFilterAsync(CreateIpFilterDto filter);

    /// <summary>
    /// Updates an existing IP filter
    /// </summary>
    /// <param name="filter">The filter to update</param>
    /// <returns>Success result with error message if failed</returns>
    Task<(bool Success, string? ErrorMessage)> UpdateFilterAsync(UpdateIpFilterDto filter);

    /// <summary>
    /// Deletes an IP filter
    /// </summary>
    /// <param name="id">The ID of the filter to delete</param>
    /// <returns>Success result with error message if failed</returns>
    Task<(bool Success, string? ErrorMessage)> DeleteFilterAsync(int id);

    /// <summary>
    /// Checks if an IP address is allowed based on the current filters
    /// </summary>
    /// <param name="ipAddress">The IP address to check</param>
    /// <returns>True if the IP address is allowed, false otherwise</returns>
    Task<bool> IsIpAllowedAsync(string ipAddress);

    /// <summary>
    /// Gets the current IP filter settings
    /// </summary>
    /// <returns>The current IP filter settings</returns>
    Task<ConduitLLM.Configuration.DTOs.IpFilter.IpFilterSettingsDto> GetIpFilterSettingsAsync();

    /// <summary>
    /// Updates the IP filter settings
    /// </summary>
    /// <param name="settings">The new settings</param>
    /// <returns>Success result with error message if failed</returns>
    Task<(bool Success, string? ErrorMessage)> UpdateIpFilterSettingsAsync(ConduitLLM.Configuration.DTOs.IpFilter.IpFilterSettingsDto settings);
}
