using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository interface for managing fallback model mappings
    /// </summary>
    public interface IFallbackModelMappingRepository
    {
        /// <summary>
        /// Gets a fallback model mapping by ID
        /// </summary>
        /// <param name="id">The fallback model mapping ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The fallback model mapping entity or null if not found</returns>
        Task<FallbackModelMappingEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a fallback model mapping by source model name within a fallback configuration
        /// </summary>
        /// <param name="fallbackConfigId">The fallback configuration ID</param>
        /// <param name="sourceModelName">The source model name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The fallback model mapping entity or null if not found</returns>
        Task<FallbackModelMappingEntity?> GetBySourceModelAsync(
            Guid fallbackConfigId,
            string sourceModelName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all fallback model mappings for a fallback configuration
        /// </summary>
        /// <param name="fallbackConfigId">The fallback configuration ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of fallback model mappings</returns>
        Task<List<FallbackModelMappingEntity>> GetByFallbackConfigIdAsync(
            Guid fallbackConfigId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all fallback model mappings
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of all fallback model mappings</returns>
        Task<List<FallbackModelMappingEntity>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new fallback model mapping
        /// </summary>
        /// <param name="fallbackModelMapping">The fallback model mapping to create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The ID of the created fallback model mapping</returns>
        Task<int> CreateAsync(FallbackModelMappingEntity fallbackModelMapping, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a fallback model mapping
        /// </summary>
        /// <param name="fallbackModelMapping">The fallback model mapping to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateAsync(FallbackModelMappingEntity fallbackModelMapping, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a fallback model mapping
        /// </summary>
        /// <param name="id">The ID of the fallback model mapping to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
