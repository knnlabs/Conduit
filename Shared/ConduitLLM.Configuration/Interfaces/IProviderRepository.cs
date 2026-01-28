using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for managing providers
    /// </summary>
    public interface IProviderRepository
    {
        /// <summary>
        /// Gets a provider by ID
        /// </summary>
        /// <param name="id">The provider ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The provider entity or null if not found</returns>
        Task<Provider?> GetByIdAsync(int id, CancellationToken cancellationToken = default);


        /// <summary>
        /// Gets all providers
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of all providers</returns>
        /// <remarks>This method is obsolete. Use GetPaginatedAsync instead for better performance.</remarks>
        [Obsolete("Use GetPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        Task<List<Provider>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets providers with pagination
        /// </summary>
        /// <param name="pageNumber">The page number (1-based)</param>
        /// <param name="pageSize">The number of items per page</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A tuple with the list of providers and the total count</returns>
        Task<(List<Provider> Items, int TotalCount)> GetPaginatedAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a dictionary mapping provider IDs to their names
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A dictionary of provider ID to name mappings</returns>
        /// <remarks>
        /// This method is optimized for lookups when only the name is needed,
        /// avoiding the need to load full entities.
        /// </remarks>
        Task<Dictionary<int, string>> GetProviderNameMapAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts providers with optional filtering
        /// </summary>
        /// <param name="enabledOnly">If true, only counts enabled providers. If false, only counts disabled. If null, counts all.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The count of providers matching the criteria</returns>
        Task<int> CountAsync(bool? enabledOnly = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new provider
        /// </summary>
        /// <param name="provider">The provider to create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The ID of the created provider</returns>
        Task<int> CreateAsync(Provider provider, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a provider
        /// </summary>
        /// <param name="provider">The provider to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateAsync(Provider provider, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a provider
        /// </summary>
        /// <param name="id">The ID of the provider to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
