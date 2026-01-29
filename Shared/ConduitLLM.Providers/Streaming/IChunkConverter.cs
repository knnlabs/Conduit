using ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.Streaming
{
    /// <summary>
    /// Defines the contract for converting provider-specific streaming chunks
    /// to the standardized ChatCompletionChunk format.
    /// </summary>
    /// <typeparam name="TProviderChunk">The provider-specific chunk type.</typeparam>
    /// <remarks>
    /// Different providers return streaming chunks in different formats.
    /// This interface allows providers to implement their own conversion logic
    /// while maintaining a consistent streaming interface.
    /// </remarks>
    public interface IChunkConverter<TProviderChunk>
    {
        /// <summary>
        /// Converts a provider-specific chunk to the standardized format.
        /// </summary>
        /// <param name="providerChunk">The provider-specific chunk to convert.</param>
        /// <param name="modelId">The model ID for the response.</param>
        /// <returns>The converted chunk, or null if the chunk should be skipped.</returns>
        ChatCompletionChunk? Convert(TProviderChunk providerChunk, string modelId);

        /// <summary>
        /// Checks if the provider chunk represents an error.
        /// </summary>
        /// <param name="chunk">The provider chunk to check.</param>
        /// <param name="errorMessage">The error message if this is an error chunk.</param>
        /// <returns>True if this is an error chunk, false otherwise.</returns>
        bool IsErrorChunk(TProviderChunk chunk, out string? errorMessage);

        /// <summary>
        /// Checks if the provider chunk is the final chunk in the stream.
        /// </summary>
        /// <param name="chunk">The provider chunk to check.</param>
        /// <returns>True if this is the final chunk, false otherwise.</returns>
        bool IsFinalChunk(TProviderChunk chunk);
    }

    /// <summary>
    /// Base class for SSE (Server-Sent Events) line parsing.
    /// </summary>
    public abstract class SseChunkConverterBase
    {
        /// <summary>
        /// The SSE data prefix.
        /// </summary>
        protected const string DataPrefix = "data: ";

        /// <summary>
        /// The SSE done marker for OpenAI-compatible APIs.
        /// </summary>
        protected const string DoneMarker = "[DONE]";

        /// <summary>
        /// Checks if a line is an SSE data line.
        /// </summary>
        /// <param name="line">The line to check.</param>
        /// <returns>True if the line starts with "data: ", false otherwise.</returns>
        protected static bool IsDataLine(string line)
        {
            return line.StartsWith(DataPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Extracts the data content from an SSE data line.
        /// </summary>
        /// <param name="line">The SSE line.</param>
        /// <returns>The data content without the "data: " prefix.</returns>
        protected static string ExtractData(string line)
        {
            return line.Substring(DataPrefix.Length);
        }

        /// <summary>
        /// Checks if the data content is the done marker.
        /// </summary>
        /// <param name="data">The data content to check.</param>
        /// <returns>True if this is the done marker, false otherwise.</returns>
        protected static bool IsDoneMarker(string data)
        {
            return data.Equals(DoneMarker, StringComparison.Ordinal);
        }
    }
}
