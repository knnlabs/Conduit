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
    ///   <item><description>CRUD operations for virtual key entities</description></item>
    ///   <item><description>Lookup by ID or key hash for authentication</description></item>
    ///   <item><description>Support for tracking creation and update timestamps</description></item>
    /// </list>
    /// <para>
    /// This interface follows the repository pattern, abstracting the data access layer
    /// and providing a clean, domain-focused API for virtual key management.
    /// </para>
    /// </remarks>
    public interface IVirtualKeyRepository
    {
        /// <summary>
        /// Retrieves a virtual key entity by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the virtual key.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the
        /// virtual key entity if found, or null if no virtual key with the specified ID exists.
        /// </returns>
        /// <remarks>
        /// This method performs a non-tracking query, meaning the entity returned is not
        /// tracked by the Entity Framework change tracker. This is suitable for read-only
        /// scenarios and improves performance.
        /// </remarks>
        Task<VirtualKey?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

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
        /// </remarks>
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
        /// </remarks>
        Task<List<VirtualKey>> GetByVirtualKeyGroupIdAsync(int virtualKeyGroupId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new virtual key entity in the database.
        /// </summary>
        /// <param name="virtualKey">The virtual key entity to create.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// the assigned ID of the newly created virtual key entity.
        /// </returns>
        /// <remarks>
        /// <para>
        /// When creating a new virtual key, the implementation should ensure that:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>The key name is unique within the system</description></item>
        ///   <item><description>The key hash represents a securely hashed value of the actual key</description></item>
        ///   <item><description>Creation and update timestamps are properly set</description></item>
        /// </list>
        /// <para>
        /// The database will assign a unique identifier to the new entity, which is returned by this method.
        /// This ID can be used for subsequent operations on the virtual key.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the virtualKey parameter is null.</exception>
        /// <exception cref="DbUpdateException">May be thrown when a database constraint is violated.</exception>
        Task<int> CreateAsync(VirtualKey virtualKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing virtual key entity in the database.
        /// </summary>
        /// <param name="virtualKey">The virtual key entity with updated values.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result is a boolean value
        /// indicating whether the update was successful (true) or if the entity wasn't found or
        /// wasn't modified (false).
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method updates all properties of the virtual key entity except for any identity
        /// or concurrency tokens. The implementation should automatically update the UpdatedAt
        /// timestamp to reflect when the change occurred.
        /// </para>
        /// <para>
        /// The method should handle concurrency conflicts gracefully, typically by applying a
        /// last-writer-wins strategy or by providing detailed concurrency exception information.
        /// </para>
        /// <para>
        /// Common properties that might be updated include:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>Key name - the display name for the virtual key</description></item>
        ///   <item><description>Expiration date - when the key becomes invalid</description></item>
        ///   <item><description>Token limits - maximum token usage allowed</description></item>
        ///   <item><description>Rate limits - requests per minute/hour/day</description></item>
        ///   <item><description>Status - whether the key is enabled or disabled</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the virtualKey parameter is null.</exception>
        /// <exception cref="DbUpdateConcurrencyException">May be thrown when a concurrency conflict occurs.</exception>
        Task<bool> UpdateAsync(VirtualKey virtualKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a virtual key entity from the database.
        /// </summary>
        /// <param name="id">The unique identifier of the virtual key to delete.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result is a boolean value
        /// indicating whether the deletion was successful (true) or if the entity wasn't found (false).
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method completely removes the virtual key entity from the database. This is a
        /// permanent operation that cannot be undone through the application.
        /// </para>
        /// <para>
        /// The implementation should ensure that any related entities, such as usage history
        /// or request logs that reference this virtual key, are handled appropriately according
        /// to the database's referential integrity rules. This might include:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>Cascading deletes to remove related records</description></item>
        ///   <item><description>Setting null values in foreign key fields of related entities</description></item>
        ///   <item><description>Preventing deletion if related records exist and require the virtual key</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="DbUpdateException">May be thrown when a database constraint prevents deletion.</exception>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

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

    }
}
