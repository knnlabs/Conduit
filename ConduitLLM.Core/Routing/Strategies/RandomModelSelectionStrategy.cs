using System;
using System.Collections.Generic;

using ConduitLLM.Core.Models.Routing;

namespace ConduitLLM.Core.Routing.Strategies
{
    /// <summary>
    /// A model selection strategy that randomly selects a model from the available options.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This strategy provides simple load balancing by randomly selecting a model for each request.
    /// It doesn't require tracking model usage or other metrics, making it lightweight and simple.
    /// </para>
    /// <para>
    /// Random selection can be useful for quick load distribution across models with similar
    /// characteristics, though it doesn't optimize for any specific factor like cost or latency.
    /// </para>
    /// </remarks>
    public class RandomModelSelectionStrategy : IModelSelectionStrategy
    {
        private readonly Random _random;

        /// <summary>
        /// Initializes a new instance of the <see cref="RandomModelSelectionStrategy"/> class.
        /// </summary>
        public RandomModelSelectionStrategy()
        {
            _random = new Random();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RandomModelSelectionStrategy"/> class with a specific random seed.
        /// </summary>
        /// <param name="seed">The seed to use for the random number generator.</param>
        /// <remarks>Using a specific seed is primarily useful for testing to ensure deterministic behavior.</remarks>
        public RandomModelSelectionStrategy(int seed)
        {
            _random = new Random(seed);
        }

        /// <inheritdoc/>
        public string? SelectModel(
            IReadOnlyList<string> availableModels,
            IReadOnlyDictionary<string, ModelDeployment> modelDeployments,
            IReadOnlyDictionary<string, int> modelUsageCounts)
        {
            if (availableModels.Count == 0)
            {
                return null;
            }

            // Select a random model from the available models
            return availableModels[_random.Next(availableModels.Count)];
        }
    }
}
