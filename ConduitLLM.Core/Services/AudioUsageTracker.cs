using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service for tracking audio usage and costs across all operations.
    /// </summary>
    public class AudioUsageTracker : IAudioUsageTracker
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AudioUsageTracker> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioUsageTracker"/> class.
        /// </summary>
        public AudioUsageTracker(
            IServiceProvider serviceProvider,
            ILogger<AudioUsageTracker> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task TrackTranscriptionAsync(
            string virtualKey,
            string provider,
            string model,
            double durationSeconds,
            int? characterCount,
            double cost,
            string? sessionId = null,
            string? language = null,
            CancellationToken cancellationToken = default)
        {
            await TrackUsageAsync(
                virtualKey,
                provider,
                "transcription",
                model,
                durationSeconds,
                characterCount,
                null,
                null,
                cost,
                sessionId,
                language,
                null,
                cancellationToken);
        }

        /// <inheritdoc />
        public async Task TrackTextToSpeechAsync(
            string virtualKey,
            string provider,
            string model,
            double durationSeconds,
            int characterCount,
            double cost,
            string? sessionId = null,
            string? voice = null,
            CancellationToken cancellationToken = default)
        {
            await TrackUsageAsync(
                virtualKey,
                provider,
                "text-to-speech",
                model,
                durationSeconds,
                characterCount,
                null,
                null,
                cost,
                sessionId,
                null,
                voice,
                cancellationToken);
        }

        /// <inheritdoc />
        public async Task TrackRealtimeSessionAsync(
            string virtualKey,
            string provider,
            string model,
            double sessionDurationSeconds,
            double inputAudioSeconds,
            double outputAudioSeconds,
            int? inputTokens,
            int? outputTokens,
            double cost,
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            await TrackUsageAsync(
                virtualKey,
                provider,
                "realtime",
                model,
                sessionDurationSeconds,
                null,
                inputTokens,
                outputTokens,
                cost,
                sessionId,
                null,
                null,
                cancellationToken);
        }

        private async Task TrackUsageAsync(
            string virtualKey,
            string provider,
            string operationType,
            string model,
            double durationSeconds,
            int? characterCount,
            int? inputTokens,
            int? outputTokens,
            double cost,
            string? sessionId,
            string? language,
            string? voice,
            CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetService<IAudioUsageLogRepository>();
            
            if (repository == null)
            {
                _logger.LogWarning("Audio usage log repository not available");
                return;
            }

            try
            {
                var log = new AudioUsageLog
                {
                    VirtualKey = virtualKey,
                    Provider = provider,
                    OperationType = operationType,
                    Model = model,
                    RequestId = Guid.NewGuid().ToString(),
                    SessionId = sessionId,
                    DurationSeconds = durationSeconds,
                    CharacterCount = characterCount,
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    Cost = (decimal)cost,
                    Language = language,
                    Voice = voice,
                    StatusCode = 200,
                    Timestamp = DateTime.UtcNow
                };

                await repository.CreateAsync(log);
                
                // Also update virtual key spend
                var virtualKeyRepo = scope.ServiceProvider.GetService<IVirtualKeyRepository>();
                if (virtualKeyRepo != null)
                {
                    await UpdateVirtualKeySpendAsync(virtualKeyRepo, virtualKey, cost);
                }
                
                _logger.LogDebug(
                    "Tracked {OperationType} usage for {VirtualKey}: {Duration}s, ${Cost:F4}",
                    operationType, virtualKey, durationSeconds, cost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to track audio usage");
            }
        }

        private async Task UpdateVirtualKeySpendAsync(
            IVirtualKeyRepository repository,
            string virtualKey,
            double cost)
        {
            try
            {
                var keys = await repository.GetAllAsync();
                var key = keys.FirstOrDefault(k => k.KeyHash == virtualKey);
                
                if (key != null)
                {
                    key.CurrentSpend += (decimal)cost;
                    key.UpdatedAt = DateTime.UtcNow;
                    
                    await repository.UpdateAsync(key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update virtual key spend");
            }
        }
    }

    /// <summary>
    /// Interface for tracking audio usage across all operations.
    /// </summary>
    public interface IAudioUsageTracker
    {
        /// <summary>
        /// Tracks audio transcription usage.
        /// </summary>
        Task TrackTranscriptionAsync(
            string virtualKey,
            string provider,
            string model,
            double durationSeconds,
            int? characterCount,
            double cost,
            string? sessionId = null,
            string? language = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tracks text-to-speech usage.
        /// </summary>
        Task TrackTextToSpeechAsync(
            string virtualKey,
            string provider,
            string model,
            double durationSeconds,
            int characterCount,
            double cost,
            string? sessionId = null,
            string? voice = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tracks real-time session usage.
        /// </summary>
        Task TrackRealtimeSessionAsync(
            string virtualKey,
            string provider,
            string model,
            double sessionDurationSeconds,
            double inputAudioSeconds,
            double outputAudioSeconds,
            int? inputTokens,
            int? outputTokens,
            double cost,
            string sessionId,
            CancellationToken cancellationToken = default);
    }
}