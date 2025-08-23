using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Moq;

namespace ConduitLLM.Tests.Routing
{
    public partial class DefaultLLMRouterTests
    {
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
    }
}