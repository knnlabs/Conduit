using System;

using ConduitLLM.Core.Routing.Strategies;

using Xunit;

namespace ConduitLLM.Tests.Routing
{
    public class ModelSelectionStrategyFactoryTests
    {
        [Theory]
        [InlineData("simple", typeof(SimpleModelSelectionStrategy))]
        [InlineData("Simple", typeof(SimpleModelSelectionStrategy))] // Test case insensitivity
        [InlineData("SIMPLE", typeof(SimpleModelSelectionStrategy))]
        [InlineData("roundrobin", typeof(RoundRobinModelSelectionStrategy))]
        [InlineData("leastcost", typeof(LeastCostModelSelectionStrategy))]
        [InlineData("leastlatency", typeof(LeastLatencyModelSelectionStrategy))]
        [InlineData("priority", typeof(HighestPriorityModelSelectionStrategy))]
        [InlineData("random", typeof(RandomModelSelectionStrategy))]
        [InlineData("leastused", typeof(LeastUsedModelSelectionStrategy))]
        [InlineData("unknown", typeof(SimpleModelSelectionStrategy))] // Default for unknown
        public void GetStrategy_ReturnsCorrectStrategyType(string strategyName, Type expectedType)
        {
            // Arrange
            ModelSelectionStrategyFactory.ClearCache(); // Start with fresh cache

            // Act
            var strategy = ModelSelectionStrategyFactory.GetStrategy(strategyName);

            // Assert
            Assert.NotNull(strategy);
            Assert.IsType(expectedType, strategy);
        }

        [Fact]
        public void GetStrategy_CachesInstancesByName()
        {
            // Arrange
            ModelSelectionStrategyFactory.ClearCache(); // Start with fresh cache

            // Act
            var strategy1 = ModelSelectionStrategyFactory.GetStrategy("simple");
            var strategy2 = ModelSelectionStrategyFactory.GetStrategy("simple");

            // Assert
            Assert.NotNull(strategy1);
            Assert.NotNull(strategy2);
            Assert.Same(strategy1, strategy2); // Should be the same instance
        }

        [Fact]
        public void GetStrategy_CreatesDifferentInstancesForDifferentNames()
        {
            // Arrange
            ModelSelectionStrategyFactory.ClearCache(); // Start with fresh cache

            // Act
            var strategy1 = ModelSelectionStrategyFactory.GetStrategy("simple");
            var strategy2 = ModelSelectionStrategyFactory.GetStrategy("roundrobin");

            // Assert
            Assert.NotNull(strategy1);
            Assert.NotNull(strategy2);
            Assert.NotSame(strategy1, strategy2); // Should be different instances
        }

        [Fact]
        public void ClearCache_RemovesCachedInstances()
        {
            // Arrange
            ModelSelectionStrategyFactory.ClearCache(); // Start with fresh cache
            var strategy1 = ModelSelectionStrategyFactory.GetStrategy("simple");

            // Act
            ModelSelectionStrategyFactory.ClearCache();
            var strategy2 = ModelSelectionStrategyFactory.GetStrategy("simple");

            // Assert
            Assert.NotNull(strategy1);
            Assert.NotNull(strategy2);
            Assert.NotSame(strategy1, strategy2); // Should be different instances after cache clear
        }

        [Fact]
        public void GetStrategy_CaseInsensitiveStrategyNames()
        {
            // Arrange
            ModelSelectionStrategyFactory.ClearCache(); // Start with fresh cache

            // Act
            var strategy1 = ModelSelectionStrategyFactory.GetStrategy("simple");
            var strategy2 = ModelSelectionStrategyFactory.GetStrategy("SIMPLE");
            var strategy3 = ModelSelectionStrategyFactory.GetStrategy("Simple");

            // Assert
            Assert.NotNull(strategy1);
            Assert.NotNull(strategy2);
            Assert.NotNull(strategy3);
            Assert.Same(strategy1, strategy2); // Should be the same instance
            Assert.Same(strategy1, strategy3); // Should be the same instance
        }
    }
}
