using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace ConduitLLM.Tests.TestHelpers
{
    /// <summary>
    /// A custom HttpContent implementation that simulates streaming responses for testing.
    /// Supports Server-Sent Events (SSE) format and configurable delays between chunks.
    /// </summary>
    public class StreamingTestContent : HttpContent
    {
        private readonly IEnumerable<string> _chunks;
        private readonly TimeSpan _delayBetweenChunks;
        private readonly bool _useServerSentEvents;

        /// <summary>
        /// Initializes a new instance of the StreamingTestContent class.
        /// </summary>
        /// <param name="chunks">The chunks of data to stream</param>
        /// <param name="contentType">The content type (e.g., "text/event-stream", "application/vnd.amazon.eventstream")</param>
        /// <param name="delayBetweenChunks">Optional delay between streaming chunks</param>
        /// <param name="useServerSentEvents">Whether to format chunks as Server-Sent Events</param>
        public StreamingTestContent(
            IEnumerable<string> chunks, 
            string contentType = "text/event-stream",
            TimeSpan delayBetweenChunks = default,
            bool useServerSentEvents = true)
        {
            _chunks = chunks ?? throw new ArgumentNullException(nameof(chunks));
            _delayBetweenChunks = delayBetweenChunks;
            _useServerSentEvents = useServerSentEvents;
            
            Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }

        /// <summary>
        /// Serializes the content to the stream asynchronously, simulating a streaming response.
        /// </summary>
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 1024, leaveOpen: true);
            
            foreach (var chunk in _chunks)
            {
                if (_useServerSentEvents)
                {
                    // Format as Server-Sent Event
                    await writer.WriteLineAsync($"data: {chunk}");
                    await writer.WriteLineAsync(); // Empty line to separate events
                }
                else
                {
                    // Raw chunk for AWS event stream (events separated by blank lines)
                    await writer.WriteLineAsync(chunk);
                    await writer.WriteLineAsync(); // Blank line to separate events
                }
                
                await writer.FlushAsync();

                if (_delayBetweenChunks > TimeSpan.Zero)
                {
                    await Task.Delay(_delayBetweenChunks);
                }
            }

            if (_useServerSentEvents)
            {
                // Send final event to indicate stream end
                await writer.WriteLineAsync("data: [DONE]");
                await writer.WriteLineAsync();
                await writer.FlushAsync();
            }
        }

        /// <summary>
        /// Tries to compute the length of the content. Returns false since this is a streaming response.
        /// </summary>
        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false; // Streaming content doesn't have a predetermined length
        }
    }

    /// <summary>
    /// Helper class to create streaming test responses for different providers.
    /// </summary>
    public static class StreamingTestResponseFactory
    {
        /// <summary>
        /// Creates a streaming response for AWS Bedrock format.
        /// </summary>
        public static HttpResponseMessage CreateBedrockStreamingResponse(IEnumerable<string> eventChunks, TimeSpan delay = default)
        {
            var content = new StreamingTestContent(
                eventChunks, 
                contentType: "application/vnd.amazon.eventstream",
                delayBetweenChunks: delay,
                useServerSentEvents: false); // Bedrock uses a different format

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            };
        }
        
        /// <summary>
        /// Formats an event for AWS Bedrock event stream format.
        /// </summary>
        public static string FormatBedrockEvent(string eventType, object data)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            return $":event-type:{eventType}\n:content-type:application/json\n:message-type:event\n{json}";
        }

        /// <summary>
        /// Creates a streaming response for OpenAI/Azure OpenAI format (SSE).
        /// </summary>
        public static HttpResponseMessage CreateOpenAIStreamingResponse(IEnumerable<string> sseChunks, TimeSpan delay = default)
        {
            var content = new StreamingTestContent(
                sseChunks,
                contentType: "text/event-stream",
                delayBetweenChunks: delay,
                useServerSentEvents: true);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            };
        }
    }
}