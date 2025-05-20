using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Admin.Extensions
{
    /// <summary>
    /// Extension methods for the model provider mapping repository
    /// </summary>
    public static class ModelProviderMappingRepositoryExtensions
    {
        /// <summary>
        /// Gets a model provider mapping by model alias
        /// </summary>
        /// <param name="repository">The repository</param>
        /// <param name="modelAlias">The model alias</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The model provider mapping entity or null if not found</returns>
        public static Task<ModelProviderMapping?> GetByModelAliasAsync(
            this IModelProviderMappingRepository repository,
            string modelAlias,
            CancellationToken cancellationToken = default)
        {
            return repository.GetByModelNameAsync(modelAlias, cancellationToken);
        }
        
        /// <summary>
        /// Creates a new model provider mapping
        /// </summary>
        /// <param name="repository">The repository</param>
        /// <param name="mapping">The model provider mapping to create</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The ID of the created model provider mapping</returns>
        public static Task<int> AddAsync(
            this IModelProviderMappingRepository repository,
            ModelProviderMapping mapping,
            CancellationToken cancellationToken = default)
        {
            return repository.CreateAsync(mapping, cancellationToken);
        }
    }
}