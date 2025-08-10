using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using ModelProviderMappingEntity = ConduitLLM.Configuration.Entities.ModelProviderMapping;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Configuration
{
    /// <summary>
    /// Database context for ConduitLLM configuration
    /// </summary>
    public class ConduitDbContext : DbContext, IConfigurationDbContext
    {
        /// <summary>
        /// Initializes a new instance of the ConfigurationDbContext
        /// </summary>
        /// <param name="options">The options to be used by the context</param>
        public ConduitDbContext(DbContextOptions<ConduitDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Database set for virtual keys
        /// </summary>
        public virtual DbSet<VirtualKey> VirtualKeys { get; set; } = null!;

        /// <summary>
        /// Database set for virtual key groups
        /// </summary>
        public virtual DbSet<VirtualKeyGroup> VirtualKeyGroups { get; set; } = null!;

        /// <summary>
        /// Database set for virtual key group transactions
        /// </summary>
        public virtual DbSet<VirtualKeyGroupTransaction> VirtualKeyGroupTransactions { get; set; } = null!;

        /// <summary>
        /// Database set for request logs
        /// </summary>
        public virtual DbSet<RequestLog> RequestLogs { get; set; } = null!;

        /// <summary>
        /// Database set for virtual key spend history
        /// </summary>
        public virtual DbSet<VirtualKeySpendHistory> VirtualKeySpendHistory { get; set; } = null!;

        /// <summary>
        /// Database set for virtual key spend history (alias for backward compatibility)
        /// </summary>
        public virtual DbSet<VirtualKeySpendHistory> VirtualKeySpendHistories => VirtualKeySpendHistory;


        /// <summary>
        /// Database set for notifications
        /// </summary>
        public virtual DbSet<Notification> Notifications { get; set; } = null!;

        /// <summary>
        /// Database set for global settings
        /// </summary>
        public virtual DbSet<GlobalSetting> GlobalSettings { get; set; } = null!;

        /// <summary>
        /// Database set for model costs
        /// </summary>
        public virtual DbSet<ModelCost> ModelCosts { get; set; } = null!;

        /// <summary>
        /// Database set for model provider mappings
        /// </summary>
        public virtual DbSet<ModelProviderMappingEntity> ModelProviderMappings { get; set; } = null!;

        /// <summary>
        /// Database set for media records
        /// </summary>
        public virtual DbSet<MediaRecord> MediaRecords { get; set; } = null!;

        /// <summary>
        /// Database set for providers
        /// </summary>
        public virtual DbSet<Provider> Providers { get; set; } = null!;

        /// <summary>
        /// Database set for provider key credentials
        /// </summary>
        public virtual DbSet<ProviderKeyCredential> ProviderKeyCredentials { get; set; } = null!;

        /// <summary>
        /// Database set for router configurations
        /// </summary>
        public virtual DbSet<RouterConfigEntity> RouterConfigurations { get; set; } = null!;

        /// <summary>
        /// Database set for router configurations (alias for backward compatibility)
        /// </summary>
        public virtual DbSet<RouterConfigEntity> RouterConfigs => RouterConfigurations;

        /// <summary>
        /// Database set for model deployments
        /// </summary>
        public virtual DbSet<ModelDeploymentEntity> ModelDeployments { get; set; } = null!;

        /// <summary>
        /// Database set for fallback configurations
        /// </summary>
        public virtual DbSet<FallbackConfigurationEntity> FallbackConfigurations { get; set; } = null!;

        /// <summary>
        /// Database set for fallback model mappings
        /// </summary>
        public virtual DbSet<FallbackModelMappingEntity> FallbackModelMappings { get; set; } = null!;


        /// <summary>
        /// Database set for IP filters
        /// </summary>
        public virtual DbSet<IpFilterEntity> IpFilters { get; set; } = null!;

        /// <summary>
        /// Database set for audio provider configurations
        /// </summary>
        public virtual DbSet<AudioProviderConfig> AudioProviderConfigs { get; set; } = null!;

        /// <summary>
        /// Database set for audio costs
        /// </summary>
        public virtual DbSet<AudioCost> AudioCosts { get; set; } = null!;

        /// <summary>
        /// Database set for audio usage logs
        /// </summary>
        public virtual DbSet<AudioUsageLog> AudioUsageLogs { get; set; } = null!;

        /// <summary>
        /// Database set for model cost mappings
        /// </summary>
        public virtual DbSet<ModelCostMapping> ModelCostMappings { get; set; } = null!;

        /// <summary>
        /// Database set for async tasks
        /// </summary>
        public virtual DbSet<AsyncTask> AsyncTasks { get; set; } = null!;

        /// <summary>
        /// Database set for media lifecycle records
        /// </summary>
        public virtual DbSet<MediaLifecycleRecord> MediaLifecycleRecords { get; set; } = null!;

        /// <summary>
        /// Database set for batch operation history
        /// </summary>
        public virtual DbSet<BatchOperationHistory> BatchOperationHistory { get; set; } = null!;

        /// <summary>
        /// Database set for cache configurations
        /// </summary>
        public virtual DbSet<CacheConfiguration> CacheConfigurations { get; set; } = null!;

        /// <summary>
        /// Database set for cache configuration audit logs
        /// </summary>
        public virtual DbSet<CacheConfigurationAudit> CacheConfigurationAudits { get; set; } = null!;

        public bool IsTestEnvironment { get; set; } = false;

        /// <summary>
        /// Configures the model for the context
        /// </summary>
        /// <param name="modelBuilder">The model builder</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure VirtualKeyGroup entity
            modelBuilder.Entity<VirtualKeyGroup>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ExternalGroupId);
                
                // Configure relationships
                entity.HasMany(e => e.VirtualKeys)
                      .WithOne(e => e.VirtualKeyGroup)
                      .HasForeignKey(e => e.VirtualKeyGroupId)
                      .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasMany(e => e.Transactions)
                      .WithOne(e => e.VirtualKeyGroup)
                      .HasForeignKey(e => e.VirtualKeyGroupId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure VirtualKey entity
            modelBuilder.Entity<VirtualKey>(entity =>
            {
                entity.HasIndex(e => e.KeyHash).IsUnique();

                // Configure navigation property for RequestLogs
                entity.HasMany(e => e.RequestLogs)
                      .WithOne(e => e.VirtualKey)
                      .HasForeignKey(e => e.VirtualKeyId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Configure navigation property for SpendHistory
                entity.HasMany(e => e.SpendHistory)
                      .WithOne(e => e.VirtualKey)
                      .HasForeignKey(e => e.VirtualKeyId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Configure navigation property for Notifications
                entity.HasMany(e => e.Notifications)
                      .WithOne(e => e.VirtualKey)
                      .HasForeignKey(e => e.VirtualKeyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure GlobalSetting entity
            modelBuilder.Entity<GlobalSetting>(entity =>
            {
                entity.HasIndex(e => e.Key).IsUnique();
            });

            // Configure RequestLog entity
            modelBuilder.Entity<RequestLog>(entity =>
            {
                entity.HasOne(e => e.VirtualKey)
                      .WithMany(e => e.RequestLogs)
                      .HasForeignKey(e => e.VirtualKeyId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure ModelCost entity
            modelBuilder.Entity<ModelCost>(entity =>
            {
                entity.HasIndex(e => e.CostName);
                
                // Configure many-to-many relationship through ModelCostMapping
                entity.HasMany(e => e.ModelCostMappings)
                      .WithOne(e => e.ModelCost)
                      .HasForeignKey(e => e.ModelCostId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure ModelCostMapping entity (junction table)
            modelBuilder.Entity<ModelCostMapping>(entity =>
            {
                entity.HasIndex(e => new { e.ModelCostId, e.ModelProviderMappingId })
                      .IsUnique(); // Each model-cost combination should be unique
                
                entity.HasOne(e => e.ModelCost)
                      .WithMany(e => e.ModelCostMappings)
                      .HasForeignKey(e => e.ModelCostId)
                      .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.ModelProviderMapping)
                      .WithMany(e => e.ModelCostMappings)
                      .HasForeignKey(e => e.ModelProviderMappingId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure VirtualKeySpendHistory entity
            modelBuilder.Entity<VirtualKeySpendHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                // Remove redundant relationship configuration as it's already defined by annotations and the VirtualKey configuration
            });

            // Configure Notification entity
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(e => e.Id);
                // Remove redundant relationship configuration as it's already defined by annotations and the VirtualKey configuration
            });

            // Configure Router entities
            modelBuilder.Entity<RouterConfigEntity>(entity =>
            {
                entity.HasIndex(e => e.LastUpdated);

                // Configure relationships with model deployments and fallback configurations
                entity.HasMany(e => e.ModelDeployments)
                      .WithOne(e => e.RouterConfig)
                      .HasForeignKey(e => e.RouterConfigId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.FallbackConfigurations)
                      .WithOne(e => e.RouterConfig)
                      .HasForeignKey(e => e.RouterConfigId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ModelDeploymentEntity>(entity =>
            {
                entity.HasIndex(e => e.ModelName);
                entity.HasIndex(e => e.ProviderId);
                entity.HasIndex(e => e.IsEnabled);
                entity.HasIndex(e => e.IsHealthy);
                
                // Configure relationship with Provider
                entity.HasOne(e => e.Provider)
                      .WithMany()
                      .HasForeignKey(e => e.ProviderId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<FallbackConfigurationEntity>(entity =>
            {
                entity.HasIndex(e => e.PrimaryModelDeploymentId);

                // Configure relationship with fallback model mappings
                entity.HasMany(e => e.FallbackMappings)
                      .WithOne(e => e.FallbackConfiguration)
                      .HasForeignKey(e => e.FallbackConfigurationId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<FallbackModelMappingEntity>(entity =>
            {
                entity.HasIndex(e => new { e.FallbackConfigurationId, e.Order }).IsUnique();
                entity.HasIndex(e => new { e.FallbackConfigurationId, e.ModelDeploymentId }).IsUnique();
            });


            // Configure IP Filter entity
            modelBuilder.Entity<IpFilterEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                // Create a non-unique index on the filter type and IP address/CIDR fields
                entity.HasIndex(e => new { e.FilterType, e.IpAddressOrCidr });
                // Create an index for IsEnabled to quickly filter active rules
                entity.HasIndex(e => e.IsEnabled);
            });

            // Configure AudioProviderConfig entity
            modelBuilder.Entity<AudioProviderConfig>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ProviderId);

                entity.HasOne(e => e.Provider)
                      .WithOne()
                      .HasForeignKey<AudioProviderConfig>(e => e.ProviderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure AudioCost entity
            modelBuilder.Entity<AudioCost>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ProviderId, e.OperationType, e.Model, e.IsActive });
                entity.HasIndex(e => new { e.EffectiveFrom, e.EffectiveTo });
            });

            // Configure AudioUsageLog entity
            modelBuilder.Entity<AudioUsageLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.VirtualKey);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => new { e.ProviderId, e.OperationType });
                entity.HasIndex(e => e.SessionId);
            });

            // Configure AsyncTask entity
            modelBuilder.Entity<AsyncTask>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.VirtualKeyId);
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.State);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.IsArchived);
                entity.HasIndex(e => new { e.VirtualKeyId, e.CreatedAt });
                
                // Composite index for archival queries
                entity.HasIndex(e => new { e.IsArchived, e.CompletedAt, e.State })
                      .HasDatabaseName("IX_AsyncTasks_Archival");
                
                // Index for cleanup queries
                entity.HasIndex(e => new { e.IsArchived, e.ArchivedAt })
                      .HasDatabaseName("IX_AsyncTasks_Cleanup");
                
                entity.HasOne(e => e.VirtualKey)
                      .WithMany()
                      .HasForeignKey(e => e.VirtualKeyId)
                      .OnDelete(DeleteBehavior.Cascade);
                
                // Configure large text fields without specifying provider-specific types
                // EF Core will map these to appropriate text types for each provider
                // By not specifying MaxLength, EF Core treats these as unlimited length text
                entity.Property(e => e.Payload);
                entity.Property(e => e.Result);
                entity.Property(e => e.Error);
                entity.Property(e => e.Metadata);
            });

            // Configure MediaRecord entity
            modelBuilder.Entity<MediaRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.StorageKey).IsUnique();
                entity.HasIndex(e => e.VirtualKeyId);
                entity.HasIndex(e => e.ExpiresAt);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => new { e.VirtualKeyId, e.CreatedAt });
                
                entity.HasOne(e => e.VirtualKey)
                      .WithMany()
                      .HasForeignKey(e => e.VirtualKeyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure MediaLifecycleRecord entity
            modelBuilder.Entity<MediaLifecycleRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.StorageKey).IsUnique();
                entity.HasIndex(e => e.VirtualKeyId);
                entity.HasIndex(e => e.ExpiresAt);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => new { e.VirtualKeyId, e.IsDeleted });
                entity.HasIndex(e => new { e.ExpiresAt, e.IsDeleted });
                
                entity.HasOne(e => e.VirtualKey)
                      .WithMany()
                      .HasForeignKey(e => e.VirtualKeyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure BatchOperationHistory entity
            modelBuilder.Entity<BatchOperationHistory>(entity =>
            {
                entity.HasKey(e => e.OperationId);
                entity.HasIndex(e => e.VirtualKeyId);
                entity.HasIndex(e => e.OperationType);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.StartedAt);
                entity.HasIndex(e => new { e.VirtualKeyId, e.StartedAt });
                entity.HasIndex(e => new { e.OperationType, e.Status, e.StartedAt });
                
                entity.HasOne(e => e.VirtualKey)
                      .WithMany()
                      .HasForeignKey(e => e.VirtualKeyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure CacheConfiguration entity
            modelBuilder.Entity<CacheConfiguration>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Region).IsUnique().HasFilter("\"IsActive\" = true");
                entity.HasIndex(e => new { e.Region, e.IsActive });
                entity.HasIndex(e => e.UpdatedAt);
                entity.Property(e => e.Version).IsConcurrencyToken();
            });

            // Configure CacheConfigurationAudit entity
            modelBuilder.Entity<CacheConfigurationAudit>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Region);
                entity.HasIndex(e => e.ChangedAt);
                entity.HasIndex(e => new { e.Region, e.ChangedAt });
                entity.HasIndex(e => e.ChangedBy);
            });

            // Configure VirtualKeyGroupTransaction entity
            modelBuilder.Entity<VirtualKeyGroupTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.VirtualKeyGroupId);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => new { e.VirtualKeyGroupId, e.CreatedAt });
                entity.HasIndex(e => new { e.IsDeleted, e.CreatedAt });
                entity.HasIndex(e => e.ReferenceType);
                entity.HasIndex(e => e.TransactionType);
                
                // Store enums as integers
                entity.Property(e => e.TransactionType)
                      .HasConversion<int>();
                      
                entity.Property(e => e.ReferenceType)
                      .HasConversion<int>();
            });

            modelBuilder.ApplyConfigurationEntityConfigurations(IsTestEnvironment);

            // Note: ModelProviderMapping and Provider are now included in test environments
            // as they are required by the application code during tests
        }
    }
}
