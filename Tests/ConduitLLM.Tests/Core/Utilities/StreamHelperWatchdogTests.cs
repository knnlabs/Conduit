using System.Net;
using System.Text;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Utilities;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Utilities
{
    /// <summary>
    /// Tests for the streaming idle-read watchdog: with HttpClient.Timeout now infinite for
    /// provider clients, a provider that stalls mid-stream must be aborted by the watchdog
    /// rather than hanging the request forever.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Core")]
    public class StreamHelperWatchdogTests : TestBase
    {
        public StreamHelperWatchdogTests(ITestOutputHelper output) : base(output)
        {
        }

        private static HttpResponseMessage SseResponse(Stream stream) => new(HttpStatusCode.OK)
        {
            Content = new PullStreamContent(stream),
        };

        [Fact]
        public async Task HealthyStream_YieldsAllChunks_WatchdogSilent()
        {
            var sse = "data: {\"n\":1}\n\ndata: {\"n\":2}\n\ndata: [DONE]\n\n";
            using var response = SseResponse(new MemoryStream(Encoding.UTF8.GetBytes(sse)));

            var chunks = new List<System.Text.Json.JsonElement>();
            await foreach (var chunk in StreamHelper.ProcessSseStreamAsync<System.Text.Json.JsonElement>(
                response, idleReadTimeout: TimeSpan.FromSeconds(5)))
            {
                chunks.Add(chunk);
            }

            Assert.Equal(2, chunks.Count);
        }

        [Fact]
        public async Task StalledStream_AbortedByWatchdog_WithDistinguishableError()
        {
            using var response = SseResponse(new StallingStream(
                Encoding.UTF8.GetBytes("data: {\"n\":1}\n\n")));

            var chunks = new List<System.Text.Json.JsonElement>();
            var ex = await Assert.ThrowsAsync<LLMCommunicationException>(async () =>
            {
                await foreach (var chunk in StreamHelper.ProcessSseStreamAsync<System.Text.Json.JsonElement>(
                    response, idleReadTimeout: TimeSpan.FromMilliseconds(300)))
                {
                    chunks.Add(chunk);
                }
            });

            Assert.Single(chunks); // the chunk before the stall was delivered
            Assert.Contains("idle timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task StalledStream_CallerCancellation_StillSurfacesAsCancellation()
        {
            using var response = SseResponse(new StallingStream(
                Encoding.UTF8.GetBytes("data: {\"n\":1}\n\n")));
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            // Watchdog is generous (10s); the caller's own cancellation must win and keep its
            // OperationCanceledException identity (not be rewritten as an idle timeout).
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in StreamHelper.ProcessSseStreamAsync<System.Text.Json.JsonElement>(
                    response, idleReadTimeout: TimeSpan.FromSeconds(10), cancellationToken: cts.Token))
                {
                }
            });
        }

        [Fact]
        public async Task SlowButActiveStream_NotAborted_WhenChunksArriveWithinIdleWindow()
        {
            // Three chunks, each delayed 150ms — total stream time (450ms+) exceeds the idle
            // window (300ms), but no single gap does. The watchdog must re-arm per read.
            using var response = SseResponse(new DrippingStream(
                Encoding.UTF8.GetBytes("data: {\"n\":1}\n\ndata: {\"n\":2}\n\ndata: {\"n\":3}\n\n"),
                chunkSize: 16,
                delayPerChunk: TimeSpan.FromMilliseconds(150)));

            var chunks = new List<System.Text.Json.JsonElement>();
            await foreach (var chunk in StreamHelper.ProcessSseStreamAsync<System.Text.Json.JsonElement>(
                response, idleReadTimeout: TimeSpan.FromMilliseconds(300)))
            {
                chunks.Add(chunk);
            }

            Assert.Equal(3, chunks.Count);
        }

        /// <summary>HttpContent exposing a caller-supplied stream directly (no buffering).</summary>
        private sealed class PullStreamContent : HttpContent
        {
            private readonly Stream _stream;

            public PullStreamContent(Stream stream) => _stream = stream;

            protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult(_stream);

            protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
                => _stream.CopyToAsync(stream);

            protected override bool TryComputeLength(out long length)
            {
                length = -1;
                return false;
            }
        }

        /// <summary>Serves its payload, then stalls forever (until cancelled).</summary>
        private sealed class StallingStream : Stream
        {
            private readonly byte[] _payload;
            private int _position;

            public StallingStream(byte[] payload) => _payload = payload;

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (_position < _payload.Length)
                {
                    var count = Math.Min(buffer.Length, _payload.Length - _position);
                    _payload.AsMemory(_position, count).CopyTo(buffer);
                    _position += count;
                    return count;
                }

                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return 0; // unreachable
            }

            public override int Read(byte[] buffer, int offset, int count)
                => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => _position; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        /// <summary>Serves its payload in small delayed chunks, then ends.</summary>
        private sealed class DrippingStream : Stream
        {
            private readonly byte[] _payload;
            private readonly int _chunkSize;
            private readonly TimeSpan _delayPerChunk;
            private int _position;

            public DrippingStream(byte[] payload, int chunkSize, TimeSpan delayPerChunk)
            {
                _payload = payload;
                _chunkSize = chunkSize;
                _delayPerChunk = delayPerChunk;
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (_position >= _payload.Length)
                {
                    return 0;
                }

                await Task.Delay(_delayPerChunk, cancellationToken);
                var count = Math.Min(Math.Min(buffer.Length, _chunkSize), _payload.Length - _position);
                _payload.AsMemory(_position, count).CopyTo(buffer);
                _position += count;
                return count;
            }

            public override int Read(byte[] buffer, int offset, int count)
                => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => _position; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
