using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Implementation of PII detection and redaction for audio content.
    /// </summary>
    public class AudioPiiDetector : IAudioPiiDetector
    {
        private readonly ILogger<AudioPiiDetector> _logger;
        private readonly IAudioAuditLogger _auditLogger;

        // Regex patterns for common PII types
        private readonly Dictionary<PiiType, string> _piiPatterns = new()
        {
            [PiiType.SSN] = @"\b\d{3}-\d{2}-\d{4}\b|\b\d{9}\b",
            [PiiType.CreditCard] = @"\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b",
            [PiiType.Email] = @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
            [PiiType.Phone] = @"\b(?:\+?1[-.\s]?)?\(?([0-9]{3})\)?[-.\s]?([0-9]{3})[-.\s]?([0-9]{4})\b",
            [PiiType.DateOfBirth] = @"\b(?:0[1-9]|1[0-2])[-/](?:0[1-9]|[12][0-9]|3[01])[-/](?:19|20)\d{2}\b",
            [PiiType.BankAccount] = @"\b\d{8,17}\b",
            [PiiType.DriversLicense] = @"\b[A-Z]{1,2}\d{5,8}\b",
            [PiiType.Passport] = @"\b[A-Z][0-9]{8}\b"
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioPiiDetector"/> class.
        /// </summary>
        public AudioPiiDetector(
            ILogger<AudioPiiDetector> logger,
            IAudioAuditLogger auditLogger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        /// <inheritdoc />
        public async Task<PiiDetectionResult> DetectPiiAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            var result = new PiiDetectionResult();

            if (string.IsNullOrWhiteSpace(text))
            {
                return result;
            }

            var detectedEntities = new List<PiiEntity>();

            // Check for each PII type
            foreach (var (piiType, pattern) in _piiPatterns)
            {
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                var matches = regex.Matches(text);

                foreach (Match match in matches)
                {
                    var entity = new PiiEntity
                    {
                        Type = piiType,
                        Text = match.Value,
                        StartIndex = match.Index,
                        EndIndex = match.Index + match.Length,
                        Confidence = CalculateConfidence(piiType, match.Value)
                    };

                    detectedEntities.Add(entity);
                }
            }

            // Also check for names using simple heuristics
            await DetectNamesAsync(text, detectedEntities);

            // Check for addresses using pattern matching
            DetectAddresses(text, detectedEntities);

            result.Entities = detectedEntities.OrderBy(e => e.StartIndex).ToList();
            result.ContainsPii = detectedEntities.Count() > 0;
            result.RiskScore = CalculateRiskScore(detectedEntities);

            if (result.ContainsPii)
            {
                _logger.LogWarning(
                    "Detected {Count} PII entities with risk score {Score:F2}",
                    result.Entities.Count(),
                    result.RiskScore);
            }

            return result;
        }

        /// <inheritdoc />
        public Task<string> RedactPiiAsync(
            string text,
            PiiDetectionResult detectionResult,
            PiiRedactionOptions? redactionOptions = null)
        {
            if (!detectionResult.ContainsPii || string.IsNullOrWhiteSpace(text))
            {
                return Task.FromResult(text);
            }

            var options = redactionOptions ?? new PiiRedactionOptions();
            var redactedText = text;

            // Process entities in reverse order to maintain indices
            foreach (var entity in detectionResult.Entities.OrderByDescending(e => e.StartIndex))
            {
                var replacement = GetRedactionReplacement(entity, options);

                redactedText = redactedText.Remove(entity.StartIndex, entity.EndIndex - entity.StartIndex);
                redactedText = redactedText.Insert(entity.StartIndex, replacement);
            }

            _logger.LogInformation(
                "Redacted {Count} PII entities using {Method} method",
                detectionResult.Entities.Count(),
                options.Method);

            return Task.FromResult(redactedText);
        }

        private string GetRedactionReplacement(PiiEntity entity, PiiRedactionOptions options)
        {
            switch (options.Method)
            {
                case RedactionMethod.Mask:
                    return options.PreserveLength
                        ? new string(options.MaskCharacter, entity.Text.Length)
                        : "****";

                case RedactionMethod.Placeholder:
                    return $"[{entity.Type}]";

                case RedactionMethod.Remove:
                    return string.Empty;

                case RedactionMethod.Custom:
                    if (options.CustomReplacements.TryGetValue(entity.Type, out var custom))
                        return custom;
                    goto case RedactionMethod.Placeholder;

                default:
                    return "[REDACTED]";
            }
        }

        private async Task DetectNamesAsync(string text, List<PiiEntity> entities)
        {
            // Simple name detection using capitalization patterns
            // In production, use NER (Named Entity Recognition) models
            var namePattern = @"\b[A-Z][a-z]+\s+[A-Z][a-z]+(?:\s+[A-Z][a-z]+)?\b";
            var regex = new Regex(namePattern);
            var matches = regex.Matches(text);

            foreach (Match match in matches)
            {
                // Skip if already detected as another PII type
                if (entities.Any(e => e.StartIndex <= match.Index && e.EndIndex >= match.Index + match.Length))
                    continue;

                // Simple heuristic: check if it looks like a name
                var parts = match.Value.Split(' ');
                if (parts.Length >= 2 && parts.Length <= 4)
                {
                    entities.Add(new PiiEntity
                    {
                        Type = PiiType.Name,
                        Text = match.Value,
                        StartIndex = match.Index,
                        EndIndex = match.Index + match.Length,
                        Confidence = 0.7 // Lower confidence for name detection
                    });
                }
            }

            await Task.CompletedTask;
        }

        private void DetectAddresses(string text, List<PiiEntity> entities)
        {
            // Simple address pattern - in production, use more sophisticated methods
            var addressPattern = @"\b\d+\s+[A-Za-z\s]+(?:Street|St|Avenue|Ave|Road|Rd|Boulevard|Blvd|Lane|Ln|Drive|Dr|Court|Ct|Plaza|Pl)\b";
            var regex = new Regex(addressPattern, RegexOptions.IgnoreCase);
            var matches = regex.Matches(text);

            foreach (Match match in matches)
            {
                entities.Add(new PiiEntity
                {
                    Type = PiiType.Address,
                    Text = match.Value,
                    StartIndex = match.Index,
                    EndIndex = match.Index + match.Length,
                    Confidence = 0.8
                });
            }
        }

        private double CalculateConfidence(PiiType type, string value)
        {
            // More structured patterns have higher confidence
            return type switch
            {
                PiiType.SSN => 0.95,
                PiiType.CreditCard => ValidateCreditCard(value) ? 0.99 : 0.7,
                PiiType.Email => 0.95,
                PiiType.Phone => 0.9,
                PiiType.DateOfBirth => 0.85,
                _ => 0.8
            };
        }

        private bool ValidateCreditCard(string number)
        {
            // Luhn algorithm validation
            var digits = number.Where(char.IsDigit).Select(c => c - '0').ToArray();
            if (digits.Length < 13 || digits.Length > 19)
                return false;

            var sum = 0;
            var alternate = false;

            for (var i = digits.Length - 1; i >= 0; i--)
            {
                var digit = digits[i];
                if (alternate)
                {
                    digit *= 2;
                    if (digit > 9)
                        digit -= 9;
                }
                sum += digit;
                alternate = !alternate;
            }

            return sum % 10 == 0;
        }

        private double CalculateRiskScore(List<PiiEntity> entities)
        {
            if (entities.Count() == 0)
                return 0;

            var highRiskTypes = new[] { PiiType.SSN, PiiType.CreditCard, PiiType.BankAccount, PiiType.MedicalRecord };
            var mediumRiskTypes = new[] { PiiType.DateOfBirth, PiiType.DriversLicense, PiiType.Passport };

            var highRiskCount = entities.Count(e => highRiskTypes.Contains(e.Type));
            var mediumRiskCount = entities.Count(e => mediumRiskTypes.Contains(e.Type));
            var lowRiskCount = entities.Count() - highRiskCount - mediumRiskCount;

            var score = (highRiskCount * 0.4) + (mediumRiskCount * 0.3) + (lowRiskCount * 0.1);
            return Math.Min(1.0, score / entities.Count());
        }
    }
}
