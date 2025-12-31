using ConduitLLM.Core.Models;
namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for a service that tracks cache metrics
    /// </summary>
    public interface ICacheMetricsService
    {
        /// <summary>
        /// Records a cache hit
        /// </summary>
        /// <param name="retrievalTimeMs">Time taken to retrieve the item from cache in milliseconds</param>
        /// <param name="model">Optional model name for model-specific metrics</param>
        void RecordHit(double retrievalTimeMs, string? model = null);

        /// <summary>
        /// Records a cache miss
        /// </summary>
        /// <param name="model">Optional model name for model-specific metrics</param>
        void RecordMiss(string? model = null);

        /// <summary>
        /// Gets the total number of cache hits
        /// </summary>
        /// <returns>Total hits</returns>
        long GetTotalHits();

        /// <summary>
        /// Gets the total number of cache misses
        /// </summary>
        /// <returns>Total misses</returns>
        long GetTotalMisses();

        /// <summary>
        /// Gets the total number of cache requests (hits + misses)
        /// </summary>
        /// <returns>Total requests</returns>
        long GetTotalRequests();

        /// <summary>
        /// Gets the cache hit rate (hits / total requests)
        /// </summary>
        /// <returns>Hit rate as a value between 0 and 1</returns>
        double GetHitRate();

        /// <summary>
        /// Gets the average retrieval time in milliseconds
        /// </summary>
        /// <returns>Average retrieval time</returns>
        double GetAverageRetrievalTimeMs();

        /// <summary>
        /// Gets metrics for all tracked models
        /// </summary>
        /// <returns>Dictionary of model names to metrics</returns>
        IDictionary<string, ModelCacheMetrics> GetModelMetrics();

        /// <summary>
        /// Gets metrics for a specific model
        /// </summary>
        /// <param name="model">Model name</param>
        /// <returns>Metrics for the model, or null if not tracked</returns>
        ModelCacheMetrics? GetMetricsForModel(string model);

        /// <summary>
        /// Gets a list of all tracked model names
        /// </summary>
        /// <returns>List of model names</returns>
        IList<string> GetTrackedModels();

        /// <summary>
        /// Resets all metrics to zero
        /// </summary>
        void Reset();

        /// <summary>
        /// Imports statistics from another source
        /// </summary>
        /// <param name="hits">Number of hits to import</param>
        /// <param name="misses">Number of misses to import</param>
        /// <param name="avgResponseTimeMs">Average response time in milliseconds</param>
        /// <param name="modelMetrics">Optional model-specific metrics to import</param>
        void ImportStats(long hits, long misses, double avgResponseTimeMs,
            IDictionary<string, ModelCacheMetrics>? modelMetrics = null);
    }
}
