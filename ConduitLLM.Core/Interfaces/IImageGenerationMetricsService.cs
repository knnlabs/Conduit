using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for tracking and analyzing image generation performance metrics.
    /// </summary>
    public interface IImageGenerationMetricsService
    {
        /// <summary>
        /// Records a new image generation metric.
        /// </summary>
        /// <param name="metric">The metric to record.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RecordMetricAsync(ImageGenerationMetrics metric, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets aggregated statistics for a specific provider and model.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="model">Model name.</param>
        /// <param name="windowMinutes">Time window in minutes for statistics (default 60).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Aggregated statistics for the provider/model.</returns>
        Task<ImageGenerationProviderStats?> GetProviderStatsAsync(
            string provider, 
            string model, 
            int windowMinutes = 60,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets statistics for all available providers and models.
        /// </summary>
        /// <param name="windowMinutes">Time window in minutes for statistics (default 60).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of provider statistics.</returns>
        Task<IReadOnlyList<ImageGenerationProviderStats>> GetAllProviderStatsAsync(
            int windowMinutes = 60,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Selects the optimal provider for image generation based on current performance.
        /// </summary>
        /// <param name="availableProviders">List of available provider/model combinations.</param>
        /// <param name="imageCount">Number of images to generate.</param>
        /// <param name="maxWaitTimeSeconds">Maximum acceptable wait time in seconds (optional).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The optimal provider/model combination or null if none meet criteria.</returns>
        Task<(string Provider, string Model)?> SelectOptimalProviderAsync(
            IEnumerable<(string Provider, string Model)> availableProviders,
            int imageCount,
            double? maxWaitTimeSeconds = null,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Updates the queue depth for a provider.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="model">Model name.</param>
        /// <param name="queueDepth">Current queue depth.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UpdateQueueDepthAsync(
            string provider, 
            string model, 
            int queueDepth,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Marks a provider as unhealthy.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="model">Model name.</param>
        /// <param name="reason">Reason for marking unhealthy.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task MarkProviderUnhealthyAsync(
            string provider, 
            string model, 
            string reason,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Cleans up old metrics data.
        /// </summary>
        /// <param name="olderThanDays">Remove metrics older than this many days.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of metrics removed.</returns>
        Task<int> CleanupOldMetricsAsync(
            int olderThanDays = 7,
            CancellationToken cancellationToken = default);
    }
}