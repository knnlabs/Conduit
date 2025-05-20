using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.Admin.Extensions
{
    /// <summary>
    /// Extension methods for the provider health repository
    /// </summary>
    public static class ProviderHealthRepositoryExtensions
    {
        /// <summary>
        /// Gets a provider health configuration by ID
        /// </summary>
        /// <param name="repository">The repository</param>
        /// <param name="id">The ID of the configuration to get</param>
        /// <returns>The provider health configuration or null if not found</returns>
        public static async Task<ProviderHealthConfiguration?> GetConfigurationByIdAsync(
            this IProviderHealthRepository repository,
            int id)
        {
            var allConfigs = await repository.GetAllConfigurationsAsync();
            return allConfigs.FirstOrDefault(c => c.Id == id);
        }
    }
}