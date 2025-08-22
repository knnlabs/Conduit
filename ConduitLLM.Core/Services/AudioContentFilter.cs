using System.Text.RegularExpressions;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Implementation of audio content filtering.
    /// </summary>
    public class AudioContentFilter : IAudioContentFilter
    {
        private readonly ILogger<AudioContentFilter> _logger;
        private readonly IAudioAuditLogger _auditLogger;

        // Simple patterns for demo - in production, use ML models or external services
        private readonly Dictionary<string, List<string>> _inappropriatePatterns = new()
        {
            ["profanity"] = new() { @"\b(badword1|badword2|badword3)\b" },
            ["violence"] = new() { @"\b(threat|kill|hurt|violence)\b" },
            ["harassment"] = new() { @"\b(harass|bully|intimidate)\b" },
            ["hate_speech"] = new() { @"\b(hate|discriminate)\b" }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioContentFilter"/> class.
        /// </summary>
        public AudioContentFilter(
            ILogger<AudioContentFilter> logger,
            IAudioAuditLogger auditLogger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        /// <inheritdoc />
        public async Task<ContentFilterResult> FilterTranscriptionAsync(
            string text,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var result = await FilterTextInternalAsync(text, AudioOperation.Transcription);

            // Audit the filtering operation
            await _auditLogger.LogContentFilteringAsync(
                new ContentFilterAuditEntry
                {
                    Operation = AudioOperation.Transcription,
                    VirtualKey = virtualKey,
                    WasBlocked = !result.IsApproved,
                    WasModified = result.WasModified,
                    ViolationCategories = result.ViolationCategories,
                    ConfidenceScore = result.ConfidenceScore,
                    Success = true,
                    DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds
                },
                cancellationToken);

            return result;
        }

        /// <inheritdoc />
        public async Task<ContentFilterResult> FilterTextToSpeechAsync(
            string text,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var result = await FilterTextInternalAsync(text, AudioOperation.TextToSpeech);

            // Audit the filtering operation
            await _auditLogger.LogContentFilteringAsync(
                new ContentFilterAuditEntry
                {
                    Operation = AudioOperation.TextToSpeech,
                    VirtualKey = virtualKey,
                    WasBlocked = !result.IsApproved,
                    WasModified = result.WasModified,
                    ViolationCategories = result.ViolationCategories,
                    ConfidenceScore = result.ConfidenceScore,
                    Success = true,
                    DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds
                },
                cancellationToken);

            return result;
        }

        /// <inheritdoc />
        public async Task<bool> ValidateAudioContentAsync(
            byte[] audioData,
            AudioFormat format,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            // In a real implementation, this might:
            // 1. Use speech recognition to convert to text
            // 2. Analyze audio characteristics for inappropriate content
            // 3. Check against audio fingerprinting databases

            _logger.LogDebug(
                "Validating audio content for format {Format}, size {Size} bytes",
                format,
                audioData.Length);

            // For now, just check file size limits
            const int maxSizeMb = 100;
            if (audioData.Length > maxSizeMb * 1024 * 1024)
            {
                _logger.LogWarning("Audio file too large: {Size} MB", audioData.Length / 1024 / 1024);
                return false;
            }

            return await Task.FromResult(true);
        }

        private Task<ContentFilterResult> FilterTextInternalAsync(
            string text,
            AudioOperation operation)
        {
            var result = new ContentFilterResult
            {
                FilteredText = text,
                IsApproved = true,
                WasModified = false,
                ConfidenceScore = 1.0
            };

            if (string.IsNullOrWhiteSpace(text))
            {
                return Task.FromResult(result);
            }

            var filteredText = text;
            var details = new List<ContentFilterDetail>();

            // Check each category of inappropriate content
            foreach (var category in _inappropriatePatterns)
            {
                foreach (var pattern in category.Value)
                {
                    var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                    var matches = regex.Matches(text);

                    foreach (Match match in matches)
                    {
                        result.ViolationCategories.Add(category.Key);

                        var detail = new ContentFilterDetail
                        {
                            Type = category.Key,
                            Severity = DetermineSeverity(category.Key),
                            OriginalText = match.Value,
                            ReplacementText = new string('*', match.Value.Length),
                            StartIndex = match.Index,
                            EndIndex = match.Index + match.Length
                        };

                        details.Add(detail);

                        // Replace with asterisks
                        filteredText = filteredText.Replace(
                            match.Value,
                            detail.ReplacementText,
                            StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            if (details.Count() > 0)
            {
                result.WasModified = true;
                result.FilteredText = filteredText;
                result.Details = details;

                // Determine if content should be blocked entirely
                var criticalViolations = details.Count(d => d.Severity == FilterSeverity.Critical);
                var highViolations = details.Count(d => d.Severity == FilterSeverity.High);

                if (criticalViolations > 0 || highViolations > 2)
                {
                    result.IsApproved = false;
                    result.ConfidenceScore = 0.1;
                }
                else
                {
                    result.ConfidenceScore = 1.0 - (details.Count * 0.1);
                }

                _logger.LogWarning(
                    "Content filtering detected {Count} issues in {Operation}: {Categories}",
                    details.Count,
                    operation,
                    string.Join(", ", result.ViolationCategories.Distinct()));
            }

            return Task.FromResult(result);
        }

        private FilterSeverity DetermineSeverity(string category)
        {
            return category switch
            {
                "profanity" => FilterSeverity.Medium,
                "violence" => FilterSeverity.High,
                "harassment" => FilterSeverity.High,
                "hate_speech" => FilterSeverity.Critical,
                _ => FilterSeverity.Low
            };
        }
    }
}
