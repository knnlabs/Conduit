using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Routing;
using ConduitLLM.Core.Routing;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Routing
{
    /// <summary>
    /// Unit tests for the DefaultLLMRouter class.
    /// </summary>
    public partial class DefaultLLMRouterTests : TestBase
    {
        private readonly Mock<ILLMClientFactory> _clientFactoryMock;
        private readonly Mock<ILogger<DefaultLLMRouter>> _loggerMock;
        private readonly Mock<IModelCapabilityDetector> _capabilityDetectorMock;
        private readonly Mock<IEmbeddingCache> _embeddingCacheMock;
        private readonly DefaultLLMRouter _router;

        public DefaultLLMRouterTests(ITestOutputHelper output) : base(output)
        {
            _clientFactoryMock = new Mock<ILLMClientFactory>();
            _loggerMock = CreateLogger<DefaultLLMRouter>();
            _capabilityDetectorMock = new Mock<IModelCapabilityDetector>();
            _embeddingCacheMock = new Mock<IEmbeddingCache>();

            _router = new DefaultLLMRouter(
                _clientFactoryMock.Object,
                _loggerMock.Object,
                _capabilityDetectorMock.Object,
                _embeddingCacheMock.Object);
        }





        #region Helper Methods

        private void InitializeRouterWithModels()
        {
            var config = new RouterConfig
            {
                DefaultRoutingStrategy = "simple",
                MaxRetries = 3,
                RetryBaseDelayMs = 100,
                RetryMaxDelayMs = 1000,
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
                        IsHealthy = true,
                        Priority = 2,
                        InputTokenCostPer1K = 0.025m,
                        OutputTokenCostPer1K = 0.05m
                    }
                },
                Fallbacks = new Dictionary<string, List<string>>
                {
                    ["gpt-4"] = new List<string> { "claude-3" }
                }
            };

            _router.Initialize(config);
        }

        private void InitializeRouterWithVisionModels()
        {
            var config = new RouterConfig
            {
                DefaultRoutingStrategy = "simple",
                MaxRetries = 3,
                RetryBaseDelayMs = 100,
                RetryMaxDelayMs = 1000,
                ModelDeployments = new List<ModelDeployment>
                {
                    new ModelDeployment
                    {
                        DeploymentName = "gpt-4",
                        ModelAlias = "openai/gpt-4",
                        IsHealthy = true,
                        Priority = 2
                    },
                    new ModelDeployment
                    {
                        DeploymentName = "gpt-4-vision",
                        ModelAlias = "openai/gpt-4-vision",
                        IsHealthy = true,
                        Priority = 1
                    }
                }
            };

            _router.Initialize(config);
        }

        private void InitializeRouterWithEmbeddingModels()
        {
            var config = new RouterConfig
            {
                DefaultRoutingStrategy = "simple",
                MaxRetries = 3,
                RetryBaseDelayMs = 100,
                RetryMaxDelayMs = 1000,
                ModelDeployments = new List<ModelDeployment>
                {
                    new ModelDeployment
                    {
                        DeploymentName = "text-embedding-ada-002",
                        ModelAlias = "openai/text-embedding-ada-002",
                        IsHealthy = true,
                        Priority = 1,
                        SupportsEmbeddings = true
                    },
                    new ModelDeployment
                    {
                        DeploymentName = "gpt-4",
                        ModelAlias = "openai/gpt-4",
                        IsHealthy = true,
                        Priority = 2,
                        SupportsEmbeddings = false
                    }
                }
            };

            _router.Initialize(config);
        }

        #endregion
    }

    /// <summary>
    /// Extension to convert IEnumerable to IAsyncEnumerable for testing
    /// </summary>
    internal static class AsyncEnumerableExtensions
    {
        public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
        {
            foreach (var item in source)
            {
                yield return item;
                await Task.Yield();
            }
        }
    }
}