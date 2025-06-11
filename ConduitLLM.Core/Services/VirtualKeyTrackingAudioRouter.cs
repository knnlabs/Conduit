using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Audio router wrapper that ensures virtual key tracking throughout the audio pipeline.
    /// </summary>
    public class VirtualKeyTrackingAudioRouter : IAudioRouter
    {
        private readonly IAudioRouter _innerRouter;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<VirtualKeyTrackingAudioRouter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualKeyTrackingAudioRouter"/> class.
        /// </summary>
        public VirtualKeyTrackingAudioRouter(
            IAudioRouter innerRouter,
            IServiceProvider serviceProvider,
            ILogger<VirtualKeyTrackingAudioRouter> logger)
        {
            _innerRouter = innerRouter ?? throw new ArgumentNullException(nameof(innerRouter));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<IAudioTranscriptionClient?> GetTranscriptionClientAsync(
            AudioTranscriptionRequest request,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            var client = await _innerRouter.GetTranscriptionClientAsync(request, virtualKey, cancellationToken);

            // Store virtual key in request metadata if available
            if (client != null && request.ProviderOptions != null)
            {
                request.ProviderOptions["_virtualKey"] = virtualKey;
            }

            return client;
        }

        /// <inheritdoc />
        public async Task<ITextToSpeechClient?> GetTextToSpeechClientAsync(
            TextToSpeechRequest request,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            var client = await _innerRouter.GetTextToSpeechClientAsync(request, virtualKey, cancellationToken);

            // Store virtual key in request metadata if available
            if (client != null && request.ProviderOptions != null)
            {
                request.ProviderOptions["_virtualKey"] = virtualKey;
            }

            return client;
        }

        /// <inheritdoc />
        public async Task<IRealtimeAudioClient?> GetRealtimeClientAsync(
            RealtimeSessionConfig config,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            var client = await _innerRouter.GetRealtimeClientAsync(config, virtualKey, cancellationToken);

            if (client != null)
            {
                // Wrap the client to ensure virtual key tracking
                return new VirtualKeyTrackingRealtimeClient(client, virtualKey, _serviceProvider, _logger);
            }

            return client;
        }

        /// <inheritdoc />
        public Task<List<string>> GetAvailableTranscriptionProvidersAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            return _innerRouter.GetAvailableTranscriptionProvidersAsync(virtualKey, cancellationToken);
        }

        /// <inheritdoc />
        public Task<List<string>> GetAvailableTextToSpeechProvidersAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            return _innerRouter.GetAvailableTextToSpeechProvidersAsync(virtualKey, cancellationToken);
        }

        /// <inheritdoc />
        public Task<List<string>> GetAvailableRealtimeProvidersAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            return _innerRouter.GetAvailableRealtimeProvidersAsync(virtualKey, cancellationToken);
        }

        /// <inheritdoc />
        public bool ValidateAudioOperation(
            AudioOperation operation,
            string provider,
            AudioRequestBase request,
            out string errorMessage)
        {
            return _innerRouter.ValidateAudioOperation(operation, provider, request, out errorMessage);
        }

        /// <inheritdoc />
        public Task<AudioRoutingStatistics> GetRoutingStatisticsAsync(
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            return _innerRouter.GetRoutingStatisticsAsync(virtualKey, cancellationToken);
        }
    }

    /// <summary>
    /// Wrapper for real-time audio client that ensures virtual key is tracked in sessions.
    /// </summary>
    internal class VirtualKeyTrackingRealtimeClient : IRealtimeAudioClient
    {
        private readonly IRealtimeAudioClient _innerClient;
        private readonly string _virtualKey;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        public VirtualKeyTrackingRealtimeClient(
            IRealtimeAudioClient innerClient,
            string virtualKey,
            IServiceProvider serviceProvider,
            ILogger logger)
        {
            _innerClient = innerClient;
            _virtualKey = virtualKey;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<RealtimeSession> CreateSessionAsync(
            RealtimeSessionConfig config,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            var session = await _innerClient.CreateSessionAsync(config, apiKey ?? _virtualKey, cancellationToken);

            // Ensure virtual key is stored in metadata
            if (session.Metadata == null)
            {
                session.Metadata = new Dictionary<string, object>();
            }
            session.Metadata["VirtualKey"] = apiKey ?? _virtualKey;

            // Store session in session store if available
            using var scope = _serviceProvider.CreateScope();
            var sessionStore = scope.ServiceProvider.GetService<IRealtimeSessionStore>();
            if (sessionStore != null)
            {
                await sessionStore.StoreSessionAsync(session, cancellationToken: cancellationToken);
                _logger.LogDebug("Stored realtime session {SessionId} for virtual key {VirtualKey}",
                    session.Id, apiKey ?? _virtualKey);
            }

            return session;
        }

        public IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse> StreamAudioAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default)
        {
            return _innerClient.StreamAudioAsync(session, cancellationToken);
        }

        public Task UpdateSessionAsync(
            RealtimeSession session,
            RealtimeSessionUpdate updates,
            CancellationToken cancellationToken = default)
        {
            return _innerClient.UpdateSessionAsync(session, updates, cancellationToken);
        }

        public async Task CloseSessionAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default)
        {
            await _innerClient.CloseSessionAsync(session, cancellationToken);

            // Update session in store
            using var scope = _serviceProvider.CreateScope();
            var sessionStore = scope.ServiceProvider.GetService<IRealtimeSessionStore>();
            if (sessionStore != null)
            {
                session.State = SessionState.Closed;
                await sessionStore.UpdateSessionAsync(session, cancellationToken);
            }
        }

        public Task<bool> SupportsRealtimeAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return _innerClient.SupportsRealtimeAsync(apiKey ?? _virtualKey, cancellationToken);
        }

        public Task<RealtimeCapabilities> GetCapabilitiesAsync(
            CancellationToken cancellationToken = default)
        {
            return _innerClient.GetCapabilitiesAsync(cancellationToken);
        }
    }
}
