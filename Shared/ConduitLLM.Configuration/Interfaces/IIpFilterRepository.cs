using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces;

/// <summary>
/// Repository interface for managing IP filters.
/// Inherits standard CRUD operations from IRepositoryBase.
/// </summary>
public interface IIpFilterRepository : IRepositoryBase<IpFilterEntity, int>
{
    /// <summary>
    /// Gets all enabled GLOBAL IP filters (those not scoped to a virtual key), ordered by filter type
    /// and IP address.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A collection of enabled global IP filters</returns>
    Task<IEnumerable<IpFilterEntity>> GetEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all enabled per-virtual-key IP filters (those scoped to a virtual key).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A collection of enabled per-key IP filters</returns>
    Task<IEnumerable<IpFilterEntity>> GetEnabledPerKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all IP filters (enabled or not) scoped to the given virtual key.
    /// </summary>
    /// <param name="virtualKeyId">The virtual key ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The virtual key's IP filters</returns>
    Task<IEnumerable<IpFilterEntity>> GetByVirtualKeyIdAsync(int virtualKeyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new IP filter and returns the created entity.
    /// </summary>
    /// <param name="filter">The filter to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The added filter with generated ID</returns>
    Task<IpFilterEntity> AddAsync(IpFilterEntity filter, CancellationToken cancellationToken = default);
}
