using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Core.Interfaces.Configuration
{
    /// <summary>
    /// Service interface for retrieving provider credentials.
    /// </summary>
    public interface IProviderCredentialService
    {
        /// <summary>
        /// Retrieves credentials for a specific provider by ID.
        /// </summary>
        /// <param name="providerId">The ID of the provider.</param>
        /// <returns>Provider credentials if found, otherwise null.</returns>
        Task<Provider?> GetCredentialByIdAsync(int providerId);
    }
}
