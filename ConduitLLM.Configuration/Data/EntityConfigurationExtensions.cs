using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Configuration.Data
{
    /// <summary>
    /// Extension methods for configuring entities consistently across the application
    /// </summary>
    public static class EntityConfigurationExtensions
    {
        /// <summary>
        /// Applies all Configuration entity configurations to the model builder
        /// </summary>
        /// <param name="modelBuilder">The model builder to configure</param>
        /// <param name="isTestEnvironment">Whether the current environment is a test environment</param>
        public static void ApplyConfigurationEntityConfigurations(this Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder, bool isTestEnvironment = false)
        {
            // Configure ModelProviderMapping entity
            modelBuilder.Entity<ConduitLLM.Configuration.Entities.ModelProviderMapping>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ModelAlias, e.ProviderCredentialId }).IsUnique();
            });

            // Configure ProviderCredential entity
            modelBuilder.Entity<ConduitLLM.Configuration.Entities.ProviderCredential>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ProviderName).IsUnique();
            });

            // Configure Provider Health entities
            modelBuilder.ApplyProviderHealthConfigurations();

            // Ignore entities in test environments if needed
            if (isTestEnvironment)
            {
                modelBuilder.Ignore<ConduitLLM.Configuration.Entities.ModelProviderMapping>();
                modelBuilder.Ignore<ConduitLLM.Configuration.Entities.ProviderCredential>();
                modelBuilder.Ignore<ProviderHealthRecord>();
                modelBuilder.Ignore<ProviderHealthConfiguration>();
            }
        }

        /// <summary>
        /// Applies configurations specific to provider health entities
        /// </summary>
        /// <param name="modelBuilder">The model builder to configure</param>
        public static void ApplyProviderHealthConfigurations(this Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            // Configure ProviderHealthRecord entity
            modelBuilder.Entity<ProviderHealthRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ProviderName, e.TimestampUtc });
                entity.HasIndex(e => e.IsOnline);
                entity.Property(e => e.StatusMessage).HasMaxLength(500);
                entity.Property(e => e.ErrorCategory).HasMaxLength(50);
                entity.Property(e => e.ErrorDetails).HasMaxLength(2000);
                entity.Property(e => e.EndpointUrl).HasMaxLength(1000);
            });

            // Configure ProviderHealthConfiguration entity
            modelBuilder.Entity<ProviderHealthConfiguration>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ProviderName).IsUnique();
                entity.Property(e => e.CustomEndpointUrl).HasMaxLength(1000);
            });
        }
    }
}
