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
    public class ConfigurationDbContext : DbContext
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
        public DbSet<VirtualKey> VirtualKeys { get; set; } = null!;

        /// <summary>
        /// Database set for request logs
        /// </summary>
        public DbSet<RequestLog> RequestLogs { get; set; } = null!;

        /// <summary>
        /// Database set for virtual key spend history
        /// </summary>
        public DbSet<VirtualKeySpendHistory> VirtualKeySpendHistory { get; set; } = null!;

        /// <summary>
        /// Database set for notifications
        /// </summary>
        public DbSet<Notification> Notifications { get; set; } = null!;

        /// <summary>
        /// Database set for global settings
        /// </summary>
        public DbSet<GlobalSetting> GlobalSettings { get; set; } = null!;

        /// <summary>
        /// Database set for model costs
        /// </summary>
        public DbSet<ModelCost> ModelCosts { get; set; } = null!;

        /// <summary>
        /// Database set for model provider mappings
        /// </summary>
        public DbSet<ConduitLLM.Configuration.Entities.ModelProviderMapping> ModelProviderMappings { get; set; } = null!;

        /// <summary>
        /// Database set for provider credentials
        /// </summary>
        public DbSet<ConduitLLM.Configuration.Entities.ProviderCredential> ProviderCredentials { get; set; } = null!;

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
                      .WithOne()
                      .HasForeignKey(e => e.VirtualKeyId)
                      .OnDelete(DeleteBehavior.Cascade);
                
                // Configure navigation property for Notifications
                entity.HasMany(e => e.Notifications)
                      .WithOne()
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
                entity.HasOne<VirtualKey>()
                      .WithMany(e => e.SpendHistory)
                      .HasForeignKey(e => e.VirtualKeyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure Notification entity
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne<VirtualKey>()
                      .WithMany(e => e.Notifications)
                      .HasForeignKey(e => e.VirtualKeyId)
                      .OnDelete(DeleteBehavior.Cascade);
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
