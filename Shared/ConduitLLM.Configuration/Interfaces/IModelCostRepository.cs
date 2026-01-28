using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for managing model costs.
    /// Extends IRepositoryBase for standard CRUD operations.
    /// </summary>
    public interface IModelCostRepository : IRepositoryBase<ModelCost, int>
    {
        /// <summary>
        /// Gets a model cost by cost name
        /// </summary>
        /// <param name="costName">The cost name to search for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The model cost entity or null if not found</returns>
        Task<ModelCost?> GetByCostNameAsync(string costName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all model costs
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of all model costs</returns>
        /// <remarks>This method is obsolete. Use GetPaginatedAsync instead for better performance.</remarks>
        [Obsolete("Use GetPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        Task<List<ModelCost>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all model costs associated with a specific provider
        /// </summary>
        /// <param name="providerId">The provider ID to filter by</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of model costs for the specified provider</returns>
        /// <remarks>This method is obsolete. Use GetByProviderPaginatedAsync instead for better performance.</remarks>
        [Obsolete("Use GetByProviderPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        Task<List<ModelCost>> GetByProviderAsync(int providerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets model costs for a specific provider with pagination
        /// </summary>
        /// <param name="providerId">The provider ID to filter by</param>
        /// <param name="pageNumber">The page number (1-based)</param>
        /// <param name="pageSize">The number of items per page</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A tuple with the list of model costs and the total count</returns>
        Task<(List<ModelCost> Items, int TotalCount)> GetByProviderPaginatedAsync(
            int providerId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);
    }
}
