using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Enhanced monitoring service that adds cost tracking and usage logging.
    /// </summary>
    public class EnhancedMonitoringAudioService : MonitoringAudioService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IAudioCostCalculationService _audioCostCalculator;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnhancedMonitoringAudioService"/> class.
        /// </summary>
        public EnhancedMonitoringAudioService(
            IAudioTranscriptionClient transcriptionClient,
            ITextToSpeechClient ttsClient,
            IRealtimeAudioClient realtimeClient,
            IAudioMetricsCollector metricsCollector,
            IAudioAlertingService alertingService,
            IAudioTracingService tracingService,
            IServiceProvider serviceProvider,
            IAudioCostCalculationService audioCostCalculator,
            ILogger<EnhancedMonitoringAudioService> logger)
            : base(transcriptionClient, ttsClient, realtimeClient, metricsCollector, alertingService, tracingService, logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _audioCostCalculator = audioCostCalculator ?? throw new ArgumentNullException(nameof(audioCostCalculator));
        }

        /// <inheritdoc />
        public override async Task<AudioTranscriptionResponse> TranscribeAudioAsync(
            AudioTranscriptionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            var response = await base.TranscribeAudioAsync(request, apiKey, cancellationToken);

            // Track usage
            await TrackTranscriptionUsageAsync(request, response, apiKey ?? "default");

            return response;
        }

        /// <inheritdoc />
        public override async Task<TextToSpeechResponse> CreateSpeechAsync(
            TextToSpeechRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            var response = await base.CreateSpeechAsync(request, apiKey, cancellationToken);

            // Track usage
            await TrackTextToSpeechUsageAsync(request, response, apiKey ?? "default");

            return response;
        }

        /// <inheritdoc />
        public override async Task CloseSessionAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default)
        {
            await base.CloseSessionAsync(session, cancellationToken);

            // Track session usage
            var virtualKey = session.Metadata?.GetValueOrDefault("VirtualKey")?.ToString() ?? "default";
            await TrackRealtimeSessionUsageAsync(session, virtualKey);
        }

        private async Task TrackTranscriptionUsageAsync(
            AudioTranscriptionRequest request,
            AudioTranscriptionResponse response,
            string virtualKey)
        {
            using var scope = _serviceProvider.CreateScope();
            var usageTracker = scope.ServiceProvider.GetService<IAudioUsageTracker>();

            if (usageTracker == null) return;

            var provider = base._transcriptionClient.GetType().Name.Replace("Client", "");
            var model = request.Model ?? "whisper-1";
            var duration = response.Duration ?? (request.AudioData?.Length ?? 0) / 16000.0; // Estimate

            // Calculate cost
            var costResult = await _audioCostCalculator.CalculateTranscriptionCostAsync(
                provider,
                model,
                duration);
            var cost = costResult.TotalCost;

            await usageTracker.TrackTranscriptionAsync(
                virtualKey,
                provider,
                model,
                duration,
                response.Text?.Length,
                cost,
                sessionId: null,
                language: response.Language);
        }

        private async Task TrackTextToSpeechUsageAsync(
            TextToSpeechRequest request,
            TextToSpeechResponse response,
            string virtualKey)
        {
            using var scope = _serviceProvider.CreateScope();
            var usageTracker = scope.ServiceProvider.GetService<IAudioUsageTracker>();

            if (usageTracker == null) return;

            var provider = base._ttsClient.GetType().Name.Replace("Client", "");
            var model = request.Model ?? "tts-1";
            var duration = response.Duration ?? (response.AudioData?.Length ?? 0) / 24000.0; // Estimate

            // Calculate cost
            var costResult = await _audioCostCalculator.CalculateTextToSpeechCostAsync(
                provider,
                model,
                request.Input.Length);
            var cost = costResult.TotalCost;

            await usageTracker.TrackTextToSpeechAsync(
                virtualKey,
                provider,
                model,
                duration,
                request.Input.Length,
                cost,
                sessionId: null,
                voice: request.Voice);
        }

        private async Task TrackRealtimeSessionUsageAsync(
            RealtimeSession session,
            string virtualKey)
        {
            using var scope = _serviceProvider.CreateScope();
            var usageTracker = scope.ServiceProvider.GetService<IAudioUsageTracker>();

            if (usageTracker == null) return;

            var provider = session.Provider;
            var model = session.Config.Model ?? "gpt-4-realtime";

            // Calculate cost based on audio duration
            var inputMinutes = session.Statistics.InputAudioDuration.TotalMinutes;
            var outputMinutes = session.Statistics.OutputAudioDuration.TotalMinutes;

            var costResult = await _audioCostCalculator.CalculateRealtimeCostAsync(
                provider,
                model,
                inputMinutes * 60,
                outputMinutes * 60,
                session.Statistics?.InputTokens ?? 0,
                session.Statistics?.OutputTokens ?? 0);
            var cost = costResult.TotalCost;

            await usageTracker.TrackRealtimeSessionAsync(
                virtualKey,
                provider,
                model,
                session.Statistics?.Duration.TotalSeconds ?? 0,
                session.Statistics?.InputAudioDuration.TotalSeconds ?? 0,
                session.Statistics?.OutputAudioDuration.TotalSeconds ?? 0,
                session.Statistics?.InputTokens ?? 0,
                session.Statistics?.OutputTokens ?? 0,
                cost,
                session.Id);
        }
    }
}
