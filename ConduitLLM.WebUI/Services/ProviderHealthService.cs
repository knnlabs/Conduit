using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConfigDTOs = ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using static ConduitLLM.Configuration.Entities.ProviderHealthRecord;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service implementation for managing provider health using direct repository access.
    /// </summary>
    public class ProviderHealthService : IProviderHealthService
    {
        private readonly IProviderHealthRepository _repository;
        private readonly ILogger<ProviderHealthService> _logger;

        /// <summary>
        /// Initializes a new instance of the ProviderHealthService.
        /// </summary>
        /// <param name="repository">The provider health repository.</param>
        /// <param name="logger">The logger.</param>
        public ProviderHealthService(IProviderHealthRepository repository, ILogger<ProviderHealthService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTOs.ProviderHealthConfigurationDto>> GetAllConfigurationsAsync()
        {
            var configurations = await _repository.GetAllConfigurationsAsync();
            return configurations.Select(ToConfigurationDto);
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ProviderHealthConfigurationDto?> GetConfigurationByNameAsync(string providerName)
        {
            var configuration = await _repository.GetConfigurationAsync(providerName);
            return configuration != null ? ToConfigurationDto(configuration) : null;
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ProviderHealthConfigurationDto?> CreateConfigurationAsync(ConfigDTOs.CreateProviderHealthConfigurationDto config)
        {
            var entity = new ProviderHealthConfiguration
            {
                ProviderName = config.ProviderName,
                CheckIntervalMinutes = config.CheckIntervalMinutes,
                ConsecutiveFailuresThreshold = config.ConsecutiveFailuresThreshold,
                TimeoutSeconds = config.TimeoutSeconds,
                MonitoringEnabled = config.MonitoringEnabled,
                CustomEndpointUrl = config.CustomEndpointUrl,
                NotificationsEnabled = config.NotificationsEnabled
            };

            await _repository.SaveConfigurationAsync(entity);
            return ToConfigurationDto(entity);
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ProviderHealthConfigurationDto?> UpdateConfigurationAsync(string providerName, ConfigDTOs.UpdateProviderHealthConfigurationDto config)
        {
            var existing = await _repository.GetConfigurationAsync(providerName);
            if (existing == null)
            {
                _logger.LogWarning($"Provider health configuration for {providerName} not found for update");
                return null;
            }

            existing.CheckIntervalMinutes = config.CheckIntervalMinutes;
            existing.ConsecutiveFailuresThreshold = config.ConsecutiveFailuresThreshold;
            existing.TimeoutSeconds = config.TimeoutSeconds;
            existing.MonitoringEnabled = config.MonitoringEnabled;
            existing.CustomEndpointUrl = config.CustomEndpointUrl;
            existing.NotificationsEnabled = config.NotificationsEnabled;

            await _repository.SaveConfigurationAsync(existing);
            return ToConfigurationDto(existing);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteConfigurationAsync(string providerName)
        {
            var existing = await _repository.GetConfigurationAsync(providerName);
            if (existing == null)
            {
                _logger.LogWarning($"Provider health configuration for {providerName} not found for deletion");
                return false;
            }

            // Since there's no delete method in the repository, we'll mark it as disabled instead
            existing.MonitoringEnabled = false;
            await _repository.SaveConfigurationAsync(existing);
            return true;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTOs.ProviderHealthRecordDto>> GetHealthRecordsAsync(string? providerName = null)
        {
            // Get records for all providers or a specific one
            if (string.IsNullOrEmpty(providerName))
            {
                // Get all providers and their latest statuses
                var latestStatuses = await _repository.GetAllLatestStatusesAsync();
                return latestStatuses.Values.Select(ToRecordDto);
            }
            else
            {
                // Get just for one provider - last 24 hours
                var records = await _repository.GetStatusHistoryAsync(providerName, DateTime.UtcNow.AddDays(-1), 100);
                return records.Select(ToRecordDto);
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTOs.ProviderHealthSummaryDto>> GetHealthSummaryAsync()
        {
            // Get all configurations
            var configurations = await _repository.GetAllConfigurationsAsync();
            
            // Get latest status for all providers
            var latestStatuses = await _repository.GetAllLatestStatusesAsync();
            
            // Build summary info
            var result = new List<ConfigDTOs.ProviderHealthSummaryDto>();
            foreach (var config in configurations)
            {
                latestStatuses.TryGetValue(config.ProviderName, out var latestRecord);
                result.Add(ToSummaryDto((config, latestRecord)));
            }
            
            return result;
        }

        private static ConfigDTOs.ProviderHealthConfigurationDto ToConfigurationDto(ProviderHealthConfiguration entity)
        {
            return new ConfigDTOs.ProviderHealthConfigurationDto
            {
                Id = entity.Id,
                ProviderName = entity.ProviderName,
                CheckIntervalMinutes = entity.CheckIntervalMinutes,
                ConsecutiveFailuresThreshold = entity.ConsecutiveFailuresThreshold,
                TimeoutSeconds = entity.TimeoutSeconds,
                MonitoringEnabled = entity.MonitoringEnabled,
                CustomEndpointUrl = entity.CustomEndpointUrl,
                NotificationsEnabled = entity.NotificationsEnabled,
                LastCheckedUtc = entity.LastCheckedUtc
            };
        }

        private static ConfigDTOs.ProviderHealthRecordDto ToRecordDto(ProviderHealthRecord entity)
        {
            return new ConfigDTOs.ProviderHealthRecordDto
            {
                Id = entity.Id,
                ProviderName = entity.ProviderName,
                Status = entity.Status,
                StatusMessage = entity.StatusMessage,
                TimestampUtc = entity.TimestampUtc,
                ResponseTimeMs = entity.ResponseTimeMs,
                ErrorCategory = entity.ErrorCategory,
                ErrorDetails = entity.ErrorDetails,
                EndpointUrl = entity.EndpointUrl
            };
        }

        private static ConfigDTOs.ProviderHealthSummaryDto ToSummaryDto((ProviderHealthConfiguration config, ProviderHealthRecord? latestRecord) item)
        {
            return new ConfigDTOs.ProviderHealthSummaryDto
            {
                ProviderName = item.config.ProviderName,
                Status = item.latestRecord?.Status ?? StatusType.Unknown,
                StatusMessage = item.latestRecord?.StatusMessage ?? "No recent checks",
                UptimePercentage = 100.0, // TODO: Calculate actual uptime
                AverageResponseTimeMs = item.latestRecord?.ResponseTimeMs ?? 0,
                ErrorCount = 0, // TODO: Calculate from repository
                ErrorCategories = new Dictionary<string, int>(),
                LastCheckedUtc = item.latestRecord?.TimestampUtc,
                MonitoringEnabled = item.config.MonitoringEnabled
            };
        }
    }
}