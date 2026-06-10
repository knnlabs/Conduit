using System.Text.Json;

using ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.Streaming
{
    /// <summary>
    /// Chunk converter for Groq streaming responses.
    /// </summary>
    /// <remarks>
    /// Groq uses a non-standard location for usage data. Instead of the standard OpenAI
    /// 'usage' field, Groq places usage information in 'x_groq.usage'. This converter
    /// extracts and maps that data to the standard format.
    ///
    /// All other fields follow the standard OpenAI streaming format.
    /// </remarks>
    public sealed class GroqChunkConverter : SseChunkConverterBase, IChunkConverter<JsonElement>
    {
        /// <summary>
        /// Singleton instance for reuse.
        /// </summary>
        public static readonly GroqChunkConverter Instance = new();

        private static readonly JsonSerializerOptions DefaultJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <inheritdoc />
        public ChatCompletionChunk? Convert(JsonElement providerChunk, string modelId)
        {
            try
            {
                // Transform the chunk to extract x_groq.usage into standard usage field
                var transformedJson = ExtractGroqUsageJson(providerChunk);
                var chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(transformedJson, DefaultJsonOptions);

                if (chunk != null && !string.IsNullOrEmpty(modelId))
                {
                    chunk.Model = modelId;
                    chunk.OriginalModelAlias = modelId;
                }

                return chunk;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <inheritdoc />
        public bool IsErrorChunk(JsonElement chunk, out string? errorMessage)
            => IsOpenAIStyleErrorChunk(chunk, out errorMessage);

        /// <inheritdoc />
        public bool IsFinalChunk(JsonElement chunk)
            => IsOpenAIStyleFinalChunk(chunk);

        /// <summary>
        /// Transforms a Groq chunk to extract x_groq.usage into the standard usage field.
        /// Shared by this converter and <c>GroqClient</c>'s raw-chunk transform.
        /// </summary>
        /// <param name="chunk">The original Groq chunk.</param>
        /// <returns>JSON string with usage data in the standard location.</returns>
        public static string ExtractGroqUsageJson(JsonElement chunk)
        {
            // Check if x_groq.usage exists
            if (!chunk.TryGetProperty("x_groq", out var xGroq) ||
                !xGroq.TryGetProperty("usage", out var xGroqUsage))
            {
                // No transformation needed
                return chunk.GetRawText();
            }

            // Create a new JSON object with usage extracted from x_groq
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();

                // Copy all existing properties except x_groq
                foreach (var property in chunk.EnumerateObject())
                {
                    if (property.Name != "x_groq")
                    {
                        property.WriteTo(writer);
                    }
                }

                // Add usage field with data from x_groq.usage
                writer.WritePropertyName("usage");
                writer.WriteStartObject();

                if (xGroqUsage.TryGetProperty("prompt_tokens", out var promptTokens))
                {
                    writer.WriteNumber("prompt_tokens", promptTokens.GetInt32());
                }

                if (xGroqUsage.TryGetProperty("completion_tokens", out var completionTokens))
                {
                    writer.WriteNumber("completion_tokens", completionTokens.GetInt32());
                }

                if (xGroqUsage.TryGetProperty("total_tokens", out var totalTokens))
                {
                    writer.WriteNumber("total_tokens", totalTokens.GetInt32());
                }

                writer.WriteEndObject(); // End usage
                writer.WriteEndObject(); // End root
            }

            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Checks if the chunk contains Groq-specific usage data.
        /// </summary>
        /// <param name="chunk">The chunk to check.</param>
        /// <returns>True if the chunk contains x_groq.usage data.</returns>
        public static bool HasGroqUsage(JsonElement chunk)
        {
            return chunk.TryGetProperty("x_groq", out var xGroq) &&
                   xGroq.TryGetProperty("usage", out _);
        }
    }
}
