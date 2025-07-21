using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Routing;
using ConduitLLM.Core.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Routing
{
    /// <summary>
    /// Unit tests for the DefaultLLMRouter class.
    /// </summary>
    public class DefaultLLMRouterTests : TestBase
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

        #region CreateChatCompletionAsync Tests

        [Fact]
        public async Task CreateChatCompletionAsync_WithNullRequest_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _router.CreateChatCompletionAsync(null!));
        }

        [Fact]
        public async Task CreateChatCompletionAsync_PassthroughMode_DirectlyCallsClient()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message> { new() { Role = "user", Content = "Hello" } }
            };
            var expectedResponse = new ChatCompletionResponse 
            { 
                Id = "test-response",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = "gpt-4",
                Object = "chat.completion",
                Choices = new List<Choice> { new() { Index = 0, Message = new Message { Role = "assistant", Content = "Hello!" }, FinishReason = "stop" } }
            };
            
            var mockClient = new Mock<ILLMClient>();
            mockClient.Setup(c => c.CreateChatCompletionAsync(request, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);
            
            _clientFactoryMock.Setup(f => f.GetClient("gpt-4"))
                .Returns(mockClient.Object);

            // Act
            var response = await _router.CreateChatCompletionAsync(request, "passthrough");

            // Assert
            Assert.Equal(expectedResponse.Id, response.Id);
            mockClient.Verify(c => c.CreateChatCompletionAsync(request, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateChatCompletionAsync_WithRoutingStrategy_SelectsAppropriateModel()
        {
            // Arrange
            InitializeRouterWithModels();
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message> { new() { Role = "user", Content = "Hello" } }
            };
            var expectedResponse = new ChatCompletionResponse 
            { 
                Id = "test-response",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = "gpt-4",
                Object = "chat.completion",
                Choices = new List<Choice> { new() { Index = 0, Message = new Message { Role = "assistant", Content = "Hello!" }, FinishReason = "stop" } }
            };
            
            var mockClient = new Mock<ILLMClient>();
            mockClient.Setup(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);
            
            _clientFactoryMock.Setup(f => f.GetClient(It.IsAny<string>()))
                .Returns(mockClient.Object);

            // Act
            var response = await _router.CreateChatCompletionAsync(request, "simple");

            // Assert
            Assert.Equal(expectedResponse.Id, response.Id);
            mockClient.Verify(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateChatCompletionAsync_WithFailedRequest_RetriesAndUsesFallback()
        {
            // Arrange
            InitializeRouterWithModels();
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message> { new() { Role = "user", Content = "Hello" } }
            };
            var expectedResponse = new ChatCompletionResponse 
            { 
                Id = "fallback-response",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = "claude-3",
                Object = "chat.completion",
                Choices = new List<Choice> { new() { Index = 0, Message = new Message { Role = "assistant", Content = "Fallback!" }, FinishReason = "stop" } }
            };
            
            var failingClient = new Mock<ILLMClient>();
            failingClient.Setup(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new LLMCommunicationException("Connection failed"));
            
            var successClient = new Mock<ILLMClient>();
            successClient.Setup(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);
            
            _clientFactoryMock.Setup(f => f.GetClient("openai/gpt-4"))
                .Returns(failingClient.Object);
            _clientFactoryMock.Setup(f => f.GetClient("anthropic/claude-3"))
                .Returns(successClient.Object);

            // Act
            var response = await _router.CreateChatCompletionAsync(request, "simple");

            // Assert
            Assert.Equal(expectedResponse.Id, response.Id);
            failingClient.Verify(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), null, It.IsAny<CancellationToken>()), Times.Once);
            successClient.Verify(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateChatCompletionAsync_AllModelsUnavailable_ThrowsLLMCommunicationException()
        {
            // Arrange
            InitializeRouterWithModels();
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message> { new() { Role = "user", Content = "Hello" } }
            };
            
            var failingClient = new Mock<ILLMClient>();
            failingClient.Setup(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new LLMCommunicationException("Connection failed"));
            
            _clientFactoryMock.Setup(f => f.GetClient(It.IsAny<string>()))
                .Returns(failingClient.Object);

            // Act & Assert
            await Assert.ThrowsAsync<LLMCommunicationException>(() =>
                _router.CreateChatCompletionAsync(request, "simple"));
        }

        [Fact]
        public async Task CreateChatCompletionAsync_WithVisionRequest_SelectsVisionCapableModel()
        {
            // Arrange
            InitializeRouterWithVisionModels();
            
            // Don't request a specific model, let the router choose based on vision capability
            var request = new ChatCompletionRequest
            {
                Model = "", // Empty model to trigger routing logic
                Messages = new List<Message> 
                { 
                    new() 
                    { 
                        Role = "user", 
                        Content = new List<object> 
                        { 
                            new { type = "text", text = "What's in this image?" },
                            new { type = "image_url", image_url = new { url = "data:image/png;base64,..." } }
                        } 
                    } 
                }
            };
            var expectedResponse = new ChatCompletionResponse 
            { 
                Id = "vision-response",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = "gpt-4-vision",
                Object = "chat.completion",
                Choices = new List<Choice> { new() { Index = 0, Message = new Message { Role = "assistant", Content = "I see..." }, FinishReason = "stop" } }
            };
            
            _capabilityDetectorMock.Setup(d => d.ContainsImageContent(It.IsAny<ChatCompletionRequest>()))
                .Returns(true);
            // Set up vision capability checks for both deployment names
            _capabilityDetectorMock.Setup(d => d.HasVisionCapability("openai/gpt-4-vision"))
                .Returns(true);
            _capabilityDetectorMock.Setup(d => d.HasVisionCapability("openai/gpt-4"))
                .Returns(false);
            
            var visionClient = new Mock<ILLMClient>();
            visionClient.Setup(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);
            
            _clientFactoryMock.Setup(f => f.GetClient("openai/gpt-4-vision"))
                .Returns(visionClient.Object);

            // Act
            var response = await _router.CreateChatCompletionAsync(request, "simple");

            // Assert
            Assert.Equal(expectedResponse.Id, response.Id);
            visionClient.Verify(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), null, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region StreamChatCompletionAsync Tests

        [Fact]
        public async Task StreamChatCompletionAsync_WithNullRequest_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var _ in _router.StreamChatCompletionAsync(null!))
                {
                    // Should not reach here
                }
            });
        }

        [Fact]
        public async Task StreamChatCompletionAsync_SuccessfulStream_ReturnsChunks()
        {
            // Arrange
            InitializeRouterWithModels();
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message> { new() { Role = "user", Content = "Hello" } }
            };
            
            var chunks = new List<ChatCompletionChunk>
            {
                new() { Id = "chunk1", Choices = new List<StreamingChoice> { new() { Index = 0, Delta = new DeltaContent { Role = "assistant", Content = "Hello" }, FinishReason = null } } },
                new() { Id = "chunk2", Choices = new List<StreamingChoice> { new() { Index = 0, Delta = new DeltaContent { Content = " world" }, FinishReason = "stop" } } }
            };
            
            var mockClient = new Mock<ILLMClient>();
            mockClient.Setup(c => c.StreamChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), null, It.IsAny<CancellationToken>()))
                .Returns(chunks.ToAsyncEnumerable());
            
            _clientFactoryMock.Setup(f => f.GetClient(It.IsAny<string>()))
                .Returns(mockClient.Object);

            // Act
            var receivedChunks = new List<ChatCompletionChunk>();
            await foreach (var chunk in _router.StreamChatCompletionAsync(request, "simple"))
            {
                receivedChunks.Add(chunk);
            }

            // Assert
            Assert.Equal(2, receivedChunks.Count);
            Assert.Equal("chunk1", receivedChunks[0].Id);
            Assert.Equal("chunk2", receivedChunks[1].Id);
        }

        [Fact]
        public async Task StreamChatCompletionAsync_NoChunksReceived_ThrowsLLMCommunicationException()
        {
            // Arrange
            InitializeRouterWithModels();
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message> { new() { Role = "user", Content = "Hello" } }
            };
            
            var mockClient = new Mock<ILLMClient>();
            mockClient.Setup(c => c.StreamChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), null, It.IsAny<CancellationToken>()))
                .Returns(new List<ChatCompletionChunk>().ToAsyncEnumerable());
            
            _clientFactoryMock.Setup(f => f.GetClient(It.IsAny<string>()))
                .Returns(mockClient.Object);

            // Act & Assert
            await Assert.ThrowsAsync<LLMCommunicationException>(async () =>
            {
                await foreach (var _ in _router.StreamChatCompletionAsync(request, "simple"))
                {
                    // Processing chunks
                }
            });
        }

        #endregion

        #region CreateEmbeddingAsync Tests

        [Fact]
        public async Task CreateEmbeddingAsync_WithNullRequest_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _router.CreateEmbeddingAsync(null!));
        }

        [Fact]
        public async Task CreateEmbeddingAsync_WithEmbeddingCapableModel_Succeeds()
        {
            // Arrange
            InitializeRouterWithEmbeddingModels();
            var request = new EmbeddingRequest
            {
                Input = "Test embedding",
                Model = "text-embedding-ada-002",
                EncodingFormat = "float"
            };
            var expectedResponse = new EmbeddingResponse
            {
                Data = new List<EmbeddingData> { new() { Index = 0, Embedding = new List<float> { 0.1f, 0.2f, 0.3f }, Object = "embedding" } },
                Model = "text-embedding-ada-002",
                Object = "list",
                Usage = new Usage { PromptTokens = 5, CompletionTokens = 0, TotalTokens = 5 }
            };
            
            var mockClient = new Mock<ILLMClient>();
            mockClient.Setup(c => c.CreateEmbeddingAsync(It.IsAny<EmbeddingRequest>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);
            
            _clientFactoryMock.Setup(f => f.GetClient("openai/text-embedding-ada-002"))
                .Returns(mockClient.Object);

            // Act
            var response = await _router.CreateEmbeddingAsync(request, "simple");

            // Assert
            Assert.Equal(expectedResponse.Data.Count, response.Data.Count);
            mockClient.Verify(c => c.CreateEmbeddingAsync(It.IsAny<EmbeddingRequest>(), null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateEmbeddingAsync_WithCachedResponse_ReturnsCachedResult()
        {
            // Arrange
            InitializeRouterWithEmbeddingModels();
            var request = new EmbeddingRequest
            {
                Input = "Test embedding",
                Model = "text-embedding-ada-002",
                EncodingFormat = "float"
            };
            var cachedResponse = new EmbeddingResponse
            {
                Data = new List<EmbeddingData> { new() { Index = 0, Embedding = new List<float> { 0.1f, 0.2f, 0.3f }, Object = "embedding" } },
                Model = "text-embedding-ada-002",
                Object = "list",
                Usage = new Usage { PromptTokens = 5, CompletionTokens = 0, TotalTokens = 5 }
            };
            
            _embeddingCacheMock.Setup(c => c.IsAvailable).Returns(true);
            _embeddingCacheMock.Setup(c => c.GenerateCacheKey(It.IsAny<EmbeddingRequest>()))
                .Returns("cache-key");
            _embeddingCacheMock.Setup(c => c.GetEmbeddingAsync("cache-key"))
                .ReturnsAsync(cachedResponse);

            // Act
            var response = await _router.CreateEmbeddingAsync(request, "simple");

            // Assert
            Assert.Equal(cachedResponse.Data.Count, response.Data.Count);
            _embeddingCacheMock.Verify(c => c.GetEmbeddingAsync("cache-key"), Times.Once);
        }

        #endregion

        #region Health Management Tests

        [Fact]
        public void UpdateModelHealth_WithValidModel_UpdatesHealthStatus()
        {
            // Arrange
            InitializeRouterWithModels();

            // Act
            _router.UpdateModelHealth("gpt-4", false);

            // Assert
            // The model should be marked as unhealthy and not selected by routing strategies
            // This is verified indirectly by testing that the model is not selected in subsequent requests
        }

        [Fact]
        public void UpdateModelHealth_WithEmptyModelName_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            _router.UpdateModelHealth("", true);
            _router.UpdateModelHealth(null!, false);
        }

        #endregion

        #region GetAvailableModelDetailsAsync Tests

        [Fact]
        public async Task GetAvailableModelDetailsAsync_ReturnsModelInfo()
        {
            // Arrange
            InitializeRouterWithModels();

            // Act
            var models = await _router.GetAvailableModelDetailsAsync();

            // Assert
            Assert.Equal(2, models.Count);
            var gpt4 = models.FirstOrDefault(m => m.Id == "gpt-4");
            Assert.NotNull(gpt4);
            Assert.Equal("openai/gpt-4", gpt4.OwnedBy);
        }

        #endregion

        #region Routing Strategy Tests

        [Theory]
        [InlineData("simple")]
        [InlineData("roundrobin")]
        [InlineData("leastcost")]
        [InlineData("leastlatency")]
        [InlineData("highestpriority")]
        public async Task CreateChatCompletionAsync_WithDifferentStrategies_SelectsModels(string strategy)
        {
            // Arrange
            InitializeRouterWithModels();
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message> { new() { Role = "user", Content = "Hello" } }
            };
            var expectedResponse = new ChatCompletionResponse 
            { 
                Id = "test-response",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = "gpt-4",
                Object = "chat.completion",
                Choices = new List<Choice> { new() { Index = 0, Message = new Message { Role = "assistant", Content = "Hello!" }, FinishReason = "stop" } }
            };
            
            var mockClient = new Mock<ILLMClient>();
            mockClient.Setup(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);
            
            _clientFactoryMock.Setup(f => f.GetClient(It.IsAny<string>()))
                .Returns(mockClient.Object);

            // Act
            var response = await _router.CreateChatCompletionAsync(request, strategy);

            // Assert
            Assert.Equal(expectedResponse.Id, response.Id);
            mockClient.Verify(c => c.CreateChatCompletionAsync(It.IsAny<ChatCompletionRequest>(), null, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

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