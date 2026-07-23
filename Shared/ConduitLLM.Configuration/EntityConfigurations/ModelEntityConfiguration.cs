using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Models;

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
            builder.Property(e => e.CapabilitySource)
                .HasDefaultValue(ModelCapabilitySource.LegacyInferred);

            // Index for querying models by series
            builder.HasIndex(e => e.ModelSeriesId)
                .HasDatabaseName("IX_Model_ModelSeriesId");
        }
    }

    public class ModelProviderTypeAssociationEntityConfiguration : IEntityTypeConfiguration<ModelProviderTypeAssociation>
    {
        public void Configure(EntityTypeBuilder<ModelProviderTypeAssociation> builder)
        {
            // Table name to maintain backward compatibility with database
            builder.ToTable("ModelIdentifiers");

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

    public class ModelProviderMappingEntityConfiguration : IEntityTypeConfiguration<ModelProviderMapping>
    {
        public void Configure(EntityTypeBuilder<ModelProviderMapping> builder)
        {
            // Index for querying mappings by model provider type association
            builder.HasIndex(e => e.ModelProviderTypeAssociationId)
                .HasDatabaseName("IX_ModelProviderMapping_ModelProviderTypeAssociationId");

            // Index for finding enabled mappings
            builder.HasIndex(e => new { e.ProviderId, e.IsEnabled })
                .HasDatabaseName("IX_ModelProviderMapping_ProviderId_IsEnabled")
                .HasFilter("\"IsEnabled\" = true");
                
            // Index for ModelProviderTypeAssociationId for performance
            builder.HasIndex(e => e.ModelProviderTypeAssociationId)
                .HasDatabaseName("IX_ModelProviderMapping_ModelProviderTypeAssociationId");
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
