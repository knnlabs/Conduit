using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration
{
    /// <summary>
    /// Database context for ConduitLLM configuration
    /// </summary>
    public class ConfigurationDbContext : DbContext, IConfigurationDbContext
    {
        /// <summary>
        /// Initializes a new instance of the ConfigurationDbContext
        /// </summary>
        /// <param name="options">The options to be used by the context</param>
        public ConfigurationDbContext(DbContextOptions<ConfigurationDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Database set for virtual keys
        /// </summary>
        public virtual DbSet<VirtualKey> VirtualKeys { get; set; } = null!;

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
        public virtual DbSet<ConduitLLM.Configuration.Entities.ModelProviderMapping> ModelProviderMappings { get; set; } = null!;

        /// <summary>
        /// Database set for provider credentials
        /// </summary>
        public virtual DbSet<ConduitLLM.Configuration.Entities.ProviderCredential> ProviderCredentials { get; set; } = null!;

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

        public bool IsTestEnvironment { get; set; } = false;

        /// <summary>
        /// Configures the model for the context
        /// </summary>
        /// <param name="modelBuilder">The model builder</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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
                entity.HasIndex(e => e.ModelIdPattern)
                      .IsUnique(false); // Patterns might not be unique if we allow overlaps
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
                entity.HasIndex(e => e.ProviderName);
                entity.HasIndex(e => e.IsEnabled);
                entity.HasIndex(e => e.IsHealthy);
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

            modelBuilder.ApplyConfigurationEntityConfigurations(IsTestEnvironment);

            // Only configure ModelProviderMapping in non-test environments or ignore it in test environments
            if (IsTestEnvironment)
            {
                modelBuilder.Ignore<ConduitLLM.Configuration.Entities.ModelProviderMapping>();
                modelBuilder.Ignore<ConduitLLM.Configuration.Entities.ProviderCredential>();
            }
        }
    }
}
