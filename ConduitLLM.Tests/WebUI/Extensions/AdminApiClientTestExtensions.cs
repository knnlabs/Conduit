using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Costs;
using ConduitLLM.WebUI.Interfaces;

namespace ConduitLLM.Tests.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for IAdminApiClient for test mocking
    /// </summary>
    public static class AdminApiClientTestExtensions
    {
        /// <summary>
        /// Gets daily usage statistics
        /// </summary>
        /// <param name="client">The admin API client</param>
        /// <param name="startDate">Start date</param>
        /// <param name="endDate">End date</param>
        /// <param name="virtualKeyId">Optional virtual key ID filter</param>
        /// <param name="modelName">Optional model name filter</param>
        /// <returns>List of daily usage statistics</returns>
        public static Task<List<ConduitLLM.Configuration.DTOs.DailyUsageStatsDto>> GetDailyUsageStatsAsync(
            this IAdminApiClient client,
            DateTime startDate,
            DateTime endDate,
            int? virtualKeyId = null,
            string? modelName = null)
        {
            // For test mocking only - real implementation would call the API
            return Task.FromResult(new List<ConduitLLM.Configuration.DTOs.DailyUsageStatsDto>());
        }

        /// <summary>
        /// Gets detailed cost data
        /// </summary>
        /// <param name="client">The admin API client</param>
        /// <param name="startDate">Start date</param>
        /// <param name="endDate">End date</param>
        /// <param name="virtualKeyId">Optional virtual key ID filter</param>
        /// <param name="modelName">Optional model name filter</param>
        /// <returns>List of detailed cost data</returns>
        public static Task<List<ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto>> GetDetailedCostDataAsync(
            this IAdminApiClient client,
            DateTime startDate,
            DateTime endDate,
            int? virtualKeyId = null,
            string? modelName = null)
        {
            // For test mocking only - real implementation would call the API
            return Task.FromResult(new List<ConduitLLM.Configuration.DTOs.Costs.DetailedCostDataDto>());
        }

        /// <summary>
        /// Gets or creates a global setting
        /// </summary>
        /// <param name="client">The admin API client</param>
        /// <param name="key">Setting key</param>
        /// <param name="defaultValue">Default value if not found</param>
        /// <returns>The global setting</returns>
        public static Task<GlobalSettingDto> GetOrCreateGlobalSettingAsync(
            this IAdminApiClient client,
            string key,
            string defaultValue)
        {
            // For test mocking only - real implementation would call the API
            return Task.FromResult(new GlobalSettingDto
            {
                Key = key,
                Value = defaultValue
            });
        }

        /// <summary>
        /// Gets a specific global setting by key
        /// </summary>
        /// <param name="client">The admin API client</param>
        /// <param name="key">Setting key</param>
        /// <returns>The global setting</returns>
        public static Task<GlobalSettingDto> GetGlobalSettingByKeyAsync(
            this IAdminApiClient client,
            string key)
        {
            // For test mocking only - real implementation would call the API
            return Task.FromResult(new GlobalSettingDto
            {
                Key = key,
                Value = "Test Value"
            });
        }

        /// <summary>
        /// Updates or creates a global setting
        /// </summary>
        /// <param name="client">The admin API client</param>
        /// <param name="setting">Setting to update or create</param>
        /// <returns>The updated or created global setting</returns>
        public static Task<GlobalSettingDto> UpsertGlobalSettingAsync(
            this IAdminApiClient client,
            GlobalSettingDto setting)
        {
            // For test mocking only - real implementation would call the API
            return Task.FromResult(setting);
        }
    }
}
