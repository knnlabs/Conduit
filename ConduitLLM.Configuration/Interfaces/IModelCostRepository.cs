using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for managing model costs
    /// </summary>
    public interface IModelCostRepository
    {
        /// <summary>
        /// Gets a model cost by ID
        /// </summary>
        /// <param name="id">The model cost ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The model cost entity or null if not found</returns>
        Task<ModelCost?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

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
        Task<List<ModelCost>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all model costs associated with a specific provider
        /// </summary>
        /// <param name="providerId">The provider ID to filter by</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of model costs for the specified provider</returns>
        Task<List<ModelCost>> GetByProviderAsync(int providerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new model cost
        /// </summary>
        /// <param name="modelCost">The model cost to create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The ID of the created model cost</returns>
        Task<int> CreateAsync(ModelCost modelCost, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a model cost
        /// </summary>
        /// <param name="modelCost">The model cost to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateAsync(ModelCost modelCost, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a model cost
        /// </summary>
        /// <param name="id">The ID of the model cost to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
