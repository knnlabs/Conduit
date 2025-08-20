using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.EntityConfigurations
{
    /// <summary>
    /// Entity Framework configuration for Model-related entities.
    /// Defines indexes and constraints for optimal query performance.
    /// </summary>
    public class ModelEntityConfiguration : IEntityTypeConfiguration<Model>
    {
        public void Configure(EntityTypeBuilder<Model> builder)
        {
            // Index for querying models by series
            builder.HasIndex(e => e.ModelSeriesId)
                .HasDatabaseName("IX_Model_ModelSeriesId");

            // Index for querying models by capabilities
            builder.HasIndex(e => e.ModelCapabilitiesId)
                .HasDatabaseName("IX_Model_ModelCapabilitiesId");
        }
    }

    public class ModelIdentifierEntityConfiguration : IEntityTypeConfiguration<ModelIdentifier>
    {
        public void Configure(EntityTypeBuilder<ModelIdentifier> builder)
        {
            // Unique constraint on provider + identifier combination
            // This ensures no duplicate identifiers within the same provider
            builder.HasIndex(e => new { e.Provider, e.Identifier })
                .IsUnique()
                .HasDatabaseName("IX_ModelIdentifier_Provider_Identifier_Unique");

            // Index for querying identifiers by model
            builder.HasIndex(e => e.ModelId)
                .HasDatabaseName("IX_ModelIdentifier_ModelId");

            // Index for querying by identifier alone (for lookups)
            builder.HasIndex(e => e.Identifier)
                .HasDatabaseName("IX_ModelIdentifier_Identifier");

            // Index for finding primary identifiers
            builder.HasIndex(e => e.IsPrimary)
                .HasDatabaseName("IX_ModelIdentifier_IsPrimary")
                .HasFilter("\"IsPrimary\" = true"); // PostgreSQL syntax
        }
    }

    public class ModelSeriesEntityConfiguration : IEntityTypeConfiguration<ModelSeries>
    {
        public void Configure(EntityTypeBuilder<ModelSeries> builder)
        {
            // Index for querying series by author
            builder.HasIndex(e => e.AuthorId)
                .HasDatabaseName("IX_ModelSeries_AuthorId");

            // Index for querying series by tokenizer type
            builder.HasIndex(e => e.TokenizerType)
                .HasDatabaseName("IX_ModelSeries_TokenizerType");

            // Unique constraint on series name within an author
            builder.HasIndex(e => new { e.AuthorId, e.Name })
                .IsUnique()
                .HasDatabaseName("IX_ModelSeries_AuthorId_Name_Unique");
        }
    }

    public class ModelCapabilitiesEntityConfiguration : IEntityTypeConfiguration<ModelCapabilities>
    {
        public void Configure(EntityTypeBuilder<ModelCapabilities> builder)
        {
            // Index for finding models with specific capabilities
            builder.HasIndex(e => e.SupportsChat)
                .HasDatabaseName("IX_ModelCapabilities_SupportsChat")
                .HasFilter("\"SupportsChat\" = true");

            builder.HasIndex(e => e.SupportsVision)
                .HasDatabaseName("IX_ModelCapabilities_SupportsVision")
                .HasFilter("\"SupportsVision\" = true");

            builder.HasIndex(e => e.SupportsFunctionCalling)
                .HasDatabaseName("IX_ModelCapabilities_SupportsFunctionCalling")
                .HasFilter("\"SupportsFunctionCalling\" = true");

            builder.HasIndex(e => e.SupportsImageGeneration)
                .HasDatabaseName("IX_ModelCapabilities_SupportsImageGeneration")
                .HasFilter("\"SupportsImageGeneration\" = true");

            builder.HasIndex(e => e.SupportsVideoGeneration)
                .HasDatabaseName("IX_ModelCapabilities_SupportsVideoGeneration")
                .HasFilter("\"SupportsVideoGeneration\" = true");

            // Composite index for common capability queries
            builder.HasIndex(e => new { e.SupportsChat, e.SupportsFunctionCalling, e.SupportsStreaming })
                .HasDatabaseName("IX_ModelCapabilities_Chat_Function_Streaming");
        }
    }

    public class ModelProviderMappingEntityConfiguration : IEntityTypeConfiguration<ModelProviderMapping>
    {
        public void Configure(EntityTypeBuilder<ModelProviderMapping> builder)
        {
            // Index for querying mappings by model (now required, no filter needed)
            builder.HasIndex(e => e.ModelId)
                .HasDatabaseName("IX_ModelProviderMapping_ModelId");

            // Index for finding enabled mappings
            builder.HasIndex(e => new { e.ProviderId, e.IsEnabled })
                .HasDatabaseName("IX_ModelProviderMapping_ProviderId_IsEnabled")
                .HasFilter("\"IsEnabled\" = true");

            // Index for quality score queries (for finding best quality providers)
            builder.HasIndex(e => new { e.ModelId, e.QualityScore })
                .HasDatabaseName("IX_ModelProviderMapping_ModelId_QualityScore")
                .HasFilter("\"QualityScore\" IS NOT NULL");

            // Index for capability overrides (to find mappings with custom capabilities)
            builder.HasIndex(e => e.CapabilityOverrides)
                .HasDatabaseName("IX_ModelProviderMapping_CapabilityOverrides")
                .HasFilter("\"CapabilityOverrides\" IS NOT NULL");
        }
    }

    public class ModelAuthorEntityConfiguration : IEntityTypeConfiguration<ModelAuthor>
    {
        public void Configure(EntityTypeBuilder<ModelAuthor> builder)
        {
            // Unique constraint on author name
            builder.HasIndex(e => e.Name)
                .IsUnique()
                .HasDatabaseName("IX_ModelAuthor_Name_Unique");
        }
    }
}