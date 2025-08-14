namespace ConduitLLM.Core.Models
{
    /// <summary>
    /// Represents cache metrics for a specific model
    /// </summary>
    public class ModelCacheMetrics
    {
        /// <summary>
        /// Number of cache hits for this model
        /// </summary>
        public long Hits { get; set; }

        /// <summary>
        /// Number of cache misses for this model
        /// </summary>
        public long Misses { get; set; }

        /// <summary>
        /// Total retrieval time in milliseconds for this model
        /// </summary>
        public long TotalRetrievalTimeMs { get; set; }

        /// <summary>
        /// Gets the cache hit rate (hits / total requests)
        /// </summary>
        /// <returns>Hit rate as a value between 0 and 1</returns>
        public double GetHitRate()
        {
            long total = Hits + Misses;

            if (total == 0)
                return 0;

            return (double)Hits / total;
        }

        /// <summary>
        /// Gets the average retrieval time in milliseconds
        /// </summary>
        /// <returns>Average retrieval time</returns>
        public double GetAverageRetrievalTimeMs()
        {
            if (Hits == 0)
                return 0;

            return (double)TotalRetrievalTimeMs / Hits;
        }
    }
}