using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConduitLLM.Configuration.EntityConfigurations
{
    /// <summary>
    /// EF configuration for <see cref="ProviderMetadataDriftItem"/>.
    /// </summary>
    public class ProviderMetadataDriftItemConfiguration : IEntityTypeConfiguration<ProviderMetadataDriftItem>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ProviderMetadataDriftItem> builder)
        {
            builder.ToTable("ProviderMetadataDriftItems");
            builder.HasKey(e => e.Id);

            // Store the drift enums as strings so the partial unique index filter reads naturally
            // ("Status" = 'Pending') and is robust to enum reordering.
            builder.Property(e => e.DriftType).HasConversion<string>().HasMaxLength(30);
            builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

            builder.HasOne(e => e.Mapping)
                .WithMany()
                .HasForeignKey(e => e.ModelProviderMappingId)
                .OnDelete(DeleteBehavior.Cascade);

            // At most one PENDING drift item per (mapping, drift type) — enforces idempotent re-detection.
            builder.HasIndex(e => new { e.ModelProviderMappingId, e.DriftType })
                .IsUnique()
                .HasFilter("\"Status\" = 'Pending'")
                .HasDatabaseName("IX_ProviderMetadataDriftItems_Mapping_Type_Pending");

            builder.HasIndex(e => new { e.Status, e.DriftType })
                .HasDatabaseName("IX_ProviderMetadataDriftItems_Status_Type");

            builder.HasIndex(e => e.ProviderId)
                .HasDatabaseName("IX_ProviderMetadataDriftItems_ProviderId");
        }
    }
}
