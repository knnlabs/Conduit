using System;
using System.Collections.Generic;
using ConduitLLM.Core.Routing.Strategies;

namespace ConduitLLM.Core.Routing
{
    /// <summary>
    /// Factory for creating model selection strategy instances based on strategy names or enum values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This factory provides a centralized way to create strategy instances based on string names
    /// or enum values. It supports both the enum-based strategy types and legacy string-based strategies
    /// for backward compatibility.
    /// </para>
    /// <para>
    /// The factory uses a cache of strategy instances to avoid creating new instances for commonly
    /// used strategies, improving performance and reducing memory usage.
    /// </para>
    /// </remarks>
    public class ModelSelectionStrategyFactory
    {
        // Cache of strategy instances to avoid recreating them
        private static readonly Dictionary<string, IModelSelectionStrategy> _strategyCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<RoutingStrategy, IModelSelectionStrategy> _enumStrategyCache = new();

        /// <summary>
        /// Gets a model selection strategy based on the strategy name.
        /// </summary>
        /// <param name="strategyName">The name of the strategy to create.</param>
        /// <returns>A model selection strategy instance.</returns>
        /// <remarks>
        /// This method supports both case-insensitive string names (for backward compatibility)
        /// and enum names. If the name is not recognized, it returns the simple strategy as a default.
        /// </remarks>
        public static IModelSelectionStrategy GetStrategy(string strategyName)
        {
            // Check cache first
            if (_strategyCache.TryGetValue(strategyName, out var cachedStrategy))
            {
                return cachedStrategy;
            }

            // Try to parse as enum first
            if (Enum.TryParse<RoutingStrategy>(strategyName, true, out var strategyEnum))
            {
                return GetStrategy(strategyEnum);
            }

            // Fall back to string-based names for backward compatibility
            IModelSelectionStrategy strategy;
            switch (strategyName.ToLowerInvariant())
            {
                case "simple":
                    strategy = new SimpleModelSelectionStrategy();
                    break;
                case "roundrobin":
                    strategy = new RoundRobinModelSelectionStrategy();
                    break;
                case "random":
                    strategy = new RandomModelSelectionStrategy();
                    break;
                case "leastused":
                    strategy = new LeastUsedModelSelectionStrategy();
                    break;
                case "leastcost":
                    strategy = new LeastCostModelSelectionStrategy();
                    break;
                case "leastlatency":
                    strategy = new LeastLatencyModelSelectionStrategy();
                    break;
                case "highestpriority":
                    strategy = new HighestPriorityModelSelectionStrategy();
                    break;
                default:
                    strategy = new SimpleModelSelectionStrategy(); // Default to simple strategy
                    break;
            }

            // Cache the instance
            _strategyCache[strategyName] = strategy;
            return strategy;
        }

        /// <summary>
        /// Gets a model selection strategy based on the strategy enum value.
        /// </summary>
        /// <param name="strategy">The strategy enum value.</param>
        /// <returns>A model selection strategy instance.</returns>
        public static IModelSelectionStrategy GetStrategy(RoutingStrategy strategy)
        {
            // Check cache first
            if (_enumStrategyCache.TryGetValue(strategy, out var cachedStrategy))
            {
                return cachedStrategy;
            }

            // Create new instance
            IModelSelectionStrategy newStrategy;
            switch (strategy)
            {
                case RoutingStrategy.Simple:
                    newStrategy = new SimpleModelSelectionStrategy();
                    break;
                case RoutingStrategy.RoundRobin:
                    newStrategy = new RoundRobinModelSelectionStrategy();
                    break;
                case RoutingStrategy.Random:
                    newStrategy = new RandomModelSelectionStrategy();
                    break;
                case RoutingStrategy.LeastUsed:
                    newStrategy = new LeastUsedModelSelectionStrategy();
                    break;
                case RoutingStrategy.LeastCost:
                    newStrategy = new LeastCostModelSelectionStrategy();
                    break;
                case RoutingStrategy.LeastLatency:
                    newStrategy = new LeastLatencyModelSelectionStrategy();
                    break;
                case RoutingStrategy.HighestPriority:
                    newStrategy = new HighestPriorityModelSelectionStrategy();
                    break;
                case RoutingStrategy.Passthrough:
                    newStrategy = new SimpleModelSelectionStrategy(); // Default for passthrough
                    break;
                default:
                    newStrategy = new SimpleModelSelectionStrategy(); // Default to simple strategy
                    break;
            }

            // Cache the instance
            _enumStrategyCache[strategy] = newStrategy;
            return newStrategy;
        }
    }
}