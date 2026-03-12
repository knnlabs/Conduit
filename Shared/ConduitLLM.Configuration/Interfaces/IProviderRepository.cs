using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces;

/// <summary>
/// Repository interface for managing providers.
/// Inherits standard CRUD operations from IRepositoryBase.
/// </summary>
public interface IProviderRepository : IRepositoryBase<Provider, int>
{
    /// <summary>
    /// Gets a dictionary mapping provider IDs to their names.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A dictionary of provider ID to name mappings</returns>
    /// <remarks>
    /// This method is optimized for lookups when only the name is needed,
    /// avoiding the need to load full entities.
    /// </remarks>
    Task<Dictionary<int, string>> GetProviderNameMapAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts providers with optional filtering by enabled status.
    /// </summary>
    /// <param name="enabledOnly">If true, only counts enabled providers. If false, only counts disabled. If null, counts all.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The count of providers matching the criteria</returns>
    Task<int> CountAsync(bool? enabledOnly, CancellationToken cancellationToken = default);
}
