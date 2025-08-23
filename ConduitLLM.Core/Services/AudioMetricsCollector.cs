using System.Collections.Concurrent;

using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Collects and aggregates audio operation metrics.
    /// </summary>
    public partial class AudioMetricsCollector : IAudioMetricsCollector
    {
        private readonly ILogger<AudioMetricsCollector> _logger;
        private readonly AudioMetricsOptions _options;
        private readonly ConcurrentDictionary<string, MetricsBucket> _metricsBuckets = new();
        private readonly ReaderWriterLockSlim _aggregationLock = new();
        private readonly Timer _aggregationTimer;
        private readonly IAudioAlertingService? _alertingService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioMetricsCollector"/> class.
        /// </summary>
        public AudioMetricsCollector(
            ILogger<AudioMetricsCollector> logger,
            IOptions<AudioMetricsOptions> options,
            IAudioAlertingService? alertingService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _alertingService = alertingService;

            // Start aggregation timer
            _aggregationTimer = new Timer(
                AggregateMetrics,
                null,
                _options.AggregationInterval,
                _options.AggregationInterval);
        }

        public void Dispose()
        {
            try
            {
                _aggregationTimer?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing aggregation timer");
            }

            _aggregationLock?.Dispose();
        }
    }
}