using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for managing Virtual Keys in the database.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Virtual keys are used to provide authorized access to the LLM API with configurable
    /// permissions, rate limits, and budget constraints. This repository provides methods
    /// for creating, retrieving, updating, and deleting virtual key entities.
    /// </para>
    /// <para>
    /// Key features of the virtual key repository:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>CRUD operations for virtual key entities (inherited from IRepositoryBase)</description></item>
    ///   <item><description>Lookup by ID or key hash for authentication</description></item>
    ///   <item><description>Support for tracking creation and update timestamps</description></item>
    /// </list>
    /// <para>
    /// This interface extends <see cref="IRepositoryBase{TEntity, TKey}"/> for standard CRUD operations
    /// and adds domain-specific methods for virtual key management.
    /// </para>
    /// </remarks>
    public interface IVirtualKeyRepository : IRepositoryBase<VirtualKey, int>
    {
        /// <summary>
        /// Retrieves a virtual key entity by its hashed key value.
        /// </summary>
        /// <param name="keyHash">The hash of the virtual key value.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the
        /// virtual key entity if found, or null if no virtual key with the specified hash exists.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is primarily used during authentication and validation of virtual keys.
        /// For security reasons, the actual key values are never stored in the database, only their
        /// hashed representations.
        /// </para>
        /// <para>
        /// The method performs a non-tracking query, meaning the entity returned is not
        /// tracked by the Entity Framework change tracker. This is suitable for read-only
        /// scenarios and improves performance.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when the keyHash parameter is null or empty.</exception>
        Task<VirtualKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all virtual key entities in the system.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// a list of all virtual key entities, ordered by key name.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method returns all virtual keys sorted alphabetically by their key names.
        /// It is primarily used by administrative interfaces to display and manage all
        /// virtual keys in the system.
        /// </para>
        /// <para>
        /// The method performs a non-tracking query, meaning the entities returned are not
        /// tracked by the Entity Framework change tracker. This is suitable for read-only
        /// scenarios and improves performance, especially when dealing with potentially
        /// large numbers of entities.
        /// </para>
        /// <para>
        /// This method is obsolete. Use GetPaginatedAsync instead for better performance.
        /// </para>
        /// </remarks>
        [Obsolete("Use GetPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        Task<List<VirtualKey>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all virtual key entities belonging to a specific group.
        /// </summary>
        /// <param name="virtualKeyGroupId">The ID of the virtual key group.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// a list of virtual key entities belonging to the specified group.
        /// </returns>
        /// <remarks>
        /// This method is used for filtering virtual keys by their group membership,
        /// which is useful for organizational and reporting purposes.
        /// This method is obsolete. Use GetByVirtualKeyGroupIdPaginatedAsync instead for better performance.
        /// </remarks>
        [Obsolete("Use GetByVirtualKeyGroupIdPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        Task<List<VirtualKey>> GetByVirtualKeyGroupIdAsync(int virtualKeyGroupId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves virtual key entities belonging to a specific group with pagination.
        /// </summary>
        /// <param name="virtualKeyGroupId">The ID of the virtual key group.</param>
        /// <param name="pageNumber">The page number (1-based).</param>
        /// <param name="pageSize">The number of items per page.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// a tuple with the list of virtual keys and the total count.
        /// </returns>
        Task<(List<VirtualKey> Items, int TotalCount)> GetByVirtualKeyGroupIdPaginatedAsync(
            int virtualKeyGroupId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves key names for a set of virtual key IDs.
        /// </summary>
        /// <param name="ids">The virtual key IDs to look up.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// a dictionary mapping virtual key IDs to their names.
        /// </returns>
        /// <remarks>
        /// This method is optimized for bulk lookups when only the name is needed,
        /// avoiding the need to load full entities.
        /// </remarks>
        Task<Dictionary<int, string>> GetKeyNamesByIdsAsync(
            IEnumerable<int> ids,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts active (enabled and non-expired) virtual keys.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// the count of active virtual keys.
        /// </returns>
        Task<int> CountActiveAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a virtual key entity from the database by key hash.
        /// </summary>
        /// <param name="keyHash">The hashed key value of the virtual key to delete.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result is a boolean value
        /// indicating whether the deletion was successful (true) or if the entity wasn't found (false).
        /// </returns>
        /// <remarks>
        /// This method is used for cache invalidation scenarios where we have the key hash
        /// but not the database ID.
        /// </remarks>
        Task<bool> DeleteAsync(string keyHash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a limited number of enabled virtual key entities, ordered by key name.
        /// </summary>
        /// <param name="count">The maximum number of virtual keys to retrieve.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// a list of up to <paramref name="count"/> enabled virtual key entities.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is optimized for scenarios where only a small subset of enabled keys is needed,
        /// such as dashboard displays or metrics collection. Unlike <see cref="GetAllAsync"/>, it applies
        /// filtering and limiting at the database level to avoid loading unnecessary data.
        /// </para>
        /// <para>
        /// The method performs a non-tracking query for optimal read performance.
        /// </para>
        /// </remarks>
        Task<List<VirtualKey>> GetTopEnabledAsync(int count, CancellationToken cancellationToken = default);

    }
}
