using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MassTransit;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.Models;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Service for managing cache configurations with dynamic runtime updates.
    /// </summary>
    public interface ICacheConfigurationService
    {
        /// <summary>
        /// Gets the configuration for a specific cache region.
        /// </summary>
        Task<CacheRegionConfig?> GetConfigurationAsync(string region, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all active cache configurations.
        /// </summary>
        Task<Dictionary<string, CacheRegionConfig>> GetAllConfigurationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the configuration for a specific cache region.
        /// </summary>
        Task<CacheRegionConfig> UpdateConfigurationAsync(string region, CacheRegionConfig config, string changedBy, string? reason = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new configuration for a cache region.
        /// </summary>
        Task<CacheRegionConfig> CreateConfigurationAsync(string region, CacheRegionConfig config, string createdBy, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the configuration for a cache region.
        /// </summary>
        Task<bool> DeleteConfigurationAsync(string region, string deletedBy, string? reason = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a cache configuration.
        /// </summary>
        Task<ValidationResult> ValidateConfigurationAsync(CacheRegionConfig config, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the audit history for a cache region.
        /// </summary>
        Task<IEnumerable<CacheConfigurationAudit>> GetAuditHistoryAsync(string region, int limit = 100, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rolls back to a previous configuration.
        /// </summary>
        Task<CacheRegionConfig> RollbackConfigurationAsync(string region, int auditId, string rolledBackBy, CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies configurations from environment variables or config files.
        /// </summary>
        Task ApplyEnvironmentConfigurationsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Implementation of cache configuration service.
    /// </summary>
    public partial class CacheConfigurationService : ICacheConfigurationService
    {
        private readonly ConduitDbContext _dbContext;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CacheConfigurationService> _logger;
        private readonly Dictionary<string, CacheRegionConfig> _cache = new();
        private readonly SemaphoreSlim _lock = new(1, 1);

        public CacheConfigurationService(
            ConduitDbContext dbContext,
            IPublishEndpoint publishEndpoint,
            IConfiguration configuration,
            ILogger<CacheConfigurationService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CacheRegionConfig?> GetConfigurationAsync(string region, CancellationToken cancellationToken = default)
        {
            // Check memory cache first
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_cache.TryGetValue(region, out var cached))
                {
                    return cached;
                }
            }
            finally
            {
                _lock.Release();
            }

            // Load from database
            var entity = await _dbContext.CacheConfigurations
                .Where(c => c.Region == region && c.IsActive)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity == null)
            {
                // Try to load from configuration
                var configSection = _configuration.GetSection($"Cache:Regions:{region}");
                if (configSection.Exists())
                {
                    var config = CreateConfigFromSection(region, configSection);
                    await CacheConfigAsync(region, config, cancellationToken);
                    return config;
                }

                return null;
            }

            var regionConfig = MapEntityToConfig(entity);
            await CacheConfigAsync(region, regionConfig, cancellationToken);
            return regionConfig;
        }

        public async Task<Dictionary<string, CacheRegionConfig>> GetAllConfigurationsAsync(CancellationToken cancellationToken = default)
        {
            var configs = new Dictionary<string, CacheRegionConfig>();

            // Load all from database
            var entities = await _dbContext.CacheConfigurations
                .Where(c => c.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var entity in entities)
            {
                configs[entity.Region] = MapEntityToConfig(entity);
            }

            // Load any missing from configuration
            foreach (string region in CacheRegions.All)
            {
                if (!configs.ContainsKey(region))
                {
                    var configSection = _configuration.GetSection($"Cache:Regions:{region}");
                    if (configSection.Exists())
                    {
                        configs[region] = CreateConfigFromSection(region, configSection);
                    }
                }
            }

            // Update cache
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _cache.Clear();
                foreach (var (region, config) in configs)
                {
                    _cache[region] = config;
                }
            }
            finally
            {
                _lock.Release();
            }

            return configs;
        }

        public async Task<CacheRegionConfig> UpdateConfigurationAsync(
            string region, 
            CacheRegionConfig config, 
            string changedBy, 
            string? reason = null, 
            CancellationToken cancellationToken = default)
        {
            // Validate configuration
            var validation = await ValidateConfigurationAsync(config, cancellationToken);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException($"Invalid configuration: {string.Join(", ", validation.Errors)}");
            }

            var entity = await _dbContext.CacheConfigurations
                .Where(c => c.Region == region && c.IsActive)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity == null)
            {
                throw new InvalidOperationException($"No active configuration found for region {region}");
            }

            // Store old config for audit
            var oldConfig = MapEntityToConfig(entity);

            // Create audit entry
            var audit = new CacheConfigurationAudit
            {
                Region = region,
                Action = "Updated",
                OldConfigJson = JsonSerializer.Serialize(oldConfig),
                NewConfigJson = JsonSerializer.Serialize(config),
                Reason = reason,
                ChangedBy = changedBy,
                ChangedAt = DateTime.UtcNow,
                ChangeSource = "API"
            };

            try
            {
                // Update entity
                UpdateEntityFromConfig(entity, config);
                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedBy = changedBy;

                _dbContext.CacheConfigurationAudits.Add(audit);
                await _dbContext.SaveChangesAsync(cancellationToken);

                audit.Success = true;

                // Update cache
                await CacheConfigAsync(region, config, cancellationToken);

                // Publish event
                await _publishEndpoint.Publish(new CacheConfigurationChangedEvent
                {
                    Region = region,
                    Action = "Updated",
                    OldConfig = oldConfig,
                    NewConfig = config,
                    ChangedBy = changedBy,
                    ChangedAt = DateTime.UtcNow,
                    Reason = reason,
                    ChangeSource = "API"
                }, cancellationToken);

                _logger.LogInformation("Updated cache configuration for region {Region}", region);
                return config;
            }
            catch (Exception ex)
            {
                audit.Success = false;
                audit.ErrorMessage = ex.Message;
                _dbContext.CacheConfigurationAudits.Add(audit);
                await _dbContext.SaveChangesAsync(cancellationToken);
                throw;
            }
        }

        public async Task<CacheRegionConfig> CreateConfigurationAsync(
            string region, 
            CacheRegionConfig config, 
            string createdBy, 
            CancellationToken cancellationToken = default)
        {
            // Validate configuration
            var validation = await ValidateConfigurationAsync(config, cancellationToken);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException($"Invalid configuration: {string.Join(", ", validation.Errors)}");
            }

            // Check if already exists
            var existing = await _dbContext.CacheConfigurations
                .Where(c => c.Region == region && c.IsActive)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing != null)
            {
                throw new InvalidOperationException($"Active configuration already exists for region {region}");
            }

            var entity = new CacheConfiguration
            {
                Region = region,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
                IsActive = true
            };

            UpdateEntityFromConfig(entity, config);

            // Create audit entry
            var audit = new CacheConfigurationAudit
            {
                Region = region,
                Action = "Created",
                NewConfigJson = JsonSerializer.Serialize(config),
                ChangedBy = createdBy,
                ChangedAt = DateTime.UtcNow,
                ChangeSource = "API"
            };

            try
            {
                _dbContext.CacheConfigurations.Add(entity);
                _dbContext.CacheConfigurationAudits.Add(audit);
                await _dbContext.SaveChangesAsync(cancellationToken);

                audit.Success = true;

                // Update cache
                await CacheConfigAsync(region, config, cancellationToken);

                // Publish event
                await _publishEndpoint.Publish(new CacheConfigurationChangedEvent
                {
                    Region = region,
                    Action = "Created",
                    NewConfig = config,
                    ChangedBy = createdBy,
                    ChangedAt = DateTime.UtcNow,
                    ChangeSource = "API"
                }, cancellationToken);

                _logger.LogInformation("Created cache configuration for region {Region}", region);
                return config;
            }
            catch (Exception ex)
            {
                audit.Success = false;
                audit.ErrorMessage = ex.Message;
                _dbContext.CacheConfigurationAudits.Add(audit);
                await _dbContext.SaveChangesAsync(cancellationToken);
                throw;
            }
        }

        public async Task<bool> DeleteConfigurationAsync(
            string region, 
            string deletedBy, 
            string? reason = null, 
            CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.CacheConfigurations
                .Where(c => c.Region == region && c.IsActive)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity == null)
            {
                return false;
            }

            var oldConfig = MapEntityToConfig(entity);

            // Create audit entry
            var audit = new CacheConfigurationAudit
            {
                Region = region,
                Action = "Deleted",
                OldConfigJson = JsonSerializer.Serialize(oldConfig),
                Reason = reason,
                ChangedBy = deletedBy,
                ChangedAt = DateTime.UtcNow,
                ChangeSource = "API"
            };

            try
            {
                // Soft delete
                entity.IsActive = false;
                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedBy = deletedBy;

                _dbContext.CacheConfigurationAudits.Add(audit);
                await _dbContext.SaveChangesAsync(cancellationToken);

                audit.Success = true;

                // Remove from cache
                await _lock.WaitAsync(cancellationToken);
                try
                {
                    _cache.Remove(region);
                }
                finally
                {
                    _lock.Release();
                }

                // Publish event
                await _publishEndpoint.Publish(new CacheConfigurationChangedEvent
                {
                    Region = region,
                    Action = "Deleted",
                    OldConfig = oldConfig,
                    ChangedBy = deletedBy,
                    ChangedAt = DateTime.UtcNow,
                    Reason = reason,
                    ChangeSource = "API"
                }, cancellationToken);

                _logger.LogInformation("Deleted cache configuration for region {Region}", region);
                return true;
            }
            catch (Exception ex)
            {
                audit.Success = false;
                audit.ErrorMessage = ex.Message;
                _dbContext.CacheConfigurationAudits.Add(audit);
                await _dbContext.SaveChangesAsync(cancellationToken);
                throw;
            }
        }

        public Task<ValidationResult> ValidateConfigurationAsync(CacheRegionConfig config, CancellationToken cancellationToken = default)
        {
            var result = new ValidationResult { IsValid = true };

            // Validate TTL
            if (config.DefaultTTL.HasValue && config.DefaultTTL.Value < TimeSpan.Zero)
            {
                result.AddError("DefaultTTL cannot be negative");
            }

            if (config.MaxTTL.HasValue && config.MaxTTL.Value < TimeSpan.Zero)
            {
                result.AddError("MaxTTL cannot be negative");
            }

            if (config.DefaultTTL.HasValue && config.MaxTTL.HasValue && config.DefaultTTL.Value > config.MaxTTL.Value)
            {
                result.AddError("DefaultTTL cannot be greater than MaxTTL");
            }

            // Validate sizes
            if (config.MaxEntries.HasValue && config.MaxEntries.Value <= 0)
            {
                result.AddError("MaxEntries must be greater than 0");
            }

            if (config.MaxMemoryBytes.HasValue && config.MaxMemoryBytes.Value <= 0)
            {
                result.AddError("MaxMemoryBytes must be greater than 0");
            }

            // Validate priority
            if (config.Priority < 0 || config.Priority > 100)
            {
                result.AddError("Priority must be between 0 and 100");
            }

            // Validate compression
            if (config.EnableCompression && config.CompressionThresholdBytes.HasValue && config.CompressionThresholdBytes.Value <= 0)
            {
                result.AddError("CompressionThresholdBytes must be greater than 0 when compression is enabled");
            }

            return Task.FromResult(result);
        }
    }
}