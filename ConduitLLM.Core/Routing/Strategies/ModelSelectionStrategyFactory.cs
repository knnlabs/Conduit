using System;
using System.Collections.Concurrent;

using ConduitLLM.Core.Interfaces;
namespace ConduitLLM.Core.Routing.Strategies
{
    /// <summary>
    /// Factory for creating and caching model selection strategy instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This factory provides a centralized way to obtain strategy instances based on strategy names.
    /// It caches strategy instances to avoid unnecessary object creation for frequently used strategies.
    /// </para>
    /// <para>
    /// The factory follows the Strategy pattern, allowing the router to dynamically select and use
    /// different model selection algorithms without changing its core logic.
    /// </para>
    /// </remarks>
    public static class ModelSelectionStrategyFactory
    {
        // Cache of strategy instances to avoid creating new instances for each request
        private static readonly ConcurrentDictionary<string, IModelSelectionStrategy> _strategyCache =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets a model selection strategy instance for the specified strategy name.
        /// </summary>
        /// <param name="strategyName">The name of the strategy to get.</param>
        /// <returns>An instance of the requested strategy, or a SimpleModelSelectionStrategy if the strategy is not recognized.</returns>
        /// <remarks>
        /// This method returns cached instances of strategies when possible, creating new ones only when necessary.
        /// If an unrecognized strategy name is provided, it defaults to the "simple" strategy.
        /// </remarks>
        public static IModelSelectionStrategy GetStrategy(string strategyName)
        {
            // If we already have a cached instance for this strategy, return it
            if (_strategyCache.TryGetValue(strategyName, out var existingStrategy))
            {
                return existingStrategy;
            }

            // Create a new strategy instance based on the name
            var newStrategy = CreateStrategy(strategyName);

            // Cache the new instance for future use
            _strategyCache[strategyName] = newStrategy;

            return newStrategy;
        }

        /// <summary>
        /// Creates a new strategy instance based on the strategy name.
        /// </summary>
        /// <param name="strategyName">The name of the strategy to create.</param>
        /// <returns>A new instance of the requested strategy, or a SimpleModelSelectionStrategy if the strategy is not recognized.</returns>
        private static IModelSelectionStrategy CreateStrategy(string strategyName)
        {
            return strategyName.ToLowerInvariant() switch
            {
                "simple" => new SimpleModelSelectionStrategy(),
                "roundrobin" => new RoundRobinModelSelectionStrategy(),
                "leastcost" => new LeastCostModelSelectionStrategy(),
                "leastlatency" => new LeastLatencyModelSelectionStrategy(),
                "priority" => new HighestPriorityModelSelectionStrategy(),
                "random" => new RoundRobinModelSelectionStrategy(), // Random removed, maps to round-robin for load distribution
                "leastused" => new RoundRobinModelSelectionStrategy(), // Maps to round-robin (identical implementation)
                // Default to simple strategy for unrecognized strategy names
                _ => new SimpleModelSelectionStrategy()
            };
        }

        /// <summary>
        /// Clears the strategy cache, forcing new instances to be created on next request.
        /// </summary>
        /// <remarks>
        /// This method is primarily useful for testing or when strategy implementations might change at runtime.
        /// </remarks>
        public static void ClearCache()
        {
            _strategyCache.Clear();
        }
    }
}
