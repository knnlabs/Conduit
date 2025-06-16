using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Admin.Extensions
{
    /// <summary>
    /// Extension methods for working with provider health objects
    /// </summary>
    public static class ProviderHealthExtensions
    {
        /// <summary>
        /// Gets the provider name from an existing configuration
        /// </summary>
        /// <param name="dto">The update DTO</param>
        /// <param name="existingConfig">The existing configuration</param>
        /// <returns>The provider name from the existing configuration</returns>
        public static string GetProviderName(
            this UpdateProviderHealthConfigurationDto dto,
            ProviderHealthConfiguration existingConfig)
        {
            return existingConfig.ProviderName;
        }

        /// <summary>
        /// Updates a ProviderHealthConfiguration entity from an UpdateProviderHealthConfigurationDto
        /// </summary>
        /// <param name="entity">The entity to update</param>
        /// <param name="dto">The DTO with updated values</param>
        /// <returns>The updated entity</returns>
        public static ProviderHealthConfiguration UpdateFrom(
            this ProviderHealthConfiguration entity,
            UpdateProviderHealthConfigurationDto dto)
        {
            entity.MonitoringEnabled = dto.MonitoringEnabled;
            entity.CheckIntervalMinutes = dto.CheckIntervalMinutes;
            entity.TimeoutSeconds = dto.TimeoutSeconds;
            entity.ConsecutiveFailuresThreshold = dto.ConsecutiveFailuresThreshold;
            entity.NotificationsEnabled = dto.NotificationsEnabled;
            entity.CustomEndpointUrl = dto.CustomEndpointUrl;

            return entity;
        }
    }
}
