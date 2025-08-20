using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Realtime audio monitoring functionality for the monitoring audio service.
    /// </summary>
    public partial class MonitoringAudioService
    {
        #region IRealtimeAudioClient Implementation

        /// <inheritdoc />
        public async Task<RealtimeSession> CreateSessionAsync(
            RealtimeSessionConfig config,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            using var trace = _tracingService.StartTrace(
                "audio.realtime.create_session",
                AudioOperation.Realtime,
                new()
                {
                    ["realtime.model"] = config.Model ?? "default",
                    ["realtime.voice"] = config.Voice ?? "default",
                    ["api_key"] = apiKey ?? "default"
                });

            try
            {
                trace.AddEvent("session.create");

                var session = await _realtimeClient.CreateSessionAsync(
                    config, apiKey, cancellationToken);

                // Store virtual key in session metadata
                if (session.Metadata == null)
                {
                    session.Metadata = new Dictionary<string, object>();
                }
                session.Metadata["VirtualKey"] = apiKey ?? "default";

                trace.AddTag("session.id", session.Id);
                trace.SetStatus(TraceStatus.Ok);

                _logger.LogInformation(
                    "Realtime session created: {SessionId} for virtual key: {VirtualKey}",
                    session.Id, apiKey ?? "default");

                return session;
            }
            catch (Exception ex)
            {
                trace.RecordException(ex);

                _logger.LogError(ex,
                    "Failed to create realtime session");

                throw;
            }
        }

        /// <inheritdoc />
        public IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse> StreamAudioAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default)
        {
            var stream = _realtimeClient.StreamAudioAsync(session, cancellationToken);
            var virtualKey = session.Metadata?.GetValueOrDefault("VirtualKey")?.ToString() ?? "default";

            return new MonitoredDuplexStream(
                stream,
                _metricsCollector,
                _tracingService,
                apiKey: virtualKey,
                _realtimeClient.GetType().Name,
                session.Id);
        }

        /// <inheritdoc />
        public Task UpdateSessionAsync(
            RealtimeSession session,
            RealtimeSessionUpdate updates,
            CancellationToken cancellationToken = default)
        {
            return _realtimeClient.UpdateSessionAsync(session, updates, cancellationToken);
        }

        /// <inheritdoc />
        public virtual async Task CloseSessionAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default)
        {
            await _realtimeClient.CloseSessionAsync(session, cancellationToken);

            // Record session completion metrics
            var sessionDuration = (DateTime.UtcNow - session.CreatedAt).TotalSeconds;
            var virtualKey = session.Metadata?.GetValueOrDefault("VirtualKey")?.ToString() ?? "default";

            await _metricsCollector.RecordRealtimeMetricAsync(new RealtimeMetric
            {
                Provider = _realtimeClient.GetType().Name,
                VirtualKey = virtualKey,
                SessionId = session.Id,
                SessionDurationSeconds = sessionDuration,
                Success = true,
                DurationMs = sessionDuration * 1000
            });
        }

        /// <inheritdoc />
        public Task<bool> SupportsRealtimeAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return _realtimeClient.SupportsRealtimeAsync(apiKey, cancellationToken);
        }

        /// <inheritdoc />
        public Task<RealtimeCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
        {
            return _realtimeClient.GetCapabilitiesAsync(cancellationToken);
        }

        #endregion
    }
}