using System.Diagnostics;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Collects and tracks performance metrics during streaming operations.
    /// </summary>
    public class StreamingMetricsCollector
    {
        private readonly string _requestId;
        private readonly Stopwatch _stopwatch;
        private readonly string _model;
        private readonly string _provider;
        
        private long? _timeToFirstTokenMs;
        private int _tokensGenerated;
        private readonly List<long> _tokenTimestamps;
        private DateTime _lastEmissionTime;
        private readonly TimeSpan _emissionInterval;

        public StreamingMetricsCollector(
            string requestId, 
            string model, 
            string provider,
            TimeSpan? emissionInterval = null)
        {
            _requestId = requestId ?? throw new ArgumentNullException(nameof(requestId));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _stopwatch = Stopwatch.StartNew();
            _tokenTimestamps = new List<long>();
            _lastEmissionTime = DateTime.UtcNow;
            _emissionInterval = emissionInterval ?? TimeSpan.FromSeconds(1);
        }

        /// <summary>
        /// Records that the first token has been received.
        /// </summary>
        public void RecordFirstToken()
        {
            if (!_timeToFirstTokenMs.HasValue)
            {
                _timeToFirstTokenMs = _stopwatch.ElapsedMilliseconds;
                _tokenTimestamps.Add(_timeToFirstTokenMs.Value);
                _tokensGenerated = 1;
            }
        }

        /// <summary>
        /// Records that a token has been generated.
        /// </summary>
        public void RecordToken()
        {
            _tokensGenerated++;
            _tokenTimestamps.Add(_stopwatch.ElapsedMilliseconds);
        }

        /// <summary>
        /// Determines if metrics should be emitted based on the emission interval.
        /// </summary>
        public bool ShouldEmitMetrics()
        {
            var now = DateTime.UtcNow;
            if (now - _lastEmissionTime >= _emissionInterval)
            {
                _lastEmissionTime = now;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the current streaming metrics.
        /// </summary>
        public StreamingMetrics GetMetrics()
        {
            var elapsedMs = _stopwatch.ElapsedMilliseconds;
            var elapsedSeconds = elapsedMs / 1000.0;
            
            var metrics = new StreamingMetrics
            {
                RequestId = _requestId,
                ElapsedMs = elapsedMs,
                TokensGenerated = _tokensGenerated,
                TimeToFirstTokenMs = _timeToFirstTokenMs,
                CurrentTokensPerSecond = elapsedSeconds > 0 ? _tokensGenerated / elapsedSeconds : 0
            };

            // Calculate average inter-token latency
            if (_tokenTimestamps.Count > 1)
            {
                var latencies = new List<long>();
                for (int i = 1; i < _tokenTimestamps.Count; i++)
                {
                    latencies.Add(_tokenTimestamps[i] - _tokenTimestamps[i - 1]);
                }
                metrics.AvgInterTokenLatencyMs = latencies.Average();
            }

            return metrics;
        }

        /// <summary>
        /// Gets the final performance metrics.
        /// </summary>
        public PerformanceMetrics GetFinalMetrics(Usage? usage = null)
        {
            _stopwatch.Stop();
            
            var totalSeconds = _stopwatch.Elapsed.TotalSeconds;
            var metrics = new PerformanceMetrics
            {
                TotalLatencyMs = _stopwatch.ElapsedMilliseconds,
                TimeToFirstTokenMs = _timeToFirstTokenMs,
                Provider = _provider,
                Model = _model,
                Streaming = true
            };

            // Use actual token count from usage if available, otherwise use our count
            var completionTokens = usage?.CompletionTokens ?? _tokensGenerated;
            
            if (totalSeconds > 0 && completionTokens > 0)
            {
                metrics.TokensPerSecond = completionTokens / totalSeconds;
                metrics.CompletionTokensPerSecond = completionTokens / totalSeconds;
            }

            // Calculate prompt tokens per second if we have usage data
            if (usage?.PromptTokens != null && _timeToFirstTokenMs.HasValue)
            {
                var promptProcessingSeconds = _timeToFirstTokenMs.Value / 1000.0;
                if (promptProcessingSeconds > 0)
                {
                    metrics.PromptTokensPerSecond = usage.PromptTokens / promptProcessingSeconds;
                }
            }

            // Calculate average inter-token latency
            if (_tokenTimestamps.Count > 1)
            {
                var latencies = new List<long>();
                for (int i = 1; i < _tokenTimestamps.Count; i++)
                {
                    latencies.Add(_tokenTimestamps[i] - _tokenTimestamps[i - 1]);
                }
                metrics.AvgInterTokenLatencyMs = latencies.Average();
            }

            return metrics;
        }
    }

    /// <summary>
    /// Represents metrics collected during streaming operations.
    /// </summary>
    public class StreamingMetrics
    {
        /// <summary>
        /// Unique identifier for the request.
        /// </summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>
        /// Total elapsed time in milliseconds.
        /// </summary>
        public long ElapsedMs { get; set; }

        /// <summary>
        /// Number of tokens generated so far.
        /// </summary>
        public int TokensGenerated { get; set; }

        /// <summary>
        /// Current tokens per second rate.
        /// </summary>
        public double CurrentTokensPerSecond { get; set; }

        /// <summary>
        /// Time to first token in milliseconds.
        /// </summary>
        public long? TimeToFirstTokenMs { get; set; }

        /// <summary>
        /// Average inter-token latency in milliseconds.
        /// </summary>
        public double? AvgInterTokenLatencyMs { get; set; }
    }
}