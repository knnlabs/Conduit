using System;
using System.Threading.Tasks;

using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Realtime;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Tracks usage and costs for real-time audio sessions.
    /// </summary>
    public interface IRealtimeUsageTracker
    {
        /// <summary>
        /// Starts tracking usage for a new session.
        /// </summary>
        /// <param name="connectionId">The connection identifier.</param>
        /// <param name="virtualKeyId">The virtual key ID.</param>
        /// <param name="model">The model being used.</param>
        /// <param name="provider">The provider being used.</param>
        /// <returns>A task that completes when tracking is initialized.</returns>
        Task StartTrackingAsync(string connectionId, int virtualKeyId, string model, string provider);

        /// <summary>
        /// Updates usage statistics for an active session.
        /// </summary>
        /// <param name="connectionId">The connection identifier.</param>
        /// <param name="stats">The usage statistics to update.</param>
        /// <returns>A task that completes when the update is recorded.</returns>
        Task UpdateUsageAsync(string connectionId, ConnectionUsageStats stats);

        /// <summary>
        /// Records audio usage for billing purposes.
        /// </summary>
        /// <param name="connectionId">The connection identifier.</param>
        /// <param name="audioSeconds">Duration of audio in seconds.</param>
        /// <param name="isInput">True for input audio, false for output.</param>
        /// <returns>A task that completes when the usage is recorded.</returns>
        Task RecordAudioUsageAsync(string connectionId, double audioSeconds, bool isInput);

        /// <summary>
        /// Records token usage for text portions of the conversation.
        /// </summary>
        /// <param name="connectionId">The connection identifier.</param>
        /// <param name="usage">Token usage information.</param>
        /// <returns>A task that completes when the usage is recorded.</returns>
        Task RecordTokenUsageAsync(string connectionId, Usage usage);

        /// <summary>
        /// Gets the current estimated cost for a session.
        /// </summary>
        /// <param name="connectionId">The connection identifier.</param>
        /// <returns>The estimated cost in the billing currency.</returns>
        Task<decimal> GetEstimatedCostAsync(string connectionId);

        /// <summary>
        /// Finalizes usage tracking for a completed session.
        /// </summary>
        /// <param name="connectionId">The connection identifier.</param>
        /// <param name="finalStats">The final usage statistics.</param>
        /// <returns>The final cost for the session.</returns>
        Task<decimal> FinalizeUsageAsync(string connectionId, ConnectionUsageStats finalStats);

        /// <summary>
        /// Gets detailed usage breakdown for a session.
        /// </summary>
        /// <param name="connectionId">The connection identifier.</param>
        /// <returns>Detailed usage information.</returns>
        Task<RealtimeUsageDetails?> GetUsageDetailsAsync(string connectionId);
    }

    /// <summary>
    /// Detailed usage information for a real-time session.
    /// </summary>
    public class RealtimeUsageDetails
    {
        /// <summary>
        /// The connection identifier.
        /// </summary>
        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>
        /// Total input audio duration in seconds.
        /// </summary>
        public double InputAudioSeconds { get; set; }

        /// <summary>
        /// Total output audio duration in seconds.
        /// </summary>
        public double OutputAudioSeconds { get; set; }

        /// <summary>
        /// Total input tokens (for text/function calls).
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// Total output tokens (for text/function responses).
        /// </summary>
        public int OutputTokens { get; set; }

        /// <summary>
        /// Number of function calls made.
        /// </summary>
        public int FunctionCalls { get; set; }

        /// <summary>
        /// Session duration in seconds.
        /// </summary>
        public double SessionDurationSeconds { get; set; }

        /// <summary>
        /// Cost breakdown by category.
        /// </summary>
        public CostBreakdown Costs { get; set; } = new();

        /// <summary>
        /// When the session started.
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// When the session ended (null if still active).
        /// </summary>
        public DateTime? EndedAt { get; set; }
    }

    /// <summary>
    /// Cost breakdown for real-time usage.
    /// </summary>
    public class CostBreakdown
    {
        /// <summary>
        /// Cost for input audio processing.
        /// </summary>
        public decimal InputAudioCost { get; set; }

        /// <summary>
        /// Cost for output audio generation.
        /// </summary>
        public decimal OutputAudioCost { get; set; }

        /// <summary>
        /// Cost for text token processing.
        /// </summary>
        public decimal TokenCost { get; set; }

        /// <summary>
        /// Cost for function calling.
        /// </summary>
        public decimal FunctionCallCost { get; set; }

        /// <summary>
        /// Any additional fees (connection time, etc.).
        /// </summary>
        public decimal AdditionalFees { get; set; }

        /// <summary>
        /// Total cost.
        /// </summary>
        public decimal Total => InputAudioCost + OutputAudioCost + TokenCost + FunctionCallCost + AdditionalFees;
    }
}
