using ConduitLLM.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConduitLLM.Configuration.EntityConfigurations
{
    /// <summary>
    /// Entity configuration for BillingAuditEvent
    /// </summary>
    public class BillingAuditEventConfiguration : IEntityTypeConfiguration<BillingAuditEvent>
    {
        /// <summary>
        /// Configures the BillingAuditEvent entity
        /// </summary>
        public void Configure(EntityTypeBuilder<BillingAuditEvent> builder)
        {
            builder.ToTable("BillingAuditEvents");

            builder.HasKey(e => e.Id);

            // Indexes for query performance
            builder.HasIndex(e => e.Timestamp)
                .HasDatabaseName("IX_BillingAuditEvents_Timestamp");

            builder.HasIndex(e => e.VirtualKeyId)
                .HasDatabaseName("IX_BillingAuditEvents_VirtualKeyId");

            builder.HasIndex(e => e.EventType)
                .HasDatabaseName("IX_BillingAuditEvents_EventType");

            builder.HasIndex(e => new { e.VirtualKeyId, e.Timestamp })
                .HasDatabaseName("IX_BillingAuditEvents_VirtualKeyId_Timestamp");

            builder.HasIndex(e => e.RequestId)
                .HasDatabaseName("IX_BillingAuditEvents_RequestId");

            // Composite index for common queries
            builder.HasIndex(e => new { e.EventType, e.Timestamp })
                .HasDatabaseName("IX_BillingAuditEvents_EventType_Timestamp");

            // Property configurations
            builder.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            builder.Property(e => e.Timestamp)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(e => e.EventType)
                .HasConversion<int>()
                .IsRequired();

            // Configure JSONB columns for PostgreSQL
            builder.Property(e => e.UsageJson)
                .HasColumnType("jsonb");

            builder.Property(e => e.MetadataJson)
                .HasColumnType("jsonb");

            // Configure decimal precision
            builder.Property(e => e.CalculatedCost)
                .HasColumnType("decimal(10, 6)");

            // Foreign key relationship
            builder.HasOne(e => e.VirtualKey)
                .WithMany()
                .HasForeignKey(e => e.VirtualKeyId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}