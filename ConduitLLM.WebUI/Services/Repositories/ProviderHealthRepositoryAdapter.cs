using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using ConfigDTOs = ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.WebUI.Services.Repositories
{
    /// <summary>
    /// Adapter implementation of IProviderHealthRepository that delegates to IProviderHealthService
    /// </summary>
    public class ProviderHealthRepositoryAdapter : IProviderHealthRepository
    {
        private readonly IProviderHealthService _providerHealthService;
        private readonly ILogger<ProviderHealthRepositoryAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderHealthRepositoryAdapter"/> class.
        /// </summary>
        /// <param name="providerHealthService">The provider health service.</param>
        /// <param name="logger">The logger.</param>
        public ProviderHealthRepositoryAdapter(
            IProviderHealthService providerHealthService,
            ILogger<ProviderHealthRepositoryAdapter> logger)
        {
            _providerHealthService = providerHealthService ?? throw new ArgumentNullException(nameof(providerHealthService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<ProviderHealthConfiguration> EnsureConfigurationExistsAsync(string providerName)
        {
            try
            {
                var config = await _providerHealthService.GetConfigurationByNameAsync(providerName);
                if (config == null)
                {
                    // Create default configuration
                    var newConfig = await _providerHealthService.CreateConfigurationAsync(new ConfigDTOs.CreateProviderHealthConfigurationDto
                    {
                        ProviderName = providerName,
                        MonitoringEnabled = true,
                        CheckIntervalMinutes = 5,
                        ConsecutiveFailuresThreshold = 3,
                        NotificationsEnabled = true,
                        TimeoutSeconds = 30
                    });

                    if (newConfig != null)
                    {
                        return MapToEntityConfiguration(newConfig);
                    }
                    
                    // If we couldn't create a configuration through the service, create a default one locally
                    return new ProviderHealthConfiguration
                    {
                        ProviderName = providerName,
                        MonitoringEnabled = true,
                        CheckIntervalMinutes = 5,
                        ConsecutiveFailuresThreshold = 3,
                        NotificationsEnabled = true,
                        TimeoutSeconds = 30,
                        LastCheckedUtc = DateTime.UtcNow
                    };
                }
                
                return MapToEntityConfiguration(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring provider health configuration exists for provider {ProviderName}", providerName);
                
                // Return a default configuration as a fallback
                return new ProviderHealthConfiguration
                {
                    ProviderName = providerName,
                    MonitoringEnabled = true,
                    CheckIntervalMinutes = 5,
                    ConsecutiveFailuresThreshold = 3,
                    NotificationsEnabled = true,
                    TimeoutSeconds = 30,
                    LastCheckedUtc = DateTime.UtcNow
                };
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, double>> GetAverageResponseTimesAsync(DateTime since)
        {
            try
            {
                // Since we don't have direct access to this data through the service,
                // we'll get all health records and calculate it ourselves
                var allRecords = await _providerHealthService.GetHealthRecordsAsync();
                
                return allRecords
                    .Where(r => r.TimestampUtc >= since && r.ResponseTimeMs > 0)
                    .GroupBy(r => r.ProviderName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Average(r => r.ResponseTimeMs)
                    );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting average response times since {Since}", since);
                return new Dictionary<string, double>();
            }
        }

        /// <inheritdoc />
        public async Task<List<ProviderHealthConfiguration>> GetAllConfigurationsAsync()
        {
            try
            {
                var configs = await _providerHealthService.GetAllConfigurationsAsync();
                return configs.Select(MapToEntityConfiguration).Where(c => c != null).ToList()!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all provider health configurations");
                return new List<ProviderHealthConfiguration>();
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, ProviderHealthRecord>> GetAllLatestStatusesAsync()
        {
            try
            {
                var allRecords = await _providerHealthService.GetHealthRecordsAsync();
                
                // Group by provider name and get the most recent record for each
                return allRecords
                    .GroupBy(r => r.ProviderName)
                    .ToDictionary(
                        g => g.Key,
                        g => MapToEntityRecord(g.OrderByDescending(r => r.TimestampUtc).First())!
                    );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all latest provider health statuses");
                return new Dictionary<string, ProviderHealthRecord>();
            }
        }

        /// <inheritdoc />
        public async Task<ProviderHealthConfiguration?> GetConfigurationAsync(string providerName)
        {
            try
            {
                var config = await _providerHealthService.GetConfigurationByNameAsync(providerName);
                return config != null ? MapToEntityConfiguration(config) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider health configuration for provider {ProviderName}", providerName);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<int> GetConsecutiveFailuresAsync(string providerName, DateTime since)
        {
            try
            {
                // Since we don't have direct access to this data through the service,
                // we'll get all health records for the provider and calculate it ourselves
                var records = (await _providerHealthService.GetHealthRecordsAsync(providerName))
                    .Where(r => r.TimestampUtc >= since)
                    .OrderByDescending(r => r.TimestampUtc)
                    .ToList();
                
                int consecutiveFailures = 0;
                foreach (var record in records)
                {
                    if (!record.IsOnline)
                    {
                        consecutiveFailures++;
                    }
                    else
                    {
                        // Break on first successful status
                        break;
                    }
                }
                
                return consecutiveFailures;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting consecutive failures for provider {ProviderName} since {Since}", providerName, since);
                return 0;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, Dictionary<string, int>>> GetErrorCategoriesByProviderAsync(DateTime since)
        {
            try
            {
                // Since we don't have direct access to this data through the service,
                // we'll get all health records and calculate it ourselves
                var allRecords = await _providerHealthService.GetHealthRecordsAsync();
                
                return allRecords
                    .Where(r => r.TimestampUtc >= since && !r.IsOnline && !string.IsNullOrEmpty(r.ErrorCategory))
                    .GroupBy(r => r.ProviderName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.GroupBy(r => r.ErrorCategory ?? "Unknown")
                            .ToDictionary(
                                c => c.Key,
                                c => c.Count()
                            )
                    );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting error categories by provider since {Since}", since);
                return new Dictionary<string, Dictionary<string, int>>();
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, int>> GetErrorCountByProviderAsync(DateTime since)
        {
            try
            {
                // Since we don't have direct access to this data through the service,
                // we'll get all health records and calculate it ourselves
                var allRecords = await _providerHealthService.GetHealthRecordsAsync();
                
                return allRecords
                    .Where(r => r.TimestampUtc >= since && !r.IsOnline)
                    .GroupBy(r => r.ProviderName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Count()
                    );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting error count by provider since {Since}", since);
                return new Dictionary<string, int>();
            }
        }

        /// <inheritdoc />
        public async Task<ProviderHealthRecord?> GetLatestStatusAsync(string providerName)
        {
            try
            {
                var records = await _providerHealthService.GetHealthRecordsAsync(providerName);
                var latestRecord = records.OrderByDescending(r => r.TimestampUtc).FirstOrDefault();
                return latestRecord != null ? MapToEntityRecord(latestRecord) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest provider health status for provider {ProviderName}", providerName);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, double>> GetProviderUptimeAsync(DateTime since)
        {
            try
            {
                // Since we don't have direct access to this data through the service,
                // we'll get all health records and calculate it ourselves
                var allRecords = await _providerHealthService.GetHealthRecordsAsync();
                
                return allRecords
                    .Where(r => r.TimestampUtc >= since)
                    .GroupBy(r => r.ProviderName)
                    .ToDictionary(
                        g => g.Key,
                        g => 
                        {
                            int total = g.Count();
                            int online = g.Count(r => r.IsOnline);
                            return total > 0 ? (double)online / total * 100 : 0;
                        }
                    );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider uptime since {Since}", since);
                return new Dictionary<string, double>();
            }
        }

        /// <inheritdoc />
        public async Task<List<ProviderHealthRecord>> GetStatusHistoryAsync(string providerName, DateTime since, int limit = 100)
        {
            try
            {
                var records = await _providerHealthService.GetHealthRecordsAsync(providerName);
                
                return records
                    .Where(r => r.TimestampUtc >= since)
                    .OrderByDescending(r => r.TimestampUtc)
                    .Take(limit)
                    .Select(MapToEntityRecord)
                    .Where(r => r != null)
                    .ToList()!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider health status history for provider {ProviderName} since {Since}", providerName, since);
                return new List<ProviderHealthRecord>();
            }
        }

        /// <inheritdoc />
        public Task<int> PurgeOldRecordsAsync(DateTime olderThan)
        {
            // Not implemented in Admin API yet
            _logger.LogWarning("PurgeOldRecordsAsync not implemented in Admin API");
            return Task.FromResult(0);
        }

        /// <inheritdoc />
        public async Task SaveConfigurationAsync(ProviderHealthConfiguration config)
        {
            try
            {
                var existingConfig = await _providerHealthService.GetConfigurationByNameAsync(config.ProviderName);
                
                if (existingConfig == null)
                {
                    // Create new configuration
                    await _providerHealthService.CreateConfigurationAsync(new ConfigDTOs.CreateProviderHealthConfigurationDto
                    {
                        ProviderName = config.ProviderName,
                        MonitoringEnabled = config.MonitoringEnabled,
                        CheckIntervalMinutes = config.CheckIntervalMinutes,
                        ConsecutiveFailuresThreshold = config.ConsecutiveFailuresThreshold,
                        NotificationsEnabled = config.NotificationsEnabled,
                        TimeoutSeconds = config.TimeoutSeconds,
                        CustomEndpointUrl = config.CustomEndpointUrl
                    });
                }
                else
                {
                    // Update existing configuration
                    await _providerHealthService.UpdateConfigurationAsync(config.ProviderName, new ConfigDTOs.UpdateProviderHealthConfigurationDto
                    {
                        MonitoringEnabled = config.MonitoringEnabled,
                        CheckIntervalMinutes = config.CheckIntervalMinutes,
                        ConsecutiveFailuresThreshold = config.ConsecutiveFailuresThreshold,
                        NotificationsEnabled = config.NotificationsEnabled,
                        TimeoutSeconds = config.TimeoutSeconds,
                        CustomEndpointUrl = config.CustomEndpointUrl
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving provider health configuration for provider {ProviderName}", config.ProviderName);
            }
        }

        /// <inheritdoc />
        public Task SaveStatusAsync(ProviderHealthRecord status)
        {
            // Not implemented in Admin API yet
            _logger.LogWarning("SaveStatusAsync not implemented in Admin API");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task UpdateLastCheckedTimeAsync(string providerName)
        {
            // Not implemented in Admin API yet
            _logger.LogWarning("UpdateLastCheckedTimeAsync not implemented in Admin API");
            return Task.CompletedTask;
        }

        // Helper methods to map between DTOs and entities
        private ProviderHealthConfiguration MapToEntityConfiguration(ConfigDTOs.ProviderHealthConfigurationDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto), "Provider health configuration DTO cannot be null");
            }
            
            return new ProviderHealthConfiguration
            {
                Id = dto.Id,
                ProviderName = dto.ProviderName,
                MonitoringEnabled = dto.MonitoringEnabled,
                CheckIntervalMinutes = dto.CheckIntervalMinutes,
                ConsecutiveFailuresThreshold = dto.ConsecutiveFailuresThreshold,
                NotificationsEnabled = dto.NotificationsEnabled,
                LastCheckedUtc = dto.LastCheckedUtc,
                TimeoutSeconds = dto.TimeoutSeconds,
                CustomEndpointUrl = dto.CustomEndpointUrl
            };
        }
        
        private ProviderHealthRecord? MapToEntityRecord(ConfigDTOs.ProviderHealthRecordDto dto)
        {
            if (dto == null) return null;
            
            return new ProviderHealthRecord
            {
                Id = dto.Id,
                ProviderName = dto.ProviderName,
                IsOnline = dto.IsOnline,
                Status = dto.Status,
                StatusMessage = dto.StatusMessage,
                ErrorCategory = dto.ErrorCategory,
                ErrorDetails = dto.ErrorDetails,
                ResponseTimeMs = dto.ResponseTimeMs,
                EndpointUrl = dto.EndpointUrl,
                TimestampUtc = dto.TimestampUtc
            };
        }
    }
}