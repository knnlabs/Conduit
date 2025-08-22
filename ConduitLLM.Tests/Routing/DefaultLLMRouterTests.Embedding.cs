using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Moq;

namespace ConduitLLM.Tests.Routing
{
    public partial class DefaultLLMRouterTests
    {
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
    }
}