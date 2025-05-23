using ConfigDTOs = ConduitLLM.Configuration.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Interface for managing and monitoring provider health
    /// </summary>
    public interface IProviderHealthService
    {
        /// <summary>
        /// Gets all provider health configurations.
        /// </summary>
        /// <returns>Collection of provider health configurations.</returns>
        Task<IEnumerable<ConfigDTOs.ProviderHealthConfigurationDto>> GetAllConfigurationsAsync();

        /// <summary>
        /// Gets a provider health configuration by provider name.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>The provider health configuration, or null if not found.</returns>
        Task<ConfigDTOs.ProviderHealthConfigurationDto?> GetConfigurationByNameAsync(string providerName);

        /// <summary>
        /// Creates a new provider health configuration.
        /// </summary>
        /// <param name="config">The configuration to create.</param>
        /// <returns>The created configuration.</returns>
        Task<ConfigDTOs.ProviderHealthConfigurationDto?> CreateConfigurationAsync(ConfigDTOs.CreateProviderHealthConfigurationDto config);

        /// <summary>
        /// Updates a provider health configuration.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <param name="config">The updated configuration.</param>
        /// <returns>The updated configuration, or null if the update failed.</returns>
        Task<ConfigDTOs.ProviderHealthConfigurationDto?> UpdateConfigurationAsync(string providerName, ConfigDTOs.UpdateProviderHealthConfigurationDto config);

        /// <summary>
        /// Deletes a provider health configuration.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>True if the deletion was successful, false otherwise.</returns>
        Task<bool> DeleteConfigurationAsync(string providerName);

        /// <summary>
        /// Gets all provider health records.
        /// </summary>
        /// <param name="providerName">Optional provider name to filter records.</param>
        /// <returns>Collection of provider health records.</returns>
        Task<IEnumerable<ConfigDTOs.ProviderHealthRecordDto>> GetHealthRecordsAsync(string? providerName = null);

        /// <summary>
        /// Gets provider health summary.
        /// </summary>
        /// <returns>Collection of provider health summaries.</returns>
        Task<IEnumerable<ConfigDTOs.ProviderHealthSummaryDto>> GetHealthSummaryAsync();
    }
}