using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Utility classes for the monitoring audio service.
    /// </summary>
    public partial class MonitoringAudioService
    {
    }

    /// <summary>
    /// Monitored duplex stream wrapper.
    /// </summary>
    internal class MonitoredDuplexStream : IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse>
    {
        private readonly IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse> _innerStream;
        private readonly IAudioMetricsCollector _metricsCollector;
        private readonly IAudioTracingService _tracingService;
        private readonly string _virtualKey;
        private readonly string _provider;
        private readonly string _sessionId;
        private readonly IAudioTraceContext _streamTrace;
        private int _framesSent;
        private int _framesReceived;

        public bool IsConnected => _innerStream.IsConnected;

        public MonitoredDuplexStream(
            IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse> innerStream,
            IAudioMetricsCollector metricsCollector,
            IAudioTracingService tracingService,
            string apiKey,
            string provider,
            string sessionId)
        {
            _innerStream = innerStream;
            _metricsCollector = metricsCollector;
            _tracingService = tracingService;
            _virtualKey = apiKey;
            _provider = provider;
            _sessionId = sessionId;

            _streamTrace = _tracingService.StartTrace(
                $"audio.realtime.stream.{sessionId}",
                AudioOperation.Realtime,
                new()
                {
                    ["session.id"] = sessionId,
                    ["virtual_key"] = apiKey,
                    ["provider"] = provider
                });
        }

        public async ValueTask SendAsync(RealtimeAudioFrame item, CancellationToken cancellationToken = default)
        {
            using var span = _tracingService.CreateSpan(_streamTrace, "stream.send");

            try
            {
                await _innerStream.SendAsync(item, cancellationToken);
                _framesSent++;

                span.AddTag("frame.type", item.Type.ToString());
                span.AddTag("frame.size", item.AudioData?.Length.ToString() ?? "0");
                span.SetStatus(TraceStatus.Ok);
            }
            catch (System.Exception ex)
            {
                span.RecordException(ex);
                throw;
            }
        }

        public async IAsyncEnumerable<RealtimeResponse> ReceiveAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var response in _innerStream.ReceiveAsync(cancellationToken))
            {
                _framesReceived++;

                using var span = _tracingService.CreateSpan(_streamTrace, "stream.receive");
                span.AddTag("response.type", response.Type.ToString());
                span.SetStatus(TraceStatus.Ok);

                yield return response;
            }
        }

        public async ValueTask CompleteAsync()
        {
            await _innerStream.CompleteAsync();

            _streamTrace.AddTag("frames.sent", _framesSent.ToString());
            _streamTrace.AddTag("frames.received", _framesReceived.ToString());
            _streamTrace.SetStatus(TraceStatus.Ok);
            _streamTrace.Dispose();
        }
    }
}