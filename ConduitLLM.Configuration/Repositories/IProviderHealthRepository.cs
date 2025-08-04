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
        /// <param name="providerId">The provider ID</param>
        /// <returns>The latest health record, or null if none exists</returns>
        Task<ProviderHealthRecord?> GetLatestStatusAsync(int providerId);

        /// <summary>
        /// Gets status history for a provider within a specified time range
        /// </summary>
        /// <param name="providerId">The provider ID</param>
        /// <param name="since">The start time (UTC) for the history</param>
        /// <param name="limit">Maximum number of records to return</param>
        /// <returns>A list of health records, ordered by timestamp descending</returns>
        Task<List<ProviderHealthRecord>> GetStatusHistoryAsync(int providerId, DateTime since, int limit = 100);

        /// <summary>
        /// Saves a new health status record
        /// </summary>
        /// <param name="status">The health record to save</param>
        /// <returns>An async task</returns>
        Task SaveStatusAsync(ProviderHealthRecord status);

        /// <summary>
        /// Gets the latest health status for all providers
        /// </summary>
        /// <returns>A dictionary mapping provider IDs to their latest health records</returns>
        Task<Dictionary<int, ProviderHealthRecord>> GetAllLatestStatusesAsync();

        /// <summary>
        /// Gets health configuration for a provider
        /// </summary>
        /// <param name="providerId">The provider ID</param>
        /// <returns>The provider health configuration, or null if none exists</returns>
        Task<ProviderHealthConfiguration?> GetConfigurationAsync(int providerId);

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
        /// <returns>A dictionary mapping provider IDs to their uptime percentages (0-100)</returns>
        Task<Dictionary<int, double>> GetProviderUptimeAsync(DateTime since);

        /// <summary>
        /// Gets average response times for providers since the specified time
        /// </summary>
        /// <param name="since">The start time (UTC) for calculating average response times</param>
        /// <returns>A dictionary mapping provider IDs to their average response times in milliseconds</returns>
        Task<Dictionary<int, double>> GetAverageResponseTimesAsync(DateTime since);

        /// <summary>
        /// Gets error counts by provider since the specified time
        /// </summary>
        /// <param name="since">The start time (UTC) for counting errors</param>
        /// <returns>A dictionary mapping provider IDs to their error counts</returns>
        Task<Dictionary<int, int>> GetErrorCountByProviderAsync(DateTime since);

        /// <summary>
        /// Gets error category distribution by provider since the specified time
        /// </summary>
        /// <param name="since">The start time (UTC) for categorizing errors</param>
        /// <returns>A nested dictionary mapping provider IDs to dictionaries of error categories and their counts</returns>
        Task<Dictionary<int, Dictionary<string, int>>> GetErrorCategoriesByProviderAsync(DateTime since);

        /// <summary>
        /// Purges health records older than the specified time
        /// </summary>
        /// <param name="olderThan">The cutoff time (UTC) for purging records</param>
        /// <returns>The number of records purged</returns>
        Task<int> PurgeOldRecordsAsync(DateTime olderThan);

        /// <summary>
        /// Creates a default configuration for a provider if one doesn't exist
        /// </summary>
        /// <param name="providerId">The provider ID</param>
        /// <returns>The new or existing configuration</returns>
        Task<ProviderHealthConfiguration> EnsureConfigurationExistsAsync(int providerId);

        /// <summary>
        /// Updates the LastCheckedUtc timestamp for a provider configuration
        /// </summary>
        /// <param name="providerId">The provider ID</param>
        /// <returns>An async task</returns>
        Task UpdateLastCheckedTimeAsync(int providerId);

        /// <summary>
        /// Gets the count of consecutive failures for a provider
        /// </summary>
        /// <param name="providerId">The provider ID</param>
        /// <param name="since">The start time (UTC) for counting failures</param>
        /// <returns>The count of consecutive failures</returns>
        Task<int> GetConsecutiveFailuresAsync(int providerId, DateTime since);

        /// <summary>
        /// Gets all health records for all providers - efficient bulk operation
        /// </summary>
        /// <param name="since">Optional start time filter</param>
        /// <param name="limit">Optional limit on total records</param>
        /// <returns>All health records matching the criteria</returns>
        Task<List<ProviderHealthRecord>> GetAllRecordsAsync(DateTime? since = null, int? limit = null);
    }
}
