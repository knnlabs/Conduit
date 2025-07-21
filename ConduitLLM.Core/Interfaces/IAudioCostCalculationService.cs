using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for audio cost calculation service.
    /// </summary>
    public interface IAudioCostCalculationService
    {
        /// <summary>
        /// Calculates the cost of audio transcription.
        /// </summary>
        Task<AudioCostResult> CalculateTranscriptionCostAsync(
            string provider,
            string model,
            double durationSeconds,
            string? virtualKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates the cost of text-to-speech.
        /// </summary>
        Task<AudioCostResult> CalculateTextToSpeechCostAsync(
            string provider,
            string model,
            int characterCount,
            string? voice = null,
            string? virtualKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates the cost of real-time audio session.
        /// </summary>
        Task<AudioCostResult> CalculateRealtimeCostAsync(
            string provider,
            string model,
            double inputAudioSeconds,
            double outputAudioSeconds,
            int? inputTokens = null,
            int? outputTokens = null,
            string? virtualKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generic method to calculate audio costs.
        /// </summary>
        Task<AudioCostResult> CalculateAudioCostAsync(
            string provider,
            string operation,
            string model,
            double durationSeconds,
            int characterCount,
            string? virtualKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates a refund for a previous audio transcription operation.
        /// </summary>
        Task<AudioRefundResult> CalculateTranscriptionRefundAsync(
            string provider,
            string model,
            double originalDurationSeconds,
            double refundDurationSeconds,
            string refundReason,
            string? originalTransactionId = null,
            string? virtualKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates a refund for a previous text-to-speech operation.
        /// </summary>
        Task<AudioRefundResult> CalculateTextToSpeechRefundAsync(
            string provider,
            string model,
            int originalCharacterCount,
            int refundCharacterCount,
            string refundReason,
            string? originalTransactionId = null,
            string? voice = null,
            string? virtualKey = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates a refund for a previous real-time audio session.
        /// </summary>
        Task<AudioRefundResult> CalculateRealtimeRefundAsync(
            string provider,
            string model,
            double originalInputAudioSeconds,
            double refundInputAudioSeconds,
            double originalOutputAudioSeconds,
            double refundOutputAudioSeconds,
            int? originalInputTokens = null,
            int? refundInputTokens = null,
            int? originalOutputTokens = null,
            int? refundOutputTokens = null,
            string refundReason = "",
            string? originalTransactionId = null,
            string? virtualKey = null,
            CancellationToken cancellationToken = default);
    }
}