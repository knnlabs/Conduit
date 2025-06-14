using System.Collections.Generic;

using ConduitLLM.Core.Models.Routing;
using ConduitLLM.Core.Routing;
using ConduitLLM.Core.Routing.Strategies;

using Xunit;

using CoreFactory = ConduitLLM.Core.Routing.ModelSelectionStrategyFactory;
using CoreRoutingStrategy = ConduitLLM.Core.Routing.RoutingStrategy;

namespace ConduitLLM.Tests.Routing
{
    public class ModelSelectionStrategyTests
    {
        [Fact]
        public void SimpleStrategy_ReturnsFirstAvailableModel()
        {
            // Arrange
            var strategy = new SimpleModelSelectionStrategy();
            var availableModels = new List<string> { "model1", "model2", "model3" };
            var modelDeployments = new Dictionary<string, ModelDeployment>();
            var modelUsageCounts = new Dictionary<string, int>();

            // Act
            var result = strategy.SelectModel(availableModels, modelDeployments, modelUsageCounts);

            // Assert
            Assert.Equal("model1", result);
        }

        [Fact]
        public void RandomStrategy_ReturnsAnyAvailableModel()
        {
            // Arrange
            var strategy = new RandomModelSelectionStrategy();
            var availableModels = new List<string> { "model1", "model2", "model3" };
            var modelDeployments = new Dictionary<string, ModelDeployment>();
            var modelUsageCounts = new Dictionary<string, int>();

            // Act
            var result = strategy.SelectModel(availableModels, modelDeployments, modelUsageCounts);

            // Assert
            Assert.Contains(result, availableModels);
        }

        [Fact]
        public void LeastUsedStrategy_ReturnsModelWithLowestUsageCount()
        {
            // Arrange
            var strategy = new LeastUsedModelSelectionStrategy();
            var availableModels = new List<string> { "model1", "model2", "model3" };
            var modelDeployments = new Dictionary<string, ModelDeployment>();
            var modelUsageCounts = new Dictionary<string, int>
            {
                { "model1", 10 },
                { "model2", 5 },
                { "model3", 15 }
            };

            // Act
            var result = strategy.SelectModel(availableModels, modelDeployments, modelUsageCounts);

            // Assert
            Assert.Equal("model2", result);
        }

        [Fact]
        public void LeastCostStrategy_ReturnsModelWithLowestCost()
        {
            // Arrange
            var strategy = new LeastCostModelSelectionStrategy();
            var availableModels = new List<string> { "model1", "model2", "model3" };
            var modelDeployments = new Dictionary<string, ModelDeployment>
            {
                { "model1", new ModelDeployment { DeploymentName = "model1", InputTokenCostPer1K = 0.02m, OutputTokenCostPer1K = 0.03m } },
                { "model2", new ModelDeployment { DeploymentName = "model2", InputTokenCostPer1K = 0.01m, OutputTokenCostPer1K = 0.02m } },
                { "model3", new ModelDeployment { DeploymentName = "model3", InputTokenCostPer1K = 0.03m, OutputTokenCostPer1K = 0.04m } }
            };
            var modelUsageCounts = new Dictionary<string, int>();

            // Act
            var result = strategy.SelectModel(availableModels, modelDeployments, modelUsageCounts);

            // Assert
            Assert.Equal("model2", result);
        }

        [Fact]
        public void StrategyFactory_StringBased_ReturnsCorrectStrategy()
        {
            // Arrange & Act
            var simpleStrategy = CoreFactory.GetStrategy("simple");
            var roundRobinStrategy = CoreFactory.GetStrategy("roundrobin");
            var randomStrategy = CoreFactory.GetStrategy("random");
            var leastUsedStrategy = CoreFactory.GetStrategy("leastused");

            // Assert
            Assert.IsType<SimpleModelSelectionStrategy>(simpleStrategy);
            Assert.IsType<RoundRobinModelSelectionStrategy>(roundRobinStrategy);
            Assert.IsType<RandomModelSelectionStrategy>(randomStrategy);
            Assert.IsType<LeastUsedModelSelectionStrategy>(leastUsedStrategy);
        }

        [Fact]
        public void StrategyFactory_EnumBased_ReturnsCorrectStrategy()
        {
            // Arrange & Act
            var simpleStrategy = CoreFactory.GetStrategy(CoreRoutingStrategy.Simple);
            var roundRobinStrategy = CoreFactory.GetStrategy(CoreRoutingStrategy.RoundRobin);
            var randomStrategy = CoreFactory.GetStrategy(CoreRoutingStrategy.Random);
            var leastUsedStrategy = CoreFactory.GetStrategy(CoreRoutingStrategy.LeastUsed);

            // Assert
            Assert.IsType<SimpleModelSelectionStrategy>(simpleStrategy);
            Assert.IsType<RoundRobinModelSelectionStrategy>(roundRobinStrategy);
            Assert.IsType<RandomModelSelectionStrategy>(randomStrategy);
            Assert.IsType<LeastUsedModelSelectionStrategy>(leastUsedStrategy);
        }
    }
}
