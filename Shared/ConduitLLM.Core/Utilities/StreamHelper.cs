using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

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
        /// Idle-read watchdog applied when a caller does not pass an explicit timeout: if no
        /// data arrives on a streaming response for this long, the stream is aborted with a
        /// distinguishable <see cref="LLMCommunicationException"/> instead of hanging forever.
        /// Initialized from <c>Conduit:ProviderHttp:Streaming:IdleReadTimeoutSeconds</c> when
        /// the provider HTTP clients are registered.
        /// </summary>
        public static TimeSpan DefaultIdleReadTimeout { get; set; } = TimeSpan.FromSeconds(90);

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
            TimeSpan? idleReadTimeout = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var jsonOptions = options ?? DefaultJsonOptions;

            await foreach (var dataBuffer in ReadSseDataLinesAsync(
                response, logger, idleReadTimeout ?? DefaultIdleReadTimeout, cancellationToken))
            {
                T? data = default;
                try
                {
                    data = JsonSerializer.Deserialize<T>(dataBuffer, jsonOptions);
                }
                catch (JsonException ex)
                {
                    logger?.LogWarning(ex, "Error deserializing stream chunk: {Data}", dataBuffer);
                }

                if (data != null)
                {
                    logger?.LogTrace("Yielding deserialized stream chunk");
                    yield return data;
                }
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
                await foreach (var dataBuffer in ReadSseDataLinesAsync(
                    response, logger, DefaultIdleReadTimeout, cancellationToken))
                {
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
        /// Reads raw SSE data lines from an HTTP response stream, yielding each complete
        /// event's data payload as a string. Handles SSE framing (event:/data: prefixes,
        /// empty-line delimiters, and the [DONE] sentinel) so callers only receive
        /// deserialization-ready JSON strings.
        /// </summary>
        /// <param name="response">The HTTP response containing the SSE stream.</param>
        /// <param name="logger">Optional logger for debugging.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>An async enumerable of raw JSON data strings from SSE events.</returns>
        private static async IAsyncEnumerable<string> ReadSseDataLinesAsync(
            HttpResponseMessage response,
            ILogger? logger,
            TimeSpan idleReadTimeout,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            logger?.LogDebug("Beginning to process SSE stream");
            logger?.LogDebug("Response headers: {Headers}", response.Headers.ToString());
            logger?.LogDebug("Content headers: {ContentHeaders}", response.Content.Headers.ToString());

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            string? line;
            string dataBuffer = string.Empty;
            int lineCount = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                line = await ReadLineWithIdleWatchdogAsync(reader, idleCts, idleReadTimeout, cancellationToken, logger);
                if (line == null) break; // End of stream
                lineCount++;

                // Log first few lines for debugging
                if (lineCount <= 5)
                {
                    logger?.LogDebug("SSE line {LineNumber}: '{Line}'", lineCount, line);
                }

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

                        yield return dataBuffer;

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

        /// <summary>
        /// Reads one line from a streaming response, aborting with a distinguishable
        /// <see cref="LLMCommunicationException"/> if no data arrives within the idle timeout.
        /// Providers that stall mid-stream would otherwise hang the request forever now that
        /// streaming clients no longer carry an HttpClient.Timeout.
        /// </summary>
        private static async Task<string?> ReadLineWithIdleWatchdogAsync(
            StreamReader reader,
            CancellationTokenSource idleCts,
            TimeSpan idleReadTimeout,
            CancellationToken callerToken,
            ILogger? logger)
        {
            idleCts.CancelAfter(idleReadTimeout);
            try
            {
                var line = await reader.ReadLineAsync(idleCts.Token);
                // Disarm the watchdog while the caller processes the line
                idleCts.CancelAfter(Timeout.InfiniteTimeSpan);
                return line;
            }
            catch (OperationCanceledException ex) when (!callerToken.IsCancellationRequested)
            {
                logger?.LogWarning(
                    "Streaming response idle timeout: no data received for {IdleTimeoutSeconds}s",
                    idleReadTimeout.TotalSeconds);
                throw new LLMCommunicationException(
                    $"Streaming response idle timeout: no data received for {idleReadTimeout.TotalSeconds:F0}s",
                    ex);
            }
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
                response, logger, options, cancellationToken: cancellationToken))
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
                using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                string? line;
                while (!cancellationToken.IsCancellationRequested)
                {
                    line = await ReadLineWithIdleWatchdogAsync(
                        reader, idleCts, DefaultIdleReadTimeout, cancellationToken, logger);
                    if (line == null) break; // End of stream
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
            catch (OperationCanceledException)
            {
                logger?.LogDebug("Stream processing was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error processing custom stream");
                throw new LLMCommunicationException("Error processing custom streaming response", ex);
            }

            return results;
        }
    }
}
