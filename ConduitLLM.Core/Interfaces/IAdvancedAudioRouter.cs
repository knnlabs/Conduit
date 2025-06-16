using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Advanced audio router with support for multiple routing strategies.
    /// </summary>
    public interface IAdvancedAudioRouter : IAudioRouter
    {
        /// <summary>
        /// Gets the available routing strategies.
        /// </summary>
        IReadOnlyList<IAudioRoutingStrategy> RoutingStrategies { get; }

        /// <summary>
        /// Sets the active routing strategy.
        /// </summary>
        /// <param name="strategyName">Name of the strategy to activate.</param>
        void SetRoutingStrategy(string strategyName);

        /// <summary>
        /// Gets the current routing strategy name.
        /// </summary>
        string CurrentStrategyName { get; }

        /// <summary>
        /// Gets provider information for all configured providers.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of provider information.</returns>
        Task<IReadOnlyList<AudioProviderInfo>> GetProviderInfoAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes provider capabilities and metrics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RefreshProviderInfoAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the best provider for a specific language.
        /// </summary>
        /// <param name="language">ISO 639-1 language code.</param>
        /// <param name="requestType">Type of audio request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Provider name or null if none found.</returns>
        Task<string?> GetBestProviderForLanguageAsync(
            string language,
            AudioRequestType requestType,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the best provider based on latency requirements.
        /// </summary>
        /// <param name="maxLatencyMs">Maximum acceptable latency in milliseconds.</param>
        /// <param name="requestType">Type of audio request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Provider name or null if none found.</returns>
        Task<string?> GetLowestLatencyProviderAsync(
            double maxLatencyMs,
            AudioRequestType requestType,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the most cost-effective provider.
        /// </summary>
        /// <param name="requestType">Type of audio request.</param>
        /// <param name="minQualityScore">Minimum acceptable quality score (0-100).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Provider name or null if none found.</returns>
        Task<string?> GetMostCostEffectiveProviderAsync(
            AudioRequestType requestType,
            double minQualityScore = 70,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reports a provider failure for tracking.
        /// </summary>
        /// <param name="provider">Provider that failed.</param>
        /// <param name="errorCode">Error code.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ReportProviderFailureAsync(
            string provider,
            string errorCode,
            CancellationToken cancellationToken = default);
    }
}
