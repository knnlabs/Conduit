using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository interface for audio provider configurations.
    /// </summary>
    public interface IAudioProviderConfigRepository
    {
        /// <summary>
        /// Gets all audio provider configurations.
        /// </summary>
        Task<List<AudioProviderConfig>> GetAllAsync();

        /// <summary>
        /// Gets an audio provider configuration by ID.
        /// </summary>
        Task<AudioProviderConfig?> GetByIdAsync(int id);

        /// <summary>
        /// Gets audio provider configuration by provider credential ID.
        /// </summary>
        Task<AudioProviderConfig?> GetByProviderCredentialIdAsync(int providerCredentialId);

        /// <summary>
        /// Gets audio provider configurations by provider type.
        /// </summary>
        Task<List<AudioProviderConfig>> GetByProviderTypeAsync(ProviderType providerType);

        /// <summary>
        /// Gets enabled audio provider configurations for a specific operation type.
        /// </summary>
        Task<List<AudioProviderConfig>> GetEnabledForOperationAsync(string operationType);

        /// <summary>
        /// Creates a new audio provider configuration.
        /// </summary>
        Task<AudioProviderConfig> CreateAsync(AudioProviderConfig config);

        /// <summary>
        /// Updates an existing audio provider configuration.
        /// </summary>
        Task<AudioProviderConfig> UpdateAsync(AudioProviderConfig config);

        /// <summary>
        /// Deletes an audio provider configuration.
        /// </summary>
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Checks if a provider credential already has audio configuration.
        /// </summary>
        Task<bool> ExistsForProviderCredentialAsync(int providerCredentialId);
    }
}
