using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using Microsoft.EntityFrameworkCore;

// Import the model provider mapping from the root namespace
using ConduitLLM.Configuration;

namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// Interface for the configuration database context
    /// </summary>
    public interface IConfigurationDbContext : IDisposable
    {
        /// <summary>
        /// Database set for virtual keys
        /// </summary>
        DbSet<VirtualKey> VirtualKeys { get; }

        /// <summary>
        /// Database set for request logs
        /// </summary>
        DbSet<RequestLog> RequestLogs { get; }

        /// <summary>
        /// Database set for virtual key spend history
        /// </summary>
        DbSet<VirtualKeySpendHistory> VirtualKeySpendHistory { get; }

        /// <summary>
        /// Database set for notifications
        /// </summary>
        DbSet<Notification> Notifications { get; }

        /// <summary>
        /// Database set for global settings
        /// </summary>
        DbSet<GlobalSetting> GlobalSettings { get; }

        /// <summary>
        /// Database set for model costs
        /// </summary>
        DbSet<ModelCost> ModelCosts { get; }

        /// <summary>
        /// Database set for model provider mappings
        /// </summary>
        DbSet<ConduitLLM.Configuration.Entities.ModelProviderMapping> ModelProviderMappings { get; }

        /// <summary>
        /// Database set for provider credentials
        /// </summary>
        DbSet<ProviderCredential> ProviderCredentials { get; }

        /// <summary>
        /// Database set for router configurations
        /// </summary>
        DbSet<RouterConfigEntity> RouterConfigurations { get; }

        /// <summary>
        /// Database set for model deployments
        /// </summary>
        DbSet<ModelDeploymentEntity> ModelDeployments { get; }

        /// <summary>
        /// Database set for fallback configurations
        /// </summary>
        DbSet<FallbackConfigurationEntity> FallbackConfigurations { get; }

        /// <summary>
        /// Database set for fallback model mappings
        /// </summary>
        DbSet<FallbackModelMappingEntity> FallbackModelMappings { get; }

        /// <summary>
        /// Database set for provider health records
        /// </summary>
        DbSet<ProviderHealthRecord> ProviderHealthRecords { get; }

        /// <summary>
        /// Database set for provider health configurations
        /// </summary>
        DbSet<ProviderHealthConfiguration> ProviderHealthConfigurations { get; }

        /// <summary>
        /// Database set for IP filters
        /// </summary>
        DbSet<IpFilterEntity> IpFilters { get; }

        /// <summary>
        /// Database set for audio provider configurations
        /// </summary>
        DbSet<AudioProviderConfig> AudioProviderConfigs { get; }

        /// <summary>
        /// Database set for audio costs
        /// </summary>
        DbSet<AudioCost> AudioCosts { get; }

        /// <summary>
        /// Database set for audio usage logs
        /// </summary>
        DbSet<AudioUsageLog> AudioUsageLogs { get; }

        /// <summary>
        /// Flag indicating if this is a test environment
        /// </summary>
        bool IsTestEnvironment { get; set; }

        /// <summary>
        /// Saves changes to the database
        /// </summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}