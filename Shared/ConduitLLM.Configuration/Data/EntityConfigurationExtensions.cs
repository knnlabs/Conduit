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
                entity.HasIndex(e => new { e.ModelAlias, e.ProviderId }).IsUnique();
                    
                // Configure relationship with Provider
                entity.HasOne(e => e.Provider)
                    .WithMany()
                    .HasForeignKey(e => e.ProviderId)
                    .OnDelete(DeleteBehavior.Restrict);
                    
                // Configure relationship with ModelProviderTypeAssociation
                entity.HasOne(e => e.ModelProviderTypeAssociation)
                    .WithMany()
                    .HasForeignKey(e => e.ModelProviderTypeAssociationId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false); // Nullable during transition
            });

            modelBuilder.Entity<ConduitLLM.Configuration.Entities.ModelRoutePolicy>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ModelAlias).IsUnique();
                entity.ToTable(table =>
                {
                    table.HasCheckConstraint("CK_ModelRoutePolicy_Weights", "\"CostWeight\" >= 0 AND \"SpeedWeight\" >= 0 AND \"QualityWeight\" >= 0");
                    table.HasCheckConstraint("CK_ModelRoutePolicy_Affinity", "\"AffinityTtlSeconds\" > 0 AND \"MaxAffinityScorePenalty\" >= 0");
                });
            });

            // Configure Provider entity
            modelBuilder.Entity<ConduitLLM.Configuration.Entities.Provider>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ProviderType); // Removed .IsUnique() to allow multiple providers of same type
            });

            // Configure ProviderKeyCredential entity
            modelBuilder.Entity<ConduitLLM.Configuration.Entities.ProviderKeyCredential>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                // Foreign key relationship
                entity.HasOne(e => e.Provider)
                    .WithMany(e => e.ProviderKeyCredentials)
                    .HasForeignKey(e => e.ProviderId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                // Index for performance
                entity.HasIndex(e => e.ProviderId)
                    .HasDatabaseName("IX_ProviderKeyCredential_ProviderId");
                
                // Unique constraint: Only one primary key per provider
                entity.HasIndex(e => new { e.ProviderId, e.IsPrimary })
                    .IsUnique()
                    .HasFilter("\"IsPrimary\" = true")
                    .HasDatabaseName("IX_ProviderKeyCredential_OnePrimaryPerProvider");
                
                // Unique constraint: Prevent duplicate API keys for the same provider
                entity.HasIndex(e => new { e.ProviderId, e.ApiKey })
                    .IsUnique()
                    .HasDatabaseName("IX_ProviderKeyCredential_UniqueApiKeyPerProvider")
                    .HasFilter("\"ApiKey\" IS NOT NULL");
                
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

            // Provider-related notification references are nullable so notification history
            // survives provider or key deletion (expand phase of the schema change).
            modelBuilder.Entity<ConduitLLM.Configuration.Entities.Notification>(entity =>
            {
                entity.HasOne(e => e.Provider)
                    .WithMany()
                    .HasForeignKey(e => e.ProviderId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);

                entity.HasOne(e => e.ProviderKeyCredential)
                    .WithMany()
                    .HasForeignKey(e => e.ProviderKeyCredentialId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);
            });


            // Model entity configuration is now handled by ModelEntityConfiguration
            // via modelBuilder.ApplyModelConfigurations() in DbContext

            // Note: Previously ignored entities are now included in test environments
            // as they are required by the application code during tests
        }

    }
}
