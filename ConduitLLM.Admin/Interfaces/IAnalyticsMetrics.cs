using System.Collections.Generic;

namespace ConduitLLM.Admin.Interfaces
{
    /// <summary>
    /// Interface for analytics metrics collection
    /// </summary>
    public interface IAnalyticsMetrics
    {
        /// <summary>
        /// Records a cache hit
        /// </summary>
        /// <param name="cacheKey">The cache key that was hit</param>
        void RecordCacheHit(string cacheKey);

        /// <summary>
        /// Records a cache miss
        /// </summary>
        /// <param name="cacheKey">The cache key that was missed</param>
        void RecordCacheMiss(string cacheKey);

        /// <summary>
        /// Records the duration of an operation
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="durationMs">Duration in milliseconds</param>
        void RecordOperationDuration(string operationName, double durationMs);

        /// <summary>
        /// Records the duration of a data fetch operation
        /// </summary>
        /// <param name="dataSource">Name of the data source</param>
        /// <param name="durationMs">Duration in milliseconds</param>
        void RecordFetchDuration(string dataSource, double durationMs);

        /// <summary>
        /// Records cache memory usage
        /// </summary>
        /// <param name="sizeBytes">Size in bytes</param>
        void RecordCacheMemoryUsage(long sizeBytes);

        /// <summary>
        /// Records a cache invalidation event
        /// </summary>
        /// <param name="reason">Reason for invalidation</param>
        /// <param name="keysInvalidated">Number of keys invalidated</param>
        void RecordCacheInvalidation(string reason, int keysInvalidated);

        /// <summary>
        /// Gets current cache statistics
        /// </summary>
        /// <returns>Dictionary of metric name to value</returns>
        Dictionary<string, object> GetCacheStatistics();

        /// <summary>
        /// Gets operation performance statistics
        /// </summary>
        /// <returns>Dictionary of operation name to average duration</returns>
        Dictionary<string, double> GetOperationStatistics();
    }
}