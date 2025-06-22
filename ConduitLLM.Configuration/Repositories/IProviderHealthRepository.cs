using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository interface for provider health monitoring
    /// </summary>
    public interface IProviderHealthRepository
    {
        /// <summary>
        /// Gets the latest health status for a provider
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <returns>The latest health record, or null if none exists</returns>
        Task<ProviderHealthRecord?> GetLatestStatusAsync(string providerName);

        /// <summary>
        /// Gets status history for a provider within a specified time range
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <param name="since">The start time (UTC) for the history</param>
        /// <param name="limit">Maximum number of records to return</param>
        /// <returns>A list of health records, ordered by timestamp descending</returns>
        Task<List<ProviderHealthRecord>> GetStatusHistoryAsync(string providerName, DateTime since, int limit = 100);

        /// <summary>
        /// Saves a new health status record
        /// </summary>
        /// <param name="status">The health record to save</param>
        /// <returns>An async task</returns>
        Task SaveStatusAsync(ProviderHealthRecord status);

        /// <summary>
        /// Gets the latest health status for all providers
        /// </summary>
        /// <returns>A dictionary mapping provider names to their latest health records</returns>
        Task<Dictionary<string, ProviderHealthRecord>> GetAllLatestStatusesAsync();

        /// <summary>
        /// Gets health configuration for a provider
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <returns>The provider health configuration, or null if none exists</returns>
        Task<ProviderHealthConfiguration?> GetConfigurationAsync(string providerName);

        /// <summary>
        /// Saves a provider health configuration
        /// </summary>
        /// <param name="config">The configuration to save</param>
        /// <returns>An async task</returns>
        Task SaveConfigurationAsync(ProviderHealthConfiguration config);

        /// <summary>
        /// Gets all provider health configurations
        /// </summary>
        /// <returns>A list of provider health configurations</returns>
        Task<List<ProviderHealthConfiguration>> GetAllConfigurationsAsync();

        /// <summary>
        /// Gets provider uptime percentages since the specified time
        /// </summary>
        /// <param name="since">The start time (UTC) for calculating uptime</param>
        /// <returns>A dictionary mapping provider names to their uptime percentages (0-100)</returns>
        Task<Dictionary<string, double>> GetProviderUptimeAsync(DateTime since);

        /// <summary>
        /// Gets average response times for providers since the specified time
        /// </summary>
        /// <param name="since">The start time (UTC) for calculating average response times</param>
        /// <returns>A dictionary mapping provider names to their average response times in milliseconds</returns>
        Task<Dictionary<string, double>> GetAverageResponseTimesAsync(DateTime since);

        /// <summary>
        /// Gets error counts by provider since the specified time
        /// </summary>
        /// <param name="since">The start time (UTC) for counting errors</param>
        /// <returns>A dictionary mapping provider names to their error counts</returns>
        Task<Dictionary<string, int>> GetErrorCountByProviderAsync(DateTime since);

        /// <summary>
        /// Gets error category distribution by provider since the specified time
        /// </summary>
        /// <param name="since">The start time (UTC) for categorizing errors</param>
        /// <returns>A nested dictionary mapping provider names to dictionaries of error categories and their counts</returns>
        Task<Dictionary<string, Dictionary<string, int>>> GetErrorCategoriesByProviderAsync(DateTime since);

        /// <summary>
        /// Purges health records older than the specified time
        /// </summary>
        /// <param name="olderThan">The cutoff time (UTC) for purging records</param>
        /// <returns>The number of records purged</returns>
        Task<int> PurgeOldRecordsAsync(DateTime olderThan);

        /// <summary>
        /// Creates a default configuration for a provider if one doesn't exist
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <returns>The new or existing configuration</returns>
        Task<ProviderHealthConfiguration> EnsureConfigurationExistsAsync(string providerName);

        /// <summary>
        /// Updates the LastCheckedUtc timestamp for a provider configuration
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <returns>An async task</returns>
        Task UpdateLastCheckedTimeAsync(string providerName);

        /// <summary>
        /// Gets the count of consecutive failures for a provider
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <param name="since">The start time (UTC) for counting failures</param>
        /// <returns>The count of consecutive failures</returns>
        Task<int> GetConsecutiveFailuresAsync(string providerName, DateTime since);

        /// <summary>
        /// Gets all health records for all providers - efficient bulk operation
        /// </summary>
        /// <param name="since">Optional start time filter</param>
        /// <param name="limit">Optional limit on total records</param>
        /// <returns>All health records matching the criteria</returns>
        Task<List<ProviderHealthRecord>> GetAllRecordsAsync(DateTime? since = null, int? limit = null);
    }
}
