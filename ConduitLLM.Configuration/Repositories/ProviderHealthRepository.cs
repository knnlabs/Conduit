using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static ConduitLLM.Configuration.Utilities.LogSanitizer;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for provider health monitoring
    /// </summary>
    public class ProviderHealthRepository : IProviderHealthRepository
    {
        private readonly IConfigurationDbContext _dbContext;
        private readonly ILogger<ProviderHealthRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the ProviderHealthRepository class
        /// </summary>
        /// <param name="dbContext">The database context</param>
        /// <param name="logger">The logger</param>
        public ProviderHealthRepository(
            IConfigurationDbContext dbContext,
            ILogger<ProviderHealthRepository> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<ProviderHealthRecord?> GetLatestStatusAsync(string providerName)
        {
            try
            {
                return await _dbContext.ProviderHealthRecords
                    .Where(r => r.ProviderName == providerName)
                    .OrderByDescending(r => r.TimestampUtc)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest health status for provider {ProviderName}", providerName.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<ProviderHealthRecord>> GetStatusHistoryAsync(string providerName, DateTime since, int limit = 100)
        {
            try
            {
                return await _dbContext.ProviderHealthRecords
                    .Where(r => r.ProviderName == providerName && r.TimestampUtc >= since)
                    .OrderByDescending(r => r.TimestampUtc)
                    .Take(limit)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting health status history for provider {ProviderName}", providerName.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SaveStatusAsync(ProviderHealthRecord status)
        {
            try
            {
                await _dbContext.ProviderHealthRecords.AddAsync(status);
                await _dbContext.SaveChangesAsync();

                _logger.LogDebug("Saved health status for provider {ProviderName}: {IsOnline}",
                    status.ProviderName.Replace(Environment.NewLine, ""), status.IsOnline);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving health status for provider {ProviderName}", status.ProviderName.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, ProviderHealthRecord>> GetAllLatestStatusesAsync()
        {
            try
            {
                // This query finds the latest status record for each provider
                var latestStatusIds = await _dbContext.ProviderHealthRecords
                    .GroupBy(r => r.ProviderName)
                    .Select(g => g.OrderByDescending(r => r.TimestampUtc).FirstOrDefault()!.Id)
                    .ToListAsync();

                var latestStatuses = await _dbContext.ProviderHealthRecords
                    .Where(r => latestStatusIds.Contains(r.Id))
                    .ToListAsync();

                return latestStatuses.ToDictionary(r => r.ProviderName, r => r);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all latest health statuses");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderHealthConfiguration?> GetConfigurationAsync(string providerName)
        {
            try
            {
                return await _dbContext.ProviderHealthConfigurations
                    .FirstOrDefaultAsync(c => c.ProviderName == providerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting health configuration for provider {ProviderName}", providerName.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SaveConfigurationAsync(ProviderHealthConfiguration config)
        {
            try
            {
                var existing = await _dbContext.ProviderHealthConfigurations
                    .FirstOrDefaultAsync(c => c.ProviderName == config.ProviderName);

                if (existing == null)
                {
                    await _dbContext.ProviderHealthConfigurations.AddAsync(config);
                }
                else
                {
                    // Update existing entity properties
                    existing.MonitoringEnabled = config.MonitoringEnabled;
                    existing.CheckIntervalMinutes = config.CheckIntervalMinutes;
                    existing.TimeoutSeconds = config.TimeoutSeconds;
                    existing.ConsecutiveFailuresThreshold = config.ConsecutiveFailuresThreshold;
                    existing.NotificationsEnabled = config.NotificationsEnabled;
                    existing.CustomEndpointUrl = config.CustomEndpointUrl;
                    existing.LastCheckedUtc = config.LastCheckedUtc;
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogDebug("Saved health configuration for provider {ProviderName}", config.ProviderName.Replace(Environment.NewLine, ""));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving health configuration for provider {ProviderName}", config.ProviderName.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<ProviderHealthConfiguration>> GetAllConfigurationsAsync()
        {
            try
            {
                return await _dbContext.ProviderHealthConfigurations.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all health configurations");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, double>> GetProviderUptimeAsync(DateTime since)
        {
            try
            {
                var uptimeResult = new Dictionary<string, double>();

                // Group records by provider and calculate uptime percentage
                var providerRecords = await _dbContext.ProviderHealthRecords
                    .Where(r => r.TimestampUtc >= since)
                    .GroupBy(r => r.ProviderName)
                    .Select(g => new
                    {
                        ProviderName = g.Key,
                        TotalChecks = g.Count(),
                        SuccessfulChecks = g.Count(r => r.IsOnline)
                    })
                    .ToListAsync();

                foreach (var record in providerRecords)
                {
                    double uptimePercentage = record.TotalChecks > 0
                        ? (double)record.SuccessfulChecks / record.TotalChecks * 100
                        : 0;

                    uptimeResult[record.ProviderName] = Math.Round(uptimePercentage, 2);
                }

                return uptimeResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating provider uptimes since {Since}", since);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, double>> GetAverageResponseTimesAsync(DateTime since)
        {
            try
            {
                var responseTimeResult = new Dictionary<string, double>();

                // Group records by provider and calculate average response time
                var providerResponseTimes = await _dbContext.ProviderHealthRecords
                    .Where(r => r.TimestampUtc >= since && r.IsOnline) // Only include successful checks
                    .GroupBy(r => r.ProviderName)
                    .Select(g => new
                    {
                        ProviderName = g.Key,
                        AverageResponseTime = g.Average(r => r.ResponseTimeMs)
                    })
                    .ToListAsync();

                foreach (var record in providerResponseTimes)
                {
                    responseTimeResult[record.ProviderName] = Math.Round(record.AverageResponseTime, 2);
                }

                return responseTimeResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating average response times since {Since}", since);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, int>> GetErrorCountByProviderAsync(DateTime since)
        {
            try
            {
                var errorCountResult = new Dictionary<string, int>();

                // Group records by provider and count errors
                var providerErrors = await _dbContext.ProviderHealthRecords
                    .Where(r => r.TimestampUtc >= since && !r.IsOnline) // Only include failed checks
                    .GroupBy(r => r.ProviderName)
                    .Select(g => new
                    {
                        ProviderName = g.Key,
                        ErrorCount = g.Count()
                    })
                    .ToListAsync();

                foreach (var record in providerErrors)
                {
                    errorCountResult[record.ProviderName] = record.ErrorCount;
                }

                return errorCountResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting errors by provider since {Since}", since);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, Dictionary<string, int>>> GetErrorCategoriesByProviderAsync(DateTime since)
        {
            try
            {
                var result = new Dictionary<string, Dictionary<string, int>>();

                // Get all error records with categories
                var errorRecords = await _dbContext.ProviderHealthRecords
                    .Where(r => r.TimestampUtc >= since && !r.IsOnline && r.ErrorCategory != null)
                    .ToListAsync();

                // Group by provider and then by error category
                foreach (var record in errorRecords)
                {
                    if (!result.ContainsKey(record.ProviderName))
                    {
                        result[record.ProviderName] = new Dictionary<string, int>();
                    }

                    var categoryDict = result[record.ProviderName];
                    var category = record.ErrorCategory ?? "Unknown"; // Default category if null

                    if (!categoryDict.ContainsKey(category))
                    {
                        categoryDict[category] = 0;
                    }

                    categoryDict[category]++;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting error categories by provider since {Since}", since);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> PurgeOldRecordsAsync(DateTime olderThan)
        {
            try
            {
                // Find records to delete
                var recordsToDelete = await _dbContext.ProviderHealthRecords
                    .Where(r => r.TimestampUtc < olderThan)
                    .ToListAsync();

                int count = recordsToDelete.Count;
                if (count > 0)
                {
                    // Remove records in batches to avoid performance issues
                    const int batchSize = 1000;

                    for (int i = 0; i < count; i += batchSize)
                    {
                        var batch = recordsToDelete.Skip(i).Take(batchSize).ToList();
                        _dbContext.ProviderHealthRecords.RemoveRange(batch);
                        await _dbContext.SaveChangesAsync();
                    }

                    _logger.LogInformation("Purged {Count} provider health records older than {OlderThan}",
                        count, olderThan);
                }

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error purging old health records older than {OlderThan}", olderThan);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderHealthConfiguration> EnsureConfigurationExistsAsync(string providerName)
        {
            try
            {
                var config = await GetConfigurationAsync(providerName);

                if (config == null)
                {
                    // Create default configuration
                    config = new ProviderHealthConfiguration
                    {
                        ProviderName = providerName,
                        MonitoringEnabled = true,
                        CheckIntervalMinutes = 5,
                        TimeoutSeconds = 10,
                        ConsecutiveFailuresThreshold = 3,
                        NotificationsEnabled = true,
                        LastCheckedUtc = null
                    };

                    await SaveConfigurationAsync(config);
                    _logger.LogInformation("Created default health configuration for provider {ProviderName}", providerName.Replace(Environment.NewLine, ""));
                }

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring health configuration exists for provider {ProviderName}", providerName.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task UpdateLastCheckedTimeAsync(string providerName)
        {
            try
            {
                var config = await GetConfigurationAsync(providerName);

                if (config != null)
                {
                    config.LastCheckedUtc = DateTime.UtcNow;
                    await SaveConfigurationAsync(config);
                }
                else
                {
                    _logger.LogWarning("Attempted to update LastCheckedUtc for non-existent provider {ProviderName}", providerName.Replace(Environment.NewLine, ""));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating LastCheckedUtc for provider {ProviderName}", providerName.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> GetConsecutiveFailuresAsync(string providerName, DateTime since)
        {
            try
            {
                // Get recent records ordered by timestamp
                var recentRecords = await _dbContext.ProviderHealthRecords
                    .Where(r => r.ProviderName == providerName && r.TimestampUtc >= since)
                    .OrderByDescending(r => r.TimestampUtc)
                    .ToListAsync();

                // Count consecutive failures from the most recent record
                int consecutiveFailures = 0;
                foreach (var record in recentRecords)
                {
                    if (!record.IsOnline)
                    {
                        consecutiveFailures++;
                    }
                    else
                    {
                        // Stop counting when we hit a success
                        break;
                    }
                }

                return consecutiveFailures;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting consecutive failures for provider {ProviderName}", providerName.Replace(Environment.NewLine, ""));
                throw;
            }
        }
    }
}
