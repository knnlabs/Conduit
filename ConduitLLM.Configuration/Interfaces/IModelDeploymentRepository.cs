using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for managing model deployments
    /// </summary>
    public interface IModelDeploymentRepository
    {
        /// <summary>
        /// Gets a model deployment by ID
        /// </summary>
        /// <param name="id">The model deployment ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The model deployment entity or null if not found</returns>
        Task<ModelDeploymentEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a model deployment by deployment name
        /// </summary>
        /// <param name="deploymentName">The deployment name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The model deployment entity or null if not found</returns>
        Task<ModelDeploymentEntity?> GetByDeploymentNameAsync(string deploymentName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets model deployments by provider
        /// </summary>
        /// <param name="providerType">The provider type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of model deployments for the specified provider</returns>
        Task<List<ModelDeploymentEntity>> GetByProviderAsync(ProviderType providerType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets model deployments by model name
        /// </summary>
        /// <param name="modelName">The model name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of model deployments for the specified model</returns>
        Task<List<ModelDeploymentEntity>> GetByModelNameAsync(string modelName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all model deployments
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A list of all model deployments</returns>
        Task<List<ModelDeploymentEntity>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new model deployment
        /// </summary>
        /// <param name="modelDeployment">The model deployment to create</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The ID of the created model deployment</returns>
        Task<Guid> CreateAsync(ModelDeploymentEntity modelDeployment, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates a model deployment
        /// </summary>
        /// <param name="modelDeployment">The model deployment to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateAsync(ModelDeploymentEntity modelDeployment, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a model deployment
        /// </summary>
        /// <param name="id">The ID of the model deployment to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the deletion was successful, false otherwise</returns>
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
