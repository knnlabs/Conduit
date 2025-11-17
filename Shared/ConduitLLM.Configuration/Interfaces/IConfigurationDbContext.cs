// Import the model provider mapping from the root namespace
using ConduitLLM.Configuration.Entities;
using ModelProviderMappingEntity = ConduitLLM.Configuration.Entities.ModelProviderMapping;

using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Configuration.Interfaces
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
        /// Database set for virtual key groups
        /// </summary>
        DbSet<VirtualKeyGroup> VirtualKeyGroups { get; }

        /// <summary>
        /// Database set for virtual key group transactions
        /// </summary>
        DbSet<VirtualKeyGroupTransaction> VirtualKeyGroupTransactions { get; }

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
        DbSet<ModelProviderMappingEntity> ModelProviderMappings { get; }

        /// <summary>
        /// Database set for media records
        /// </summary>
        DbSet<MediaRecord> MediaRecords { get; }

        /// <summary>
        /// Database set for media retention policies
        /// </summary>
        DbSet<MediaRetentionPolicy> MediaRetentionPolicies { get; }

        /// <summary>
        /// Database set for providers
        /// </summary>
        DbSet<Provider> Providers { get; }

        /// <summary>
        /// Database set for provider key credentials
        /// </summary>
        DbSet<ProviderKeyCredential> ProviderKeyCredentials { get; }



        /// <summary>
        /// Database set for IP filters
        /// </summary>
        DbSet<IpFilterEntity> IpFilters { get; }


        /// <summary>
        /// Database set for async tasks
        /// </summary>
        DbSet<AsyncTask> AsyncTasks { get; }

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
