using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Utilities
{
    /// <summary>
    /// Helper class for processing streaming responses, particularly server-sent events (SSE)
    /// from LLM provider APIs.
    /// </summary>
    public static class StreamHelper
    {
        private static readonly JsonSerializerOptions DefaultJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Processes a server-sent event (SSE) stream from an HTTP response and yields deserialized objects.
        /// </summary>
        /// <typeparam name="T">The type to deserialize each data event into.</typeparam>
        /// <param name="response">The HTTP response containing the SSE stream.</param>
        /// <param name="logger">Optional logger for errors and debugging.</param>
        /// <param name="options">Optional JSON serialization options.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable of deserialized objects from the stream.</returns>
        public static async IAsyncEnumerable<T> ProcessSseStreamAsync<T>(
            HttpResponseMessage response,
            ILogger? logger = null,
            JsonSerializerOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var jsonOptions = options ?? DefaultJsonOptions;

            // Process the stream outside of any try-catch
            foreach (var data in await ExtractSseDataAsync<T>(response, logger, jsonOptions, cancellationToken))
            {
                yield return data;
            }
        }

        /// <summary>
        /// Extracts and deserializes data from an SSE stream.
        /// </summary>
        private static async Task<List<T>> ExtractSseDataAsync<T>(
            HttpResponseMessage response,
            ILogger? logger,
            JsonSerializerOptions jsonOptions,
            CancellationToken cancellationToken)
        {
            var results = new List<T>();

            try
            {
                logger?.LogDebug("Beginning to process SSE stream");
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                string? line;
                string dataBuffer = string.Empty;
                // SSE event type (only used internally for parsing, not exposed)

                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    line = await reader.ReadLineAsync();

                    if (string.IsNullOrEmpty(line))
                    {
                        // Empty line indicates the end of an event
                        if (!string.IsNullOrEmpty(dataBuffer))
                        {
                            // Process the complete event data
                            if (dataBuffer == "[DONE]")
                            {
                                logger?.LogDebug("Received end of stream marker [DONE]");
                                break;
                            }

                            try
                            {
                                var data = JsonSerializer.Deserialize<T>(dataBuffer, jsonOptions);
                                if (data != null)
                                {
                                    logger?.LogTrace("Adding deserialized stream chunk to results");
                                    results.Add(data);
                                }
                            }
                            catch (JsonException ex)
                            {
                                logger?.LogWarning(ex, "Error deserializing stream chunk: {Data}", dataBuffer);
                            }

                            // Reset for next event
                            dataBuffer = string.Empty;
                        }
                        continue;
                    }

                    // Check for event type
                    if (line.StartsWith("event:"))
                    {
                        // Event line - just continue to the next line
                        continue;
                    }

                    // Process data lines
                    if (line.StartsWith("data:"))
                    {
                        var data = line.Substring(5).TrimStart();
                        dataBuffer = data;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogError(ex, "Error processing SSE stream");
                throw new LLMCommunicationException("Error processing streaming response", ex);
            }

            return results;
        }

        /// <summary>
        /// Processes a server-sent event (SSE) stream specially formatted for LLM chat completion responses.
        /// </summary>
        /// <param name="response">The HTTP response containing the SSE stream.</param>
        /// <param name="logger">Optional logger for errors and debugging.</param>
        /// <param name="options">Optional JSON serialization options.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable of ChatCompletionChunk objects from the stream.</returns>
        public static async IAsyncEnumerable<ChatCompletionChunk> ProcessLlmStreamAsync(
            HttpResponseMessage response,
            ILogger? logger = null,
            JsonSerializerOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var chunk in ProcessSseStreamAsync<ChatCompletionChunk>(
                response, logger, options, cancellationToken))
            {
                yield return chunk;
            }
        }

        /// <summary>
        /// Transforms one async enumerable stream into another by applying a transform function to each item.
        /// </summary>
        /// <typeparam name="TInput">The input stream item type.</typeparam>
        /// <typeparam name="TOutput">The output stream item type.</typeparam>
        /// <param name="source">The source async enumerable to transform.</param>
        /// <param name="transform">The function to apply to each item.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable of transformed items.</returns>
        public static async IAsyncEnumerable<TOutput> TransformStreamAsync<TInput, TOutput>(
            IAsyncEnumerable<TInput> source,
            Func<TInput, TOutput> transform,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                yield return transform(item);
            }
        }

        /// <summary>
        /// Transforms one async enumerable stream into another by applying an async transform function to each item.
        /// </summary>
        /// <typeparam name="TInput">The input stream item type.</typeparam>
        /// <typeparam name="TOutput">The output stream item type.</typeparam>
        /// <param name="source">The source async enumerable to transform.</param>
        /// <param name="transform">The async function to apply to each item.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable of transformed items.</returns>
        public static async IAsyncEnumerable<TOutput> TransformStreamAsync<TInput, TOutput>(
            IAsyncEnumerable<TInput> source,
            Func<TInput, Task<TOutput>> transform,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                yield return await transform(item);
            }
        }

        /// <summary>
        /// Filters an async enumerable stream to include only items that satisfy a predicate.
        /// </summary>
        /// <typeparam name="T">The stream item type.</typeparam>
        /// <param name="source">The source async enumerable to filter.</param>
        /// <param name="predicate">A function to test each element for a condition.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable of filtered items.</returns>
        public static async IAsyncEnumerable<T> FilterStreamAsync<T>(
            IAsyncEnumerable<T> source,
            Func<T, bool> predicate,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                if (predicate(item))
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Merges two async enumerable streams into a single stream, preserving order.
        /// </summary>
        /// <typeparam name="T">The stream item type.</typeparam>
        /// <param name="first">The first async enumerable.</param>
        /// <param name="second">The second async enumerable.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable containing all items from both streams in order.</returns>
        public static async IAsyncEnumerable<T> MergeOrderedStreamsAsync<T>(
            IAsyncEnumerable<T> first,
            IAsyncEnumerable<T> second,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in first.WithCancellation(cancellationToken))
            {
                yield return item;
            }

            await foreach (var item in second.WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }

        /// <summary>
        /// Processes a response stream from a provider that doesn't use standard SSE format.
        /// </summary>
        /// <typeparam name="TRaw">The raw response type from the provider.</typeparam>
        /// <typeparam name="TResult">The standardized result type to convert to.</typeparam>
        /// <param name="response">The HTTP response containing the stream.</param>
        /// <param name="converter">A function to convert from raw format to result format.</param>
        /// <param name="delimiter">Optional line delimiter (defaults to newline).</param>
        /// <param name="logger">Optional logger for errors and debugging.</param>
        /// <param name="options">Optional JSON serialization options.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable of standardized result objects.</returns>
        public static async IAsyncEnumerable<TResult> ProcessCustomStreamAsync<TRaw, TResult>(
            HttpResponseMessage response,
            Func<TRaw, TResult> converter,
            string delimiter = "\n",
            ILogger? logger = null,
            JsonSerializerOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var jsonOptions = options ?? DefaultJsonOptions;

            // Process the stream outside of any try-catch
            foreach (var result in await ExtractCustomStreamDataAsync<TRaw, TResult>(
                response, converter, delimiter, logger, jsonOptions, cancellationToken))
            {
                yield return result;
            }
        }

        /// <summary>
        /// Extracts and converts data from a custom stream format.
        /// </summary>
        private static async Task<List<TResult>> ExtractCustomStreamDataAsync<TRaw, TResult>(
            HttpResponseMessage response,
            Func<TRaw, TResult> converter,
            string delimiter,
            ILogger? logger,
            JsonSerializerOptions jsonOptions,
            CancellationToken cancellationToken)
        {
            var results = new List<TResult>();

            try
            {
                logger?.LogDebug("Beginning to process custom stream with delimiter: {Delimiter}", delimiter);
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                string? line;
                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    try
                    {
                        var rawData = JsonSerializer.Deserialize<TRaw>(line, jsonOptions);
                        if (rawData != null)
                        {
                            logger?.LogTrace("Converting raw stream data to result type");
                            results.Add(converter(rawData));
                        }
                    }
                    catch (JsonException ex)
                    {
                        logger?.LogWarning(ex, "Error deserializing custom stream line: {Line}", line);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogError(ex, "Error processing custom stream");
                throw new LLMCommunicationException("Error processing custom streaming response", ex);
            }

            return results;
        }
    }
}
