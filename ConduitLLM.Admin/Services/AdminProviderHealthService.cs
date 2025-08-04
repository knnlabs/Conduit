using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service implementation for managing provider health monitoring through the Admin API
    /// </summary>
    public class AdminProviderHealthService : IAdminProviderHealthService
    {
        private readonly IProviderHealthRepository _providerHealthRepository;
        private readonly IProviderRepository _ProviderRepository;
        private readonly ILogger<AdminProviderHealthService> _logger;

        /// <summary>
        /// Initializes a new instance of the AdminProviderHealthService
        /// </summary>
        /// <param name="providerHealthRepository">The provider health repository</param>
        /// <param name="ProviderRepository">The provider credential repository</param>
        /// <param name="logger">The logger</param>
        public AdminProviderHealthService(
            IProviderHealthRepository providerHealthRepository,
            IProviderRepository ProviderRepository,
            ILogger<AdminProviderHealthService> logger)
        {
            _providerHealthRepository = providerHealthRepository ?? throw new ArgumentNullException(nameof(providerHealthRepository));
            _ProviderRepository = ProviderRepository ?? throw new ArgumentNullException(nameof(ProviderRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<ProviderHealthConfiguration> CreateConfigurationAsync(ProviderHealthConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            try
            {
                // Check if configuration already exists for this provider
                var existingConfig = await _providerHealthRepository.GetConfigurationAsync(config.ProviderId);
                if (existingConfig != null)
                {
                    throw new InvalidOperationException($"Provider health configuration already exists for provider ID '{config.ProviderId}'" );
                }

                // Verify that the provider exists
                var provider = await _ProviderRepository.GetByIdAsync(config.ProviderId);
                if (provider == null)
                {
                    throw new InvalidOperationException($"Provider with ID '{config.ProviderId}' does not exist");
                }

                // Save the configuration
                await _providerHealthRepository.SaveConfigurationAsync(config);

                // Retrieve the saved configuration
                var savedConfig = await _providerHealthRepository.GetConfigurationAsync(config.ProviderId);
                if (savedConfig == null)
                {
                    throw new InvalidOperationException($"Failed to retrieve newly created configuration for provider ID '{config.ProviderId}'");
                }

                _logger.LogInformation("Created health configuration for provider ID '{ProviderId}'", config.ProviderId);
                return savedConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating health configuration for provider ID '{ProviderId}'", config.ProviderId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderHealthConfiguration>> GetAllConfigurationsAsync()
        {
            try
            {
                return await _providerHealthRepository.GetAllConfigurationsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all provider health configurations");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<int, ProviderHealthRecord>> GetAllLatestStatusesAsync()
        {
            try
            {
                return await _providerHealthRepository.GetAllLatestStatusesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving latest health statuses for all providers");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderHealthRecord>> GetAllRecordsAsync()
        {
            try
            {
                return await _providerHealthRepository.GetAllRecordsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all health records");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderHealthConfiguration?> GetConfigurationByProviderIdAsync(int providerId)
        {
            try
            {
                return await _providerHealthRepository.GetConfigurationAsync(providerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving health configuration for provider ID '{ProviderId}'", providerId);
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
                var configDict = allConfigs.ToDictionary(c => c.ProviderId, c => c);

                // Combine all data into summaries
                var summaries = new List<ProviderHealthSummaryDto>();

                foreach (var providerId in allStatuses.Keys)
                {
                    var status = allStatuses[providerId];
                    var config = configDict.ContainsKey(providerId) ? configDict[providerId] : null;

                    var summary = new ProviderHealthSummaryDto
                    {
                        ProviderId = providerId,
                        Status = status.Status,
                        StatusMessage = status.StatusMessage,
                        UptimePercentage = uptimeDict.ContainsKey(providerId) ? uptimeDict[providerId] : 0,
                        AverageResponseTimeMs = responseTimesDict.ContainsKey(providerId) ? responseTimesDict[providerId] : 0,
                        ErrorCount = errorCountDict.ContainsKey(providerId) ? errorCountDict[providerId] : 0,
                        ErrorCategories = errorCategoriesDict.ContainsKey(providerId)
                            ? errorCategoriesDict[providerId]
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
        public async Task<ProviderHealthRecord?> GetLatestStatusAsync(int providerId)
        {
            try
            {
                return await _providerHealthRepository.GetLatestStatusAsync(providerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving latest health status for provider ID '{ProviderId}'", providerId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderHealthRecord>> GetStatusHistoryAsync(int providerId, int hours = 24, int limit = 100)
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
                return await _providerHealthRepository.GetStatusHistoryAsync(providerId, sinceTime, limit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving health status history for provider ID '{ProviderId}'", providerId);
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
        public async Task<ProviderHealthRecord> TriggerHealthCheckAsync(int providerId)
        {
            try
            {
                var provider = await _ProviderRepository.GetByIdAsync(providerId);
                if (provider == null)
                {
                    throw new InvalidOperationException($"Provider with ID '{providerId}' does not exist");
                }

                
                // Create a mock health check record
                // In a real implementation, this would trigger the health check service
                var record = new ProviderHealthRecord
                {
                    ProviderId = providerId,
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
                await _providerHealthRepository.UpdateLastCheckedTimeAsync(providerId);

                _logger.LogInformation("Triggered health check for provider ID '{ProviderId}'", providerId);
                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering health check for provider ID '{ProviderId}'", providerId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateConfigurationAsync(ProviderHealthConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            try
            {
                // Verify the configuration exists
                var existingConfig = await _providerHealthRepository.GetConfigurationAsync(config.ProviderId);
                if (existingConfig == null)
                {
                    _logger.LogWarning("Provider health configuration not found for provider ID {ProviderId}", config.ProviderId);
                    return false;
                }

                // Save the updated configuration
                await _providerHealthRepository.SaveConfigurationAsync(config);

                _logger.LogInformation("Updated health configuration for provider ID '{ProviderId}'", config.ProviderId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating health configuration for provider");
                throw;
            }
        }

    }
}
