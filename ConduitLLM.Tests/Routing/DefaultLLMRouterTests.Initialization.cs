using ConduitLLM.Core.Models.Routing;
using ConduitLLM.Core.Routing;

namespace ConduitLLM.Tests.Routing
{
    public partial class DefaultLLMRouterTests
    {
        #region Initialization Tests

        [Fact]
        public void Constructor_WithNullClientFactory_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new DefaultLLMRouter(null!, _loggerMock.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new DefaultLLMRouter(_clientFactoryMock.Object, null!));
        }

        [Fact]
        public void Initialize_WithNullConfig_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _router.Initialize(null!));
        }

        [Fact]
        public void Initialize_WithValidConfig_SetsUpModelDeployments()
        {
            // Arrange
            var config = new RouterConfig
            {
                DefaultRoutingStrategy = "roundrobin",
                MaxRetries = 5,
                RetryBaseDelayMs = 1000,
                RetryMaxDelayMs = 20000,
                ModelDeployments = new List<ModelDeployment>
                {
                    new ModelDeployment
                    {
                        DeploymentName = "gpt-4",
                        ModelAlias = "openai/gpt-4",
                        IsHealthy = true,
                        Priority = 1,
                        InputTokenCostPer1K = 0.03m,
                        OutputTokenCostPer1K = 0.06m
                    },
                    new ModelDeployment
                    {
                        DeploymentName = "claude-3",
                        ModelAlias = "anthropic/claude-3",
                        IsHealthy = false,
                        Priority = 2
                    }
                },
                Fallbacks = new Dictionary<string, List<string>>
                {
                    ["gpt-4"] = new List<string> { "claude-3", "gpt-3.5-turbo" }
                }
            };

            // Act
            _router.Initialize(config);

            // Assert
            var availableModels = _router.GetAvailableModels();
            Assert.Equal(2, availableModels.Count);
            Assert.Contains("gpt-4", availableModels);
            Assert.Contains("claude-3", availableModels);

            var fallbacks = _router.GetFallbackModels("gpt-4");
            Assert.Equal(2, fallbacks.Count);
            Assert.Equal("claude-3", fallbacks[0]);
            Assert.Equal("gpt-3.5-turbo", fallbacks[1]);
        }

        #endregion
    }
}