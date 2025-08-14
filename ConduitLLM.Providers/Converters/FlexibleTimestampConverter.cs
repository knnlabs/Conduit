using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConduitLLM.Providers.OpenAI
{
    /// <summary>
    /// Converts Unix timestamps from various formats (long, double, string) to long.
    /// Some providers like SambaNova return timestamps as decimals instead of integers.
    /// </summary>
    internal class FlexibleTimestampConverter : JsonConverter<long?>
    {
        public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;
                    
                case JsonTokenType.Number:
                    // Handle both integer and decimal numbers
                    if (reader.TryGetInt64(out var longValue))
                    {
                        return longValue;
                    }
                    else if (reader.TryGetDouble(out var doubleValue))
                    {
                        // Convert decimal to long (truncate decimal part)
                        return (long)doubleValue;
                    }
                    throw new JsonException($"Unable to convert number {reader.GetDouble()} to timestamp");
                    
                case JsonTokenType.String:
                    var stringValue = reader.GetString();
                    if (string.IsNullOrWhiteSpace(stringValue))
                        return null;
                    
                    // Try parsing as long first
                    if (long.TryParse(stringValue, out var parsedLong))
                        return parsedLong;
                    
                    // Try parsing as double and convert
                    if (double.TryParse(stringValue, out var parsedDouble))
                        return (long)parsedDouble;
                    
                    throw new JsonException($"Unable to convert string '{stringValue}' to timestamp");
                    
                default:
                    throw new JsonException($"Unexpected token type {reader.TokenType} for timestamp");
            }
        }

        public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteNumberValue(value.Value);
            else
                writer.WriteNullValue();
        }
    }
}