using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Routing
{
    public partial class DefaultLLMRouterTests
    {
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
    }
}