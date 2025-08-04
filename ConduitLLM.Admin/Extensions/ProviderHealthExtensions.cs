using ConduitLLM.Configuration;
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
