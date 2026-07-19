namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for managing model provider mappings in the database.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Model provider mappings are a key part of the Conduit routing system. They define
    /// how model aliases (user-friendly names) map to specific provider models, allowing
    /// for model abstraction and seamless provider switching.
    /// </para>
    /// <para>
    /// Key features of this repository include:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>CRUD operations for model provider mapping entities (inherited from IRepositoryBase)</description></item>
    ///   <item><description>Lookup by model alias to find the appropriate provider and model</description></item>
    ///   <item><description>Filtering by provider to get all mappings for a specific provider</description></item>
    /// </list>
    /// <para>
    /// This interface follows the repository pattern, abstracting the data access layer
    /// and providing a clean, domain-focused API for model mapping management.
    /// </para>
    /// </remarks>
    public interface IModelProviderMappingRepository : IRepositoryBase<Entities.ModelProviderMapping, int>
    {
        /// <summary>
        /// Gets a model provider mapping by model alias
        /// </summary>
        /// <param name="modelName">The model alias</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The model provider mapping entity or null if not found</returns>
        Task<Entities.ModelProviderMapping?> GetByModelNameAsync(string modelName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all ENABLED mappings for a model alias, ordered by ascending Priority then Id
        /// (the provider-level failover candidate order).
        /// </summary>
        /// <param name="modelAlias">The model alias</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Enabled mappings in failover order (may be empty)</returns>
        Task<List<Entities.ModelProviderMapping>> GetAllByModelAliasAsync(string modelAlias, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all model provider mappings for a specific provider
        /// </summary>
        /// <param name="providerType">The provider type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of model provider mappings for the specified provider</returns>
        /// <remarks>This method is obsolete. Use GetByProviderPaginatedAsync instead for better performance.</remarks>
        [Obsolete("Use GetByProviderPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        Task<List<Entities.ModelProviderMapping>> GetByProviderAsync(ProviderType providerType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets model provider mappings for a specific provider with pagination
        /// </summary>
        /// <param name="providerId">The provider ID</param>
        /// <param name="pageNumber">The page number (1-based)</param>
        /// <param name="pageSize">The number of items per page</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A tuple with the list of mappings and the total count</returns>
        Task<(List<Entities.ModelProviderMapping> Items, int TotalCount)> GetByProviderPaginatedAsync(
            int providerId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets model provider mappings for a specific model ID
        /// </summary>
        /// <param name="modelId">The model ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of model provider mappings for the specified model</returns>
        Task<List<Entities.ModelProviderMapping>> GetByModelIdAsync(int modelId, CancellationToken cancellationToken = default);
    }
}
