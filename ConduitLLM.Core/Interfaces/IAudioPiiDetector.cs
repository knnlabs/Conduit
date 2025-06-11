using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for detecting and redacting PII in audio content.
    /// </summary>
    public interface IAudioPiiDetector
    {
        /// <summary>
        /// Detects PII in transcribed text.
        /// </summary>
        /// <param name="text">The text to scan for PII.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>PII detection result.</returns>
        Task<PiiDetectionResult> DetectPiiAsync(
            string text,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Redacts PII from text.
        /// </summary>
        /// <param name="text">The text containing PII.</param>
        /// <param name="detectionResult">The PII detection result.</param>
        /// <param name="redactionOptions">Options for redaction.</param>
        /// <returns>Text with PII redacted.</returns>
        Task<string> RedactPiiAsync(
            string text,
            PiiDetectionResult detectionResult,
            PiiRedactionOptions? redactionOptions = null);
    }

    /// <summary>
    /// Result of PII detection.
    /// </summary>
    public class PiiDetectionResult
    {
        /// <summary>
        /// Gets or sets whether PII was detected.
        /// </summary>
        public bool ContainsPii { get; set; }

        /// <summary>
        /// Gets or sets the detected PII entities.
        /// </summary>
        public List<PiiEntity> Entities { get; set; } = new();

        /// <summary>
        /// Gets or sets the overall risk score.
        /// </summary>
        public double RiskScore { get; set; }
    }

    /// <summary>
    /// Represents a detected PII entity.
    /// </summary>
    public class PiiEntity
    {
        /// <summary>
        /// Gets or sets the type of PII.
        /// </summary>
        public PiiType Type { get; set; }

        /// <summary>
        /// Gets or sets the detected text.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the start position.
        /// </summary>
        public int StartIndex { get; set; }

        /// <summary>
        /// Gets or sets the end position.
        /// </summary>
        public int EndIndex { get; set; }

        /// <summary>
        /// Gets or sets the confidence score.
        /// </summary>
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Types of PII that can be detected.
    /// </summary>
    public enum PiiType
    {
        /// <summary>
        /// Social Security Number.
        /// </summary>
        SSN,

        /// <summary>
        /// Credit card number.
        /// </summary>
        CreditCard,

        /// <summary>
        /// Email address.
        /// </summary>
        Email,

        /// <summary>
        /// Phone number.
        /// </summary>
        Phone,

        /// <summary>
        /// Physical address.
        /// </summary>
        Address,

        /// <summary>
        /// Person name.
        /// </summary>
        Name,

        /// <summary>
        /// Date of birth.
        /// </summary>
        DateOfBirth,

        /// <summary>
        /// Medical record number.
        /// </summary>
        MedicalRecord,

        /// <summary>
        /// Bank account number.
        /// </summary>
        BankAccount,

        /// <summary>
        /// Driver's license number.
        /// </summary>
        DriversLicense,

        /// <summary>
        /// Passport number.
        /// </summary>
        Passport,

        /// <summary>
        /// Other sensitive information.
        /// </summary>
        Other
    }

    /// <summary>
    /// Options for PII redaction.
    /// </summary>
    public class PiiRedactionOptions
    {
        /// <summary>
        /// Gets or sets the redaction method.
        /// </summary>
        public RedactionMethod Method { get; set; } = RedactionMethod.Mask;

        /// <summary>
        /// Gets or sets the mask character.
        /// </summary>
        public char MaskCharacter { get; set; } = '*';

        /// <summary>
        /// Gets or sets whether to preserve length.
        /// </summary>
        public bool PreserveLength { get; set; } = true;

        /// <summary>
        /// Gets or sets custom replacement patterns.
        /// </summary>
        public Dictionary<PiiType, string> CustomReplacements { get; set; } = new();
    }

    /// <summary>
    /// Methods for redacting PII.
    /// </summary>
    public enum RedactionMethod
    {
        /// <summary>
        /// Replace with mask characters.
        /// </summary>
        Mask,

        /// <summary>
        /// Replace with type placeholder.
        /// </summary>
        Placeholder,

        /// <summary>
        /// Remove entirely.
        /// </summary>
        Remove,

        /// <summary>
        /// Use custom replacement.
        /// </summary>
        Custom
    }
}