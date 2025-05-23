using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories;

/// <summary>
/// Repository interface for managing IP filters
/// </summary>
public interface IIpFilterRepository
{
    /// <summary>
    /// Gets all IP filters
    /// </summary>
    /// <returns>A collection of IP filters</returns>
    Task<IEnumerable<IpFilterEntity>> GetAllAsync();
    
    /// <summary>
    /// Gets all enabled IP filters
    /// </summary>
    /// <returns>A collection of enabled IP filters</returns>
    Task<IEnumerable<IpFilterEntity>> GetEnabledAsync();
    
    /// <summary>
    /// Gets an IP filter by ID
    /// </summary>
    /// <param name="id">The ID of the filter to get</param>
    /// <returns>The IP filter entity if found, null otherwise</returns>
    Task<IpFilterEntity?> GetByIdAsync(int id);
    
    /// <summary>
    /// Adds a new IP filter
    /// </summary>
    /// <param name="filter">The filter to add</param>
    /// <returns>The added filter with generated ID</returns>
    Task<IpFilterEntity> AddAsync(IpFilterEntity filter);
    
    /// <summary>
    /// Updates an existing IP filter
    /// </summary>
    /// <param name="filter">The filter to update</param>
    /// <returns>True if the filter was updated, false if not found</returns>
    Task<bool> UpdateAsync(IpFilterEntity filter);
    
    /// <summary>
    /// Deletes an IP filter by ID
    /// </summary>
    /// <param name="id">The ID of the filter to delete</param>
    /// <returns>True if the filter was deleted, false if not found</returns>
    Task<bool> DeleteAsync(int id);
}