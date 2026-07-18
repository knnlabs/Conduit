using System.Text.Json;

using ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.Streaming
{
    /// <summary>
    /// Chunk converter for OpenAI-compatible streaming responses.
    /// </summary>
    /// <remarks>
    /// This converter handles the standard OpenAI streaming format used by most
    /// OpenAI-compatible providers including OpenAI, Fireworks, DeepInfra, Cerebras, and SambaNova.
    ///
    /// The OpenAI streaming format closely matches the Core ChatCompletionChunk model,
    /// so this converter primarily does direct deserialization with minimal transformation.
    /// </remarks>
    public sealed class OpenAIChunkConverter : SseChunkConverterBase, IChunkConverter<JsonElement>
    {
        /// <summary>
        /// Singleton instance for reuse.
        /// </summary>
        public static readonly OpenAIChunkConverter Instance = new();

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
                var chunkJson = providerChunk.GetRawText();
                var chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(chunkJson, DefaultJsonOptions);

                if (chunk != null && !string.IsNullOrEmpty(modelId))
                {
                    // Preserve the original model alias
                    chunk.Model = modelId;
                    chunk.OriginalModelAlias = modelId;
                }

                return chunk;
            }
            catch (JsonException)
            {
                // If deserialization fails, return null to skip this chunk
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
        /// Parses a raw SSE line and converts it to a ChatCompletionChunk.
        /// </summary>
        /// <param name="line">The SSE data line (without the "data: " prefix).</param>
        /// <param name="modelId">The model ID to set on the chunk.</param>
        /// <returns>The converted chunk, or null if the line should be skipped.</returns>
        public ChatCompletionChunk? ParseSseLine(string line, string modelId)
        {
            if (string.IsNullOrWhiteSpace(line) || IsDoneMarker(line))
            {
                return null;
            }

            try
            {
                var jsonElement = JsonDocument.Parse(line).RootElement;
                return Convert(jsonElement, modelId);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
