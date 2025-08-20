using System;

using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Core functionality for monitoring audio service wrapper.
    /// </summary>
    public partial class MonitoringAudioService : IAudioTranscriptionClient, ITextToSpeechClient, IRealtimeAudioClient
    {
        protected readonly IAudioTranscriptionClient _transcriptionClient;
        protected readonly ITextToSpeechClient _ttsClient;
        protected readonly IRealtimeAudioClient _realtimeClient;
        protected readonly IAudioMetricsCollector _metricsCollector;
        protected readonly IAudioAlertingService _alertingService;
        protected readonly IAudioTracingService _tracingService;
        protected readonly ILogger<MonitoringAudioService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitoringAudioService"/> class.
        /// </summary>
        public MonitoringAudioService(
            IAudioTranscriptionClient transcriptionClient,
            ITextToSpeechClient ttsClient,
            IRealtimeAudioClient realtimeClient,
            IAudioMetricsCollector metricsCollector,
            IAudioAlertingService alertingService,
            IAudioTracingService tracingService,
            ILogger<MonitoringAudioService> logger)
        {
            _transcriptionClient = transcriptionClient ?? throw new ArgumentNullException(nameof(transcriptionClient));
            _ttsClient = ttsClient ?? throw new ArgumentNullException(nameof(ttsClient));
            _realtimeClient = realtimeClient ?? throw new ArgumentNullException(nameof(realtimeClient));
            _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
            _alertingService = alertingService ?? throw new ArgumentNullException(nameof(alertingService));
            _tracingService = tracingService ?? throw new ArgumentNullException(nameof(tracingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
    }
}