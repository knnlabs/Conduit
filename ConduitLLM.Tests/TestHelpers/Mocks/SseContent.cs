using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ConduitLLM.Tests.TestHelpers.Mocks
{
    // Helper to simulate SSE (Server-Sent Events) streaming content for OpenAI
    public class SseContent : HttpContent
    {
        private readonly IEnumerable<object> _chunks;
        private readonly int _delayMs;
        private readonly bool _throwOnRead;

        public SseContent(IEnumerable<object> chunks, int delayMs = 0, bool throwOnRead = false)
        {
            _chunks = chunks;
            _delayMs = delayMs;
            _throwOnRead = throwOnRead;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            foreach (var chunk in _chunks)
            {
                if (_throwOnRead)
                    throw new IOException("Simulated stream failure");
                var json = System.Text.Json.JsonSerializer.Serialize(chunk);

                // Write the line in the format the StreamHelper expects
                // Note: Each chunk should be a complete SSE message with data: prefix and double newline at the end
                var sseLine = $"data: {json}\n\n";
                var bytes = Encoding.UTF8.GetBytes(sseLine);

                await stream.WriteAsync(bytes, 0, bytes.Length);
                await stream.FlushAsync();
                if (_delayMs > 0)
                    await Task.Delay(_delayMs);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }

        public static SseContent FromChunks(IEnumerable<object> chunks, int delayMs = 0, bool throwOnRead = false)
            => new SseContent(chunks, delayMs, throwOnRead);
    }
}
