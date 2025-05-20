using ConduitLLM.Configuration.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.Admin.Interfaces
{
    /// <summary>
    /// Service interface for managing provider health monitoring through the Admin API
    /// </summary>
    public interface IAdminProviderHealthService
    {
        /// <summary>
        /// Gets all provider health configurations
        /// </summary>
        /// <returns>List of provider health configurations</returns>
        Task<IEnumerable<ProviderHealthConfigurationDto>> GetAllConfigurationsAsync();

        /// <summary>
        /// Gets a provider health configuration by provider name
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <returns>The provider health configuration, or null if not found</returns>
        Task<ProviderHealthConfigurationDto?> GetConfigurationByProviderNameAsync(string providerName);

        /// <summary>
        /// Creates a new provider health configuration
        /// </summary>
        /// <param name="config">The configuration to create</param>
        /// <returns>The created configuration</returns>
        Task<ProviderHealthConfigurationDto> CreateConfigurationAsync(CreateProviderHealthConfigurationDto config);

        /// <summary>
        /// Updates a provider health configuration
        /// </summary>
        /// <param name="config">The configuration to update</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateConfigurationAsync(UpdateProviderHealthConfigurationDto config);

        /// <summary>
        /// Gets the latest health status for all providers
        /// </summary>
        /// <returns>Dictionary mapping provider names to their latest health status</returns>
        Task<Dictionary<string, ProviderHealthRecordDto>> GetAllLatestStatusesAsync();

        /// <summary>
        /// Gets the latest health status for a specific provider
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <returns>The latest health status, or null if not found</returns>
        Task<ProviderHealthRecordDto?> GetLatestStatusAsync(string providerName);

        /// <summary>
        /// Gets health status history for a provider
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <param name="hours">Number of hours to look back</param>
        /// <param name="limit">Maximum number of records to return</param>
        /// <returns>List of health status records</returns>
        Task<IEnumerable<ProviderHealthRecordDto>> GetStatusHistoryAsync(string providerName, int hours = 24, int limit = 100);

        /// <summary>
        /// Gets health summary for all providers
        /// </summary>
        /// <param name="hours">Number of hours to include in the summary</param>
        /// <returns>List of provider health summaries</returns>
        Task<IEnumerable<ProviderHealthSummaryDto>> GetHealthSummaryAsync(int hours = 24);

        /// <summary>
        /// Gets health statistics across all providers
        /// </summary>
        /// <param name="hours">Number of hours to include in the statistics</param>
        /// <returns>Provider health statistics</returns>
        Task<ProviderHealthStatisticsDto> GetHealthStatisticsAsync(int hours = 24);

        /// <summary>
        /// Triggers an immediate health check for a provider
        /// </summary>
        /// <param name="providerName">The name of the provider to check</param>
        /// <returns>The health check result</returns>
        Task<ProviderHealthRecordDto> TriggerHealthCheckAsync(string providerName);

        /// <summary>
        /// Purges health records older than the specified time
        /// </summary>
        /// <param name="days">Number of days to keep records for</param>
        /// <returns>Number of records purged</returns>
        Task<int> PurgeOldRecordsAsync(int days = 30);
    }
}