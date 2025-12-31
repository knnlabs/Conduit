using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConduitLLM.Core.Converters;

/// <summary>
/// Custom JSON converter that ensures all DateTime values are serialized as UTC with ISO 8601 format.
/// Solves the issue where EF Core loses DateTimeKind metadata when reading from PostgreSQL,
/// causing JavaScript to misinterpret dates as local time.
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    /// <summary>
    /// Reads and parses a DateTime value from JSON, ensuring the result is in UTC.
    /// </summary>
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            throw new JsonException("Cannot convert null to non-nullable DateTime");
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var dateString = reader.GetString();
            if (string.IsNullOrEmpty(dateString))
            {
                throw new JsonException("Cannot parse empty string as DateTime");
            }

            // Parse the datetime string - DateTime.Parse handles ISO 8601 formats
            if (DateTime.TryParse(dateString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedDate))
            {
                // If the parsed date doesn't have UTC kind, convert it to UTC
                return parsedDate.Kind == DateTimeKind.Utc
                    ? parsedDate
                    : DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
            }

            throw new JsonException($"Unable to parse '{dateString}' as DateTime");
        }

        // Fallback: use default DateTime deserialization
        return reader.GetDateTime();
    }

    /// <summary>
    /// Writes a DateTime value to JSON in UTC ISO 8601 format with 'Z' suffix.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Convert to UTC if not already (handles Local and Unspecified kinds)
        var utcValue = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        // Write as ISO 8601 with 'Z' suffix (e.g., "2024-01-15T10:30:00.123Z")
        writer.WriteStringValue(utcValue);
    }
}

/// <summary>
/// Custom JSON converter for nullable DateTime values, ensuring UTC serialization.
/// </summary>
public class NullableUtcDateTimeConverter : JsonConverter<DateTime?>
{
    /// <summary>
    /// Reads and parses a nullable DateTime value from JSON.
    /// </summary>
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var dateString = reader.GetString();
            if (string.IsNullOrEmpty(dateString))
            {
                return null;
            }

            if (DateTime.TryParse(dateString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedDate))
            {
                return parsedDate.Kind == DateTimeKind.Utc
                    ? parsedDate
                    : DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
            }

            throw new JsonException($"Unable to parse '{dateString}' as DateTime");
        }

        return reader.GetDateTime();
    }

    /// <summary>
    /// Writes a nullable DateTime value to JSON in UTC ISO 8601 format.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        var utcValue = value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };

        writer.WriteStringValue(utcValue);
    }
}
