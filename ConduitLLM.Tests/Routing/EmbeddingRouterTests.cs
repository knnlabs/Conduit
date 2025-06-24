using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Routing;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Routing
{
    /// <summary>
    /// Unit tests for embedding functionality in the LLM router.
    /// </summary>
    public class EmbeddingRouterTests
    {
        private readonly Mock<ILLMClientFactory> _mockClientFactory;
        private readonly Mock<ILogger<DefaultLLMRouter>> _mockLogger;
        private readonly DefaultLLMRouter _router;

        public EmbeddingRouterTests()
        {
            _mockClientFactory = new Mock<ILLMClientFactory>();
            _mockLogger = new Mock<ILogger<DefaultLLMRouter>>();
            _router = new DefaultLLMRouter(_mockClientFactory.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task CreateEmbeddingAsync_Should_Throw_ArgumentNullException_For_Null_Request()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _router.CreateEmbeddingAsync(null!));
        }

        [Fact]
        public async Task CreateEmbeddingAsync_Should_Call_Client_CreateEmbeddingAsync()
        {
            // Arrange
            var request = new EmbeddingRequest
            {
                Input = "Test input",
                Model = "text-embedding-3-small",
                EncodingFormat = "float"
            };

            var expectedResponse = new EmbeddingResponse
            {
                Object = "list",
                Data = new List<EmbeddingData>
                {
                    new EmbeddingData
                    {
                        Object = "embedding",
                        Embedding = new List<float> { 0.1f, 0.2f, 0.3f },
                        Index = 0
                    }
                },
                Model = "text-embedding-3-small",
                Usage = new Usage
                {
                    PromptTokens = 2,
                    CompletionTokens = 0,
                    TotalTokens = 2
                }
            };

            var mockClient = new Mock<ILLMClient>();
            mockClient.Setup(x => x.CreateEmbeddingAsync(request, null, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(expectedResponse);

            _mockClientFactory.Setup(x => x.CreateClient("text-embedding-3-small"))
                             .Returns(mockClient.Object);

            // Act
            var result = await _router.CreateEmbeddingAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedResponse.Object, result.Object);
            Assert.Equal(expectedResponse.Model, result.Model);
            Assert.Single(result.Data);
            Assert.Equal(expectedResponse.Data[0].Embedding.Count, result.Data[0].Embedding.Count);

            // Verify the client was called correctly
            mockClient.Verify(x => x.CreateEmbeddingAsync(request, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateEmbeddingAsync_Should_Handle_Array_Input()
        {
            // Arrange
            var request = new EmbeddingRequest
            {
                Input = new[] { "First text", "Second text" },
                Model = "text-embedding-3-small",
                EncodingFormat = "float"
            };

            var expectedResponse = new EmbeddingResponse
            {
                Object = "list",
                Data = new List<EmbeddingData>
                {
                    new EmbeddingData
                    {
                        Object = "embedding",
                        Embedding = new List<float> { 0.1f, 0.2f, 0.3f },
                        Index = 0
                    },
                    new EmbeddingData
                    {
                        Object = "embedding",
                        Embedding = new List<float> { 0.4f, 0.5f, 0.6f },
                        Index = 1
                    }
                },
                Model = "text-embedding-3-small",
                Usage = new Usage
                {
                    PromptTokens = 4,
                    CompletionTokens = 0,
                    TotalTokens = 4
                }
            };

            var mockClient = new Mock<ILLMClient>();
            mockClient.Setup(x => x.CreateEmbeddingAsync(request, null, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(expectedResponse);

            _mockClientFactory.Setup(x => x.CreateClient("text-embedding-3-small"))
                             .Returns(mockClient.Object);

            // Act
            var result = await _router.CreateEmbeddingAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Data.Count);
            Assert.Equal(0, result.Data[0].Index);
            Assert.Equal(1, result.Data[1].Index);
        }

        [Fact]
        public async Task CreateEmbeddingAsync_Should_Propagate_Client_Exceptions()
        {
            // Arrange
            var request = new EmbeddingRequest
            {
                Input = "Test input",
                Model = "text-embedding-3-small",
                EncodingFormat = "float"
            };

            var mockClient = new Mock<ILLMClient>();
            mockClient.Setup(x => x.CreateEmbeddingAsync(request, null, It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new LLMCommunicationException("Provider unavailable"));

            _mockClientFactory.Setup(x => x.CreateClient("text-embedding-3-small"))
                             .Returns(mockClient.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<LLMCommunicationException>(() =>
                _router.CreateEmbeddingAsync(request));

            Assert.Equal("Provider unavailable", exception.Message);
        }

        [Fact]
        public async Task CreateEmbeddingAsync_Should_Handle_ModelUnavailableException()
        {
            // Arrange
            var request = new EmbeddingRequest
            {
                Input = "Test input",
                Model = "non-existent-model",
                EncodingFormat = "float"
            };

            _mockClientFactory.Setup(x => x.CreateClient("non-existent-model"))
                             .Throws(new ModelUnavailableException("Model not found"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ModelUnavailableException>(() =>
                _router.CreateEmbeddingAsync(request));

            Assert.Equal("Model not found", exception.Message);
        }

        [Fact]
        public async Task CreateEmbeddingAsync_Should_Handle_Custom_ApiKey()
        {
            // Arrange
            var request = new EmbeddingRequest
            {
                Input = "Test input",
                Model = "text-embedding-3-small",
                EncodingFormat = "float"
            };

            var customApiKey = "custom-api-key";

            var expectedResponse = new EmbeddingResponse
            {
                Object = "list",
                Data = new List<EmbeddingData>(),
                Model = "text-embedding-3-small",
                Usage = new Usage()
            };

            var mockClient = new Mock<ILLMClient>();
            mockClient.Setup(x => x.CreateEmbeddingAsync(request, customApiKey, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(expectedResponse);

            _mockClientFactory.Setup(x => x.CreateClient("text-embedding-3-small"))
                             .Returns(mockClient.Object);

            // Act
            var result = await _router.CreateEmbeddingAsync(request, apiKey: customApiKey);

            // Assert
            Assert.NotNull(result);
            mockClient.Verify(x => x.CreateEmbeddingAsync(request, customApiKey, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateEmbeddingAsync_Should_Handle_CancellationToken()
        {
            // Arrange
            var request = new EmbeddingRequest
            {
                Input = "Test input",
                Model = "text-embedding-3-small",
                EncodingFormat = "float"
            };

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel(); // Cancel immediately

            var mockClient = new Mock<ILLMClient>();
            mockClient.Setup(x => x.CreateEmbeddingAsync(request, null, It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new OperationCanceledException());

            _mockClientFactory.Setup(x => x.CreateClient("text-embedding-3-small"))
                             .Returns(mockClient.Object);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _router.CreateEmbeddingAsync(request, cancellationToken: cancellationTokenSource.Token));
        }

        [Theory]
        [InlineData("text-embedding-3-small")]
        [InlineData("text-embedding-3-large")]
        [InlineData("text-embedding-ada-002")]
        public async Task CreateEmbeddingAsync_Should_Support_Different_Models(string modelName)
        {
            // Arrange
            var request = new EmbeddingRequest
            {
                Input = "Test input",
                Model = modelName,
                EncodingFormat = "float"
            };

            var expectedResponse = new EmbeddingResponse
            {
                Object = "list",
                Data = new List<EmbeddingData>(),
                Model = modelName,
                Usage = new Usage()
            };

            var mockClient = new Mock<ILLMClient>();
            mockClient.Setup(x => x.CreateEmbeddingAsync(request, null, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(expectedResponse);

            _mockClientFactory.Setup(x => x.CreateClient(modelName))
                             .Returns(mockClient.Object);

            // Act
            var result = await _router.CreateEmbeddingAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(modelName, result.Model);
        }
    }
}