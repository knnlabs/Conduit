using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;

using Microsoft.Extensions.Logging;

using ProviderHealthExt = ConduitLLM.Admin.Extensions.ProviderHealthExtensions;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service implementation for managing provider health monitoring through the Admin API
    /// </summary>
    public class AdminProviderHealthService : IAdminProviderHealthService
    {
        private readonly IProviderHealthRepository _providerHealthRepository;
        private readonly IProviderCredentialRepository _providerCredentialRepository;
        private readonly ILogger<AdminProviderHealthService> _logger;

        /// <summary>
        /// Initializes a new instance of the AdminProviderHealthService
        /// </summary>
        /// <param name="providerHealthRepository">The provider health repository</param>
        /// <param name="providerCredentialRepository">The provider credential repository</param>
        /// <param name="logger">The logger</param>
        public AdminProviderHealthService(
            IProviderHealthRepository providerHealthRepository,
            IProviderCredentialRepository providerCredentialRepository,
            ILogger<AdminProviderHealthService> logger)
        {
            _providerHealthRepository = providerHealthRepository ?? throw new ArgumentNullException(nameof(providerHealthRepository));
            _providerCredentialRepository = providerCredentialRepository ?? throw new ArgumentNullException(nameof(providerCredentialRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<ProviderHealthConfigurationDto> CreateConfigurationAsync(CreateProviderHealthConfigurationDto config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            try
            {
                // Check if configuration already exists for this provider
                // Check if this provider exists
                var existingConfig = await _providerHealthRepository.GetConfigurationAsync(config.ProviderType);
                if (existingConfig != null)
                {
                    throw new InvalidOperationException($"Provider health configuration already exists for provider '{config.ProviderType}'" );
                }

                // Verify that the provider exists
                var providerExists = await ProviderExistsByTypeAsync(config.ProviderType);
                if (!providerExists)
                {
                    throw new InvalidOperationException($"Provider '{config.ProviderType}' does not exist");
                }

                // Convert to entity and save
                var configEntity = config.ToEntity();
                await _providerHealthRepository.SaveConfigurationAsync(configEntity);

                // Retrieve the saved configuration
                var savedConfig = await _providerHealthRepository.GetConfigurationAsync(config.ProviderType);
                if (savedConfig == null)
                {
                    throw new InvalidOperationException($"Failed to retrieve newly created configuration for provider '{config.ProviderType}'");
                }

_logger.LogInformation("Created health configuration for provider '{ProviderType}'", config.ProviderType.ToString().Replace(Environment.NewLine, ""));
                return savedConfig.ToDto();
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Error creating health configuration for provider '{ProviderType}'".Replace(Environment.NewLine, ""), config.ProviderType.ToString().Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderHealthConfigurationDto>> GetAllConfigurationsAsync()
        {
            try
            {
                var configurations = await _providerHealthRepository.GetAllConfigurationsAsync();
                return configurations.Select(c => c.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all provider health configurations");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<ProviderType, ProviderHealthRecordDto>> GetAllLatestStatusesAsync()
        {
            try
            {
                var statuses = await _providerHealthRepository.GetAllLatestStatusesAsync();
                return statuses.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving latest health statuses for all providers");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderHealthRecordDto>> GetAllRecordsAsync()
        {
            try
            {
                // Since the GetStatusHistoryAsync method filters by provider name, 
                // we need a different approach to get all records.
                // Get all provider names first, then get records for each
                var latestStatuses = await _providerHealthRepository.GetAllLatestStatusesAsync();
                var providerNames = latestStatuses.Keys.ToList();

                // Use bulk query instead of N individual queries
                var allRecords = await _providerHealthRepository.GetAllRecordsAsync();

                return allRecords.Select(r => r.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all health records");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderHealthConfigurationDto?> GetConfigurationByProviderTypeAsync(ProviderType providerType)
        {
            try
            {
                var config = await _providerHealthRepository.GetConfigurationAsync(providerType);
                return config?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving health configuration for provider type '{ProviderType}'", providerType);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderHealthStatisticsDto> GetHealthStatisticsAsync(int hours = 24)
        {
            if (hours <= 0)
            {
                throw new ArgumentException("Hours must be greater than zero", nameof(hours));
            }

            try
            {
                var sinceTime = DateTime.UtcNow.AddHours(-hours);

                // Get status counts
                var allStatuses = await _providerHealthRepository.GetAllLatestStatusesAsync();
                int totalProviders = allStatuses.Count;
                int onlineProviders = allStatuses.Count(s => s.Value.Status == ProviderHealthRecord.StatusType.Online);
                int offlineProviders = allStatuses.Count(s => s.Value.Status == ProviderHealthRecord.StatusType.Offline);
                int unknownProviders = allStatuses.Count(s => s.Value.Status == ProviderHealthRecord.StatusType.Unknown);

                // Get response times
                var responseTimesDict = await _providerHealthRepository.GetAverageResponseTimesAsync(sinceTime);
                double averageResponseTime = responseTimesDict.Count > 0
                    ? responseTimesDict.Values.Average()
                    : 0;

                // Get error counts
                var errorCountDict = await _providerHealthRepository.GetErrorCountByProviderAsync(sinceTime);
                int totalErrors = errorCountDict.Values.Sum();

                // Get error categories
                var errorCategoriesDict = await _providerHealthRepository.GetErrorCategoriesByProviderAsync(sinceTime);
                var errorCategoryDistribution = new Dictionary<string, int>();

                // Combine error categories across providers
                foreach (var providerErrors in errorCategoriesDict.Values)
                {
                    foreach (var category in providerErrors)
                    {
                        if (errorCategoryDistribution.ContainsKey(category.Key))
                        {
                            errorCategoryDistribution[category.Key] += category.Value;
                        }
                        else
                        {
                            errorCategoryDistribution[category.Key] = category.Value;
                        }
                    }
                }

                return new ProviderHealthStatisticsDto
                {
                    TotalProviders = totalProviders,
                    OnlineProviders = onlineProviders,
                    OfflineProviders = offlineProviders,
                    UnknownProviders = unknownProviders,
                    AverageResponseTimeMs = averageResponseTime,
                    TotalErrors = totalErrors,
                    ErrorCategoryDistribution = errorCategoryDistribution,
                    TimePeriodHours = hours
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving health statistics for providers");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderHealthSummaryDto>> GetHealthSummaryAsync(int hours = 24)
        {
            if (hours <= 0)
            {
                throw new ArgumentException("Hours must be greater than zero", nameof(hours));
            }

            try
            {
                var sinceTime = DateTime.UtcNow.AddHours(-hours);

                // Get all current statuses
                var allStatuses = await _providerHealthRepository.GetAllLatestStatusesAsync();

                // Get uptime percentages
                var uptimeDict = await _providerHealthRepository.GetProviderUptimeAsync(sinceTime);

                // Get average response times
                var responseTimesDict = await _providerHealthRepository.GetAverageResponseTimesAsync(sinceTime);

                // Get error counts
                var errorCountDict = await _providerHealthRepository.GetErrorCountByProviderAsync(sinceTime);

                // Get error categories
                var errorCategoriesDict = await _providerHealthRepository.GetErrorCategoriesByProviderAsync(sinceTime);

                // Get configuration for all providers
                var allConfigs = await _providerHealthRepository.GetAllConfigurationsAsync();
                var configDict = allConfigs.ToDictionary(c => c.ProviderType.ToString(), c => c);

                // Combine all data into summaries
                var summaries = new List<ProviderHealthSummaryDto>();

                foreach (var provider in allStatuses.Keys)
                {
                    var status = allStatuses[provider];
                    var config = configDict.ContainsKey(provider.ToString()) ? configDict[provider.ToString()] : null;
                    var providerType = provider; // provider is already a ProviderType

                    var summary = new ProviderHealthSummaryDto
                    {
                        ProviderType = providerType,
                        Status = status.Status,
                        StatusMessage = status.StatusMessage,
                        UptimePercentage = uptimeDict.ContainsKey(provider) ? uptimeDict[provider] : 0,
                        AverageResponseTimeMs = responseTimesDict.ContainsKey(provider) ? responseTimesDict[provider] : 0,
                        ErrorCount = errorCountDict.ContainsKey(provider) ? errorCountDict[provider] : 0,
                        ErrorCategories = errorCategoriesDict.ContainsKey(provider)
                            ? errorCategoriesDict[provider]
                            : new Dictionary<string, int>(),
                        LastCheckedUtc = config?.LastCheckedUtc,
                        MonitoringEnabled = config?.MonitoringEnabled ?? false
                    };

                    summaries.Add(summary);
                }

                return summaries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving health summaries for providers");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderHealthRecordDto?> GetLatestStatusAsync(ProviderType providerType)
        {
            try
            {
                var status = await _providerHealthRepository.GetLatestStatusAsync(providerType);
                return status?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving latest health status for provider type '{ProviderType}'", providerType);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderHealthRecordDto>> GetStatusHistoryAsync(ProviderType providerType, int hours = 24, int limit = 100)
        {
            if (hours <= 0)
            {
                throw new ArgumentException("Hours must be greater than zero", nameof(hours));
            }

            if (limit <= 0)
            {
                throw new ArgumentException("Limit must be greater than zero", nameof(limit));
            }

            try
            {
                var sinceTime = DateTime.UtcNow.AddHours(-hours);
                var history = await _providerHealthRepository.GetStatusHistoryAsync(providerType, sinceTime, limit);
                return history.Select(h => h.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving health status history for provider type '{ProviderType}'", providerType);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> PurgeOldRecordsAsync(int days = 30)
        {
            if (days <= 0)
            {
                throw new ArgumentException("Days must be greater than zero", nameof(days));
            }

            try
            {
                var olderThan = DateTime.UtcNow.AddDays(-days);
                var purgedCount = await _providerHealthRepository.PurgeOldRecordsAsync(olderThan);

                _logger.LogInformation("Purged {Count} health records older than {Date}", purgedCount, olderThan);
                return purgedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error purging old health records");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderHealthRecordDto> TriggerHealthCheckAsync(ProviderType providerType)
        {
            try
            {
                var providerExists = await ProviderExistsByTypeAsync(providerType);
                if (!providerExists)
                {
                    throw new InvalidOperationException($"Provider '{providerType}' does not exist");
                }

                
                // Create a mock health check record
                // In a real implementation, this would trigger the health check service
                var record = new ProviderHealthRecord
                {
                    ProviderType = providerType,
                    Status = ProviderHealthRecord.StatusType.Unknown,
                    StatusMessage = "Manual health check triggered",
                    TimestampUtc = DateTime.UtcNow,
                    ResponseTimeMs = 0,
                    ErrorCategory = null,
                    ErrorDetails = null,
                    EndpointUrl = null
                };

                // Save the record
                await _providerHealthRepository.SaveStatusAsync(record);

                // Update last checked time
                await _providerHealthRepository.UpdateLastCheckedTimeAsync(providerType);

                _logger.LogInformation("Triggered health check for provider type '{ProviderType}'", providerType);
                return record.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering health check for provider type '{ProviderType}'", providerType);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateConfigurationAsync(UpdateProviderHealthConfigurationDto config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            try
            {
                // Get existing configuration by id
                var existingConfig = await _providerHealthRepository.GetConfigurationByIdAsync(config.Id);
                if (existingConfig == null)
                {
                    _logger.LogWarning("Provider health configuration not found with ID {Id}", config.Id);
                    return false;
                }

                // Update entity using the extension method from ProviderHealthExtensions
                ProviderHealthExt.UpdateFrom(existingConfig, config);

                // Save changes
                await _providerHealthRepository.SaveConfigurationAsync(existingConfig);

                string providerName = existingConfig.ProviderType.ToString(); // Get the name from the existing config
                _logger.LogInformation("Updated health configuration for provider '{ProviderName}'", providerName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating health configuration for provider");
                throw;
            }
        }

        /// <summary>
        /// Checks if a provider exists
        /// </summary>
        /// <param name="providerType">The type of the provider</param>
        /// <returns>True if the provider exists, false otherwise</returns>
        private async Task<bool> ProviderExistsByTypeAsync(ProviderType providerType)
        {
            try
            {
                var credential = await _providerCredentialRepository.GetByProviderTypeAsync(providerType);
                return credential != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if provider '{ProviderType}' exists", providerType);
                return false;
            }
        }
    }
}
