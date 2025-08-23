using ConduitLLM.Core.Models.Routing;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for a repository that stores router configuration
    /// </summary>
    public interface IRouterConfigRepository
    {
        /// <summary>
        /// Gets the router configuration
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Router configuration or null if not found</returns>
        Task<RouterConfig?> GetRouterConfigAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves the router configuration
        /// </summary>
        /// <param name="config">Router configuration to save</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SaveRouterConfigAsync(RouterConfig config, CancellationToken cancellationToken = default);
    }
}
