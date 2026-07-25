using System.Text.Json;

using ConduitLLM.Configuration.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ConduitLLM.Configuration.EntityConfigurations
{
    /// <summary>
    /// EF Core configuration for <see cref="Provider"/>. Maps the structured <see cref="Provider.Settings"/>
    /// dictionary to a PostgreSQL <c>jsonb</c> column with a value converter and comparer so change
    /// tracking works on the mutable dictionary.
    /// </summary>
    public class ProviderEntityConfiguration : IEntityTypeConfiguration<Provider>
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<Provider> builder)
        {
            var converter = new ValueConverter<Dictionary<string, string>?, string?>(
                value => value == null ? null : JsonSerializer.Serialize(value, SerializerOptions),
                json => string.IsNullOrWhiteSpace(json)
                    ? null
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(json, SerializerOptions));

            var comparer = new ValueComparer<Dictionary<string, string>?>(
                (left, right) => JsonSerializer.Serialize(left, SerializerOptions)
                    == JsonSerializer.Serialize(right, SerializerOptions),
                value => value == null ? 0 : JsonSerializer.Serialize(value, SerializerOptions).GetHashCode(),
                value => value == null ? null : new Dictionary<string, string>(value));

            builder.Property(p => p.Settings)
                .HasColumnName("Settings")
                .HasColumnType("jsonb")
                .HasConversion(converter, comparer);
        }
    }

    /// <summary>
    /// EF Core configuration for <see cref="ProviderKeyCredential"/>. Maps the encrypted
    /// <see cref="ProviderKeyCredential.SecretSettings"/> dictionary to a PostgreSQL <c>jsonb</c>
    /// column with the same converter and comparer treatment as the provider settings bag.
    /// </summary>
    public class ProviderKeyCredentialEntityConfiguration : IEntityTypeConfiguration<ProviderKeyCredential>
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ProviderKeyCredential> builder)
        {
            var converter = new ValueConverter<Dictionary<string, string>?, string?>(
                value => value == null ? null : JsonSerializer.Serialize(value, SerializerOptions),
                json => string.IsNullOrWhiteSpace(json)
                    ? null
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(json, SerializerOptions));

            var comparer = new ValueComparer<Dictionary<string, string>?>(
                (left, right) => JsonSerializer.Serialize(left, SerializerOptions)
                    == JsonSerializer.Serialize(right, SerializerOptions),
                value => value == null ? 0 : JsonSerializer.Serialize(value, SerializerOptions).GetHashCode(),
                value => value == null ? null : new Dictionary<string, string>(value));

            builder.Property(p => p.SecretSettings)
                .HasColumnName("SecretSettings")
                .HasColumnType("jsonb")
                .HasConversion(converter, comparer);
        }
    }
}
