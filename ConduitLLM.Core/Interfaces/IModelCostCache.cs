using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Cache interface for Model Cost operations with event-driven invalidation
    /// </summary>
    public interface IModelCostCache
    {
        /// <summary>
        /// Get Model Cost by pattern from cache with database fallback
        /// </summary>
        /// <param name="modelIdPattern">The model ID pattern to look up</param>
        /// <param name="databaseFallback">Function to fetch from database on cache miss</param>
        /// <returns>Model Cost entity or null if not found</returns>
        Task<ModelCost?> GetModelCostByPatternAsync(string modelIdPattern, Func<string, Task<ModelCost?>> databaseFallback);

        // Note: Provider-based lookups are not supported as ModelCost entity doesn't contain provider information
        // This would require joining with ModelProviderMapping or other provider-related entities

        /// <summary>
        /// Get Model Cost for a specific model ID
        /// This method finds the best matching pattern for the given model ID
        /// </summary>
        /// <param name="modelId">The specific model ID to find costs for</param>
        /// <param name="databaseFallback">Function to fetch all costs and find best match</param>
        /// <returns>Best matching Model Cost entity or null if not found</returns>
        Task<ModelCost?> GetModelCostForModelIdAsync(string modelId, Func<string, Task<ModelCost?>> databaseFallback);

        /// <summary>
        /// Invalidate a specific Model Cost in cache
        /// </summary>
        /// <param name="modelCostId">The model cost ID to invalidate</param>
        Task InvalidateModelCostAsync(int modelCostId);

        // Note: Provider-based invalidation is not supported as ModelCost entity doesn't contain provider information

        /// <summary>
        /// Invalidate Model Cost by pattern
        /// </summary>
        /// <param name="modelIdPattern">The model ID pattern to invalidate</param>
        Task InvalidateModelCostByPatternAsync(string modelIdPattern);

        /// <summary>
        /// Clear all Model Cost entries from cache
        /// Used when bulk changes occur or during system reinitialization
        /// </summary>
        Task ClearAllModelCostsAsync();

        /// <summary>
        /// Get cache performance statistics
        /// </summary>
        Task<ModelCostCacheStats> GetStatsAsync();
    }

    /// <summary>
    /// Cache performance statistics for Model Costs
    /// </summary>
    public class ModelCostCacheStats
    {
        public long HitCount { get; set; }
        public long MissCount { get; set; }
        public long InvalidationCount { get; set; }
        public double HitRate => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) : 0;
        public TimeSpan AverageGetTime { get; set; }
        public DateTime LastResetTime { get; set; }
        public long EntryCount { get; set; }
        public long PatternMatchCount { get; set; }
    }
}