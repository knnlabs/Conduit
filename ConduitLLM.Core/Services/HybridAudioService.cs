using System.Collections.Concurrent;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Implements hybrid audio conversation by chaining STT, LLM, and TTS services.
    /// </summary>
    /// <remarks>
    /// This service provides conversational AI capabilities for providers that don't have
    /// native real-time audio support, by orchestrating a pipeline of separate services.
    /// <para>
    /// This class is split into multiple partial files:
    /// - HybridAudioService.cs: Core functionality, dependencies, and initialization
    /// - HybridAudioService.Processing.cs: Main audio processing operations and availability checks
    /// - HybridAudioService.Sessions.cs: Session management and conversation history
    /// - HybridAudioService.Metrics.cs: Metrics collection and latency monitoring
    /// - HybridAudioServiceStreaming.cs: Streaming audio processing implementation
    /// </para>
    /// </remarks>
    public partial class HybridAudioService : IHybridAudioService
    {
        private readonly ILLMRouter _llmRouter;
        private readonly IAudioRouter _audioRouter;
        private readonly ILogger<HybridAudioService> _logger;
        private readonly ICostCalculationService _costService;
        private readonly IContextManager _contextManager;
        private readonly IAudioProcessingService _audioProcessingService;

        // Session management
        private readonly ConcurrentDictionary<string, HybridSession> _sessions = new();
        private readonly Timer _sessionCleanupTimer;

        // Latency tracking
        private readonly Queue<ProcessingMetrics> _recentMetrics = new();
        private readonly object _metricsLock = new();
        private const int MaxMetricsSamples = 100;

        /// <summary>
        /// Initializes a new instance of the <see cref="HybridAudioService"/> class.
        /// </summary>
        /// <param name="llmRouter">The LLM router for text generation.</param>
        /// <param name="audioRouter">The audio router for STT and TTS.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="costService">The cost calculation service.</param>
        /// <param name="contextManager">The context manager for conversation history.</param>
        /// <param name="audioProcessingService">The audio processing service.</param>
        public HybridAudioService(
            ILLMRouter llmRouter,
            IAudioRouter audioRouter,
            ILogger<HybridAudioService> logger,
            ICostCalculationService costService,
            IContextManager contextManager,
            IAudioProcessingService audioProcessingService)
        {
            _llmRouter = llmRouter ?? throw new ArgumentNullException(nameof(llmRouter));
            _audioRouter = audioRouter ?? throw new ArgumentNullException(nameof(audioRouter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _costService = costService ?? throw new ArgumentNullException(nameof(costService));
            _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
            _audioProcessingService = audioProcessingService ?? throw new ArgumentNullException(nameof(audioProcessingService));

            // Start session cleanup timer
            _sessionCleanupTimer = new Timer(
                CleanupExpiredSessions,
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5));
        }

        // ProcessAudioAsync is implemented in HybridAudioService.Processing.cs
        // StreamProcessAudioAsync is implemented in HybridAudioServiceStreaming.cs
        // Session management methods are implemented in HybridAudioService.Sessions.cs  
        // Metrics and monitoring methods are implemented in HybridAudioService.Metrics.cs
    }
}
