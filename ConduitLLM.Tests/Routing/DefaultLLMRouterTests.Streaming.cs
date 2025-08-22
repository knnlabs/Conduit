using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Moq;

namespace ConduitLLM.Tests.Routing
{
    public partial class DefaultLLMRouterTests
    {
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
    }
}