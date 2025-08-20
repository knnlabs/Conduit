using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Models;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Cache configuration service - Helper methods and environment configuration
    /// </summary>
    public partial class CacheConfigurationService
    {
        public async Task ApplyEnvironmentConfigurationsAsync(CancellationToken cancellationToken = default)
        {
            var applied = 0;

            foreach (string region in CacheRegions.All)
            {
                var envKey = $"CONDUIT_CACHE_{region.ToString().ToUpperInvariant()}_";
                var envConfig = new Dictionary<string, string?>();

                // Check for environment variables
                var enabled = Environment.GetEnvironmentVariable($"{envKey}ENABLED");
                if (!string.IsNullOrEmpty(enabled))
                {
                    envConfig["Enabled"] = enabled;
                }

                var ttl = Environment.GetEnvironmentVariable($"{envKey}TTL");
                if (!string.IsNullOrEmpty(ttl))
                {
                    envConfig["DefaultTTL"] = ttl;
                }

                var maxTtl = Environment.GetEnvironmentVariable($"{envKey}MAX_TTL");
                if (!string.IsNullOrEmpty(maxTtl))
                {
                    envConfig["MaxTTL"] = maxTtl;
                }

                if (envConfig.Count() > 0)
                {
                    try
                    {
                        var currentConfig = await GetConfigurationAsync(region, cancellationToken);
                        if (currentConfig == null)
                        {
                            currentConfig = new CacheRegionConfig { Region = region };
                        }

                        // Apply environment overrides
                        if (envConfig.TryGetValue("Enabled", out var enabledStr) && bool.TryParse(enabledStr, out var enabledValue))
                        {
                            currentConfig.Enabled = enabledValue;
                        }

                        if (envConfig.TryGetValue("DefaultTTL", out var ttlStr) && int.TryParse(ttlStr, out var ttlSeconds))
                        {
                            currentConfig.DefaultTTL = TimeSpan.FromSeconds(ttlSeconds);
                        }

                        if (envConfig.TryGetValue("MaxTTL", out var maxTtlStr) && int.TryParse(maxTtlStr, out var maxTtlSeconds))
                        {
                            currentConfig.MaxTTL = TimeSpan.FromSeconds(maxTtlSeconds);
                        }

                        // Check if configuration exists in database
                        var exists = await _dbContext.CacheConfigurations
                            .AnyAsync(c => c.Region == region && c.IsActive, cancellationToken);

                        if (exists)
                        {
                            await UpdateConfigurationAsync(region, currentConfig, "System", "Applied from environment variables", cancellationToken);
                        }
                        else
                        {
                            await CreateConfigurationAsync(region, currentConfig, "System", cancellationToken);
                        }

                        applied++;
                        _logger.LogInformation("Applied environment configuration for cache region {Region}", region);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to apply environment configuration for cache region {Region}", region);
                    }
                }
            }

            if (applied > 0)
            {
                _logger.LogInformation("Applied {Count} cache configurations from environment variables", applied);
            }
        }

        private CacheRegionConfig MapEntityToConfig(CacheConfiguration entity)
        {
            var config = new CacheRegionConfig
            {
                Region = entity.Region,
                Enabled = entity.Enabled,
                Priority = entity.Priority,
                EvictionPolicy = entity.EvictionPolicy,
                UseMemoryCache = entity.UseMemoryCache,
                UseDistributedCache = entity.UseDistributedCache,
                EnableDetailedStats = entity.EnableDetailedStats,
                EnableCompression = entity.EnableCompression
            };

            if (entity.DefaultTtlSeconds.HasValue)
            {
                config.DefaultTTL = TimeSpan.FromSeconds(entity.DefaultTtlSeconds.Value);
            }

            if (entity.MaxTtlSeconds.HasValue)
            {
                config.MaxTTL = TimeSpan.FromSeconds(entity.MaxTtlSeconds.Value);
            }

            if (entity.MaxEntries.HasValue)
            {
                config.MaxEntries = entity.MaxEntries.Value;
            }

            if (entity.MaxMemoryBytes.HasValue)
            {
                config.MaxMemoryBytes = entity.MaxMemoryBytes.Value;
            }

            if (entity.CompressionThresholdBytes.HasValue)
            {
                config.CompressionThresholdBytes = entity.CompressionThresholdBytes.Value;
            }

            // Parse extended config if available
            if (!string.IsNullOrEmpty(entity.ExtendedConfig))
            {
                try
                {
                    var extended = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.ExtendedConfig);
                    if (extended != null)
                    {
                        config.ExtendedProperties = extended;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse extended config for region {Region}", entity.Region);
                }
            }

            return config;
        }

        private void UpdateEntityFromConfig(CacheConfiguration entity, CacheRegionConfig config)
        {
            entity.Enabled = config.Enabled;
            entity.Priority = config.Priority;
            entity.EvictionPolicy = config.EvictionPolicy;
            entity.UseMemoryCache = config.UseMemoryCache;
            entity.UseDistributedCache = config.UseDistributedCache;
            entity.EnableDetailedStats = config.EnableDetailedStats;
            entity.EnableCompression = config.EnableCompression;

            entity.DefaultTtlSeconds = config.DefaultTTL?.TotalSeconds > 0 ? (int)config.DefaultTTL.Value.TotalSeconds : null;
            entity.MaxTtlSeconds = config.MaxTTL?.TotalSeconds > 0 ? (int)config.MaxTTL.Value.TotalSeconds : null;
            entity.MaxEntries = config.MaxEntries;
            entity.MaxMemoryBytes = config.MaxMemoryBytes;
            entity.CompressionThresholdBytes = config.CompressionThresholdBytes;

            if (config.ExtendedProperties?.Count > 0)
            {
                entity.ExtendedConfig = JsonSerializer.Serialize(config.ExtendedProperties);
            }
        }

        private CacheRegionConfig CreateConfigFromSection(string region, IConfigurationSection section)
        {
            var config = new CacheRegionConfig
            {
                Region = region,
                Enabled = section.GetValue<bool>("Enabled", true),
                Priority = section.GetValue<int>("Priority", 50),
                UseMemoryCache = section.GetValue<bool>("UseMemoryCache", true),
                UseDistributedCache = section.GetValue<bool>("UseDistributedCache", false),
                EnableDetailedStats = section.GetValue<bool>("EnableDetailedStats", true),
                EnableCompression = section.GetValue<bool>("EnableCompression", false)
            };

            var ttlSeconds = section.GetValue<int?>("DefaultTtlSeconds");
            if (ttlSeconds.HasValue)
            {
                config.DefaultTTL = TimeSpan.FromSeconds(ttlSeconds.Value);
            }

            var maxTtlSeconds = section.GetValue<int?>("MaxTtlSeconds");
            if (maxTtlSeconds.HasValue)
            {
                config.MaxTTL = TimeSpan.FromSeconds(maxTtlSeconds.Value);
            }

            config.MaxEntries = section.GetValue<long?>("MaxEntries");
            config.MaxMemoryBytes = section.GetValue<long?>("MaxMemoryBytes");
            config.CompressionThresholdBytes = section.GetValue<long?>("CompressionThresholdBytes");

            var evictionPolicy = section.GetValue<string>("EvictionPolicy");
            if (!string.IsNullOrEmpty(evictionPolicy))
            {
                config.EvictionPolicy = evictionPolicy;
            }

            return config;
        }

        private async Task CacheConfigAsync(string region, CacheRegionConfig config, CancellationToken cancellationToken)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _cache[region] = config;
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    /// <summary>
    /// Validation result for cache configurations.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; } = new();

        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }
    }
}