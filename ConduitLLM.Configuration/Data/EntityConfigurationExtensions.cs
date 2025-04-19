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
            
            // Ignore entities in test environments if needed
            if (isTestEnvironment)
            {
                modelBuilder.Ignore<ConduitLLM.Configuration.Entities.ModelProviderMapping>();
                modelBuilder.Ignore<ConduitLLM.Configuration.Entities.ProviderCredential>();
            }
        }
    }
}
