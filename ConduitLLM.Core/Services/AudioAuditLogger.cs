using System.Text.Json;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Implementation of audio audit logging.
    /// </summary>
    public class AudioAuditLogger : IAudioAuditLogger
    {
        private readonly ILogger<AudioAuditLogger> _logger;
        private readonly IRequestLogRepository _requestLogRepository;
        private readonly INotificationRepository _notificationRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioAuditLogger"/> class.
        /// </summary>
        public AudioAuditLogger(
            ILogger<AudioAuditLogger> logger,
            IRequestLogRepository requestLogRepository,
            INotificationRepository notificationRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _requestLogRepository = requestLogRepository ?? throw new ArgumentNullException(nameof(requestLogRepository));
            _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        }

        /// <inheritdoc />
        public async Task LogTranscriptionAsync(
            AudioAuditEntry entry,
            CancellationToken cancellationToken = default)
        {
            entry.Operation = AudioOperation.Transcription;
            await LogAudioOperationAsync(entry, cancellationToken);
        }

        /// <inheritdoc />
        public async Task LogTextToSpeechAsync(
            AudioAuditEntry entry,
            CancellationToken cancellationToken = default)
        {
            entry.Operation = AudioOperation.TextToSpeech;
            await LogAudioOperationAsync(entry, cancellationToken);
        }

        /// <inheritdoc />
        public async Task LogRealtimeSessionAsync(
            AudioAuditEntry entry,
            CancellationToken cancellationToken = default)
        {
            entry.Operation = AudioOperation.Realtime;
            await LogAudioOperationAsync(entry, cancellationToken);
        }

        /// <inheritdoc />
        public async Task LogContentFilteringAsync(
            ContentFilterAuditEntry entry,
            CancellationToken cancellationToken = default)
        {
            // Add specific metadata for content filtering
            entry.Metadata["FilterType"] = "Content";
            entry.Metadata["WasBlocked"] = entry.WasBlocked.ToString();
            entry.Metadata["WasModified"] = entry.WasModified.ToString();
            entry.Metadata["ViolationCount"] = entry.ViolationCategories.Count.ToString();

            if (entry.ViolationCategories.Count() > 0)
            {
                entry.Metadata["ViolationCategories"] = string.Join(",", entry.ViolationCategories);
            }

            await LogAudioOperationAsync(entry, cancellationToken);

            // Create notification if content was blocked
            if (entry.WasBlocked)
            {
                await CreateSecurityNotificationAsync(
                    "Content Blocked",
                    $"Audio {entry.Operation} request blocked due to inappropriate content",
                    entry.VirtualKey,
                    cancellationToken);
            }
        }

        /// <inheritdoc />
        public async Task LogPiiDetectionAsync(
            PiiAuditEntry entry,
            CancellationToken cancellationToken = default)
        {
            // Add specific metadata for PII detection
            entry.Metadata["FilterType"] = "PII";
            entry.Metadata["PiiDetected"] = entry.PiiDetected.ToString();
            entry.Metadata["EntityCount"] = entry.EntityCount.ToString();
            entry.Metadata["RiskScore"] = entry.RiskScore.ToString("F2");

            if (entry.PiiTypes.Count() > 0)
            {
                entry.Metadata["PiiTypes"] = string.Join(",", entry.PiiTypes);
            }

            if (entry.WasRedacted)
            {
                entry.Metadata["Redacted"] = "true";
                entry.Metadata["RedactionMethod"] = entry.RedactionMethod?.ToString() ?? "Unknown";
            }

            await LogAudioOperationAsync(entry, cancellationToken);

            // Create notification if high-risk PII was detected
            if (entry.RiskScore > 0.7)
            {
                await CreateSecurityNotificationAsync(
                    "High-Risk PII Detected",
                    $"Audio {entry.Operation} request contained high-risk PII (score: {entry.RiskScore:F2})",
                    entry.VirtualKey,
                    cancellationToken);
            }
        }

        private Task LogAudioOperationAsync(
            AudioAuditEntry entry,
            CancellationToken cancellationToken)
        {
            try
            {
                // For now, just log to the logger
                // In a real implementation, you would create a proper audio audit table
                _logger.LogInformation(
                    "Audio Operation: {Operation} | Key: {VirtualKey} | Provider: {Provider} | Model: {Model} | " +
                    "Duration: {Duration}ms | Success: {Success} | Size: {Size} bytes | Language: {Language}",
                    entry.Operation,
                    entry.VirtualKey,
                    entry.Provider,
                    entry.Model,
                    entry.DurationMs,
                    entry.Success,
                    entry.SizeBytes,
                    entry.Language);

                if (!entry.Success && !string.IsNullOrEmpty(entry.ErrorMessage))
                {
                    _logger.LogError(
                        "Audio operation failed: {ErrorMessage}",
                        entry.ErrorMessage);
                }

                // Log metadata as structured data
                if (entry.Metadata.Count() > 0)
                {
                    _logger.LogDebug(
                        "Audio operation metadata: {Metadata}",
                        JsonSerializer.Serialize(entry.Metadata));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to log audio audit entry {Id}",
                    entry.Id);
            }

            return Task.CompletedTask;
        }

        private async Task CreateSecurityNotificationAsync(
            string title,
            string message,
            string virtualKey,
            CancellationToken cancellationToken)
        {
            try
            {
                // For now, just log the security notification
                // In a real implementation, you would use a proper notification system
                _logger.LogWarningSecure(
                    "SECURITY NOTIFICATION - {Title}: {Message} | VirtualKey: {VirtualKey}",
                    title,
                    message,
                    virtualKey);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogErrorSecure(
                    ex,
                    "Failed to create security notification for key {VirtualKey}",
                    virtualKey);
            }
        }
    }
}
