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

            // Configure ProviderKeyCredential entity
            modelBuilder.Entity<ConduitLLM.Configuration.Entities.ProviderKeyCredential>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                // Foreign key relationship
                entity.HasOne(e => e.ProviderCredential)
                    .WithMany(e => e.ProviderKeyCredentials)
                    .HasForeignKey(e => e.ProviderCredentialId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                // Index for performance
                entity.HasIndex(e => e.ProviderCredentialId)
                    .HasDatabaseName("IX_ProviderKeyCredential_ProviderCredentialId");
                
                // Unique constraint: Only one primary key per provider
                entity.HasIndex(e => new { e.ProviderCredentialId, e.IsPrimary })
                    .IsUnique()
                    .HasFilter("\"IsPrimary\" = true")
                    .HasDatabaseName("IX_ProviderKeyCredential_OnePrimaryPerProvider");
                
                // Configure check constraints in table configuration
                entity.ToTable(t => {
                    // Check constraint: Primary keys must be enabled
                    t.HasCheckConstraint(
                        "CK_ProviderKeyCredential_PrimaryMustBeEnabled",
                        "\"IsPrimary\" = false OR \"IsEnabled\" = true"
                    );
                    
                    // Check constraint: ProviderAccountGroup range (0-32)
                    t.HasCheckConstraint(
                        "CK_ProviderKeyCredential_AccountGroupRange",
                        "\"ProviderAccountGroup\" >= 0 AND \"ProviderAccountGroup\" <= 32"
                    );
                });
            });

            // Configure Provider Health entities
            modelBuilder.ApplyProviderHealthConfigurations();

            // Note: Previously ignored entities are now included in test environments
            // as they are required by the application code during tests
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
