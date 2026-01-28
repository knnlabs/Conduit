using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces;

/// <summary>
/// Repository interface for managing IP filters.
/// Inherits standard CRUD operations from IRepositoryBase.
/// </summary>
public interface IIpFilterRepository : IRepositoryBase<IpFilterEntity, int>
{
    /// <summary>
    /// Gets all IP filters ordered by filter type and IP address.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A collection of all IP filters</returns>
    Task<IEnumerable<IpFilterEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all enabled IP filters ordered by filter type and IP address.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A collection of enabled IP filters</returns>
    Task<IEnumerable<IpFilterEntity>> GetEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new IP filter and returns the created entity.
    /// </summary>
    /// <param name="filter">The filter to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The added filter with generated ID</returns>
    Task<IpFilterEntity> AddAsync(IpFilterEntity filter, CancellationToken cancellationToken = default);
}
