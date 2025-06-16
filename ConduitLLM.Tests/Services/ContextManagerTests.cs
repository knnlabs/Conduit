using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Services
{
    public class ContextManagerTests
    {
        private readonly Mock<ITokenCounter> _tokenCounterMock;
        private readonly Mock<ILogger<ContextManager>> _loggerMock;
        private readonly IContextManager _contextManager;

        public ContextManagerTests()
        {
            _tokenCounterMock = new Mock<ITokenCounter>();
            _loggerMock = new Mock<ILogger<ContextManager>>();
            _contextManager = new ContextManager(_tokenCounterMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task ManageContextAsync_NullMaxContextTokens_ReturnsOriginalRequest()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message>
                {
                    new Message { Role = MessageRole.User, Content = "Hello" }
                }
            };

            // Act
            var result = await _contextManager.ManageContextAsync(request, null);

            // Assert
            Assert.Same(request, result);
        }

        [Fact]
        public async Task ManageContextAsync_EmptyMessages_ReturnsOriginalRequest()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message>()
            };

            // Act
            var result = await _contextManager.ManageContextAsync(request, 4000);

            // Assert
            Assert.Same(request, result);
        }

        [Fact]
        public async Task ManageContextAsync_TokensUnderLimit_ReturnsOriginalRequest()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message>
                {
                    new Message { Role = MessageRole.System, Content = "You are a helpful assistant." },
                    new Message { Role = MessageRole.User, Content = "Hello" }
                }
            };

            _tokenCounterMock.Setup(x => x.EstimateTokenCountAsync(request.Model, request.Messages))
                .ReturnsAsync(100);

            // Act
            var result = await _contextManager.ManageContextAsync(request, 4000);

            // Assert
            Assert.Same(request, result);
        }

        [Fact]
        public async Task ManageContextAsync_TokensOverLimit_TrimsMesages()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message>
                {
                    new Message { Role = MessageRole.System, Content = "You are a helpful assistant." },
                    new Message { Role = MessageRole.User, Content = "First message" },
                    new Message { Role = MessageRole.Assistant, Content = "First response" },
                    new Message { Role = MessageRole.User, Content = "Second message" }
                },
                MaxTokens = 500
            };

            _tokenCounterMock.Setup(x => x.EstimateTokenCountAsync(request.Model, request.Messages))
                .ReturnsAsync(2000);

            _tokenCounterMock.Setup(x => x.EstimateTokenCountAsync(request.Model, It.Is<List<Message>>(m => m.Count < request.Messages.Count)))
                .ReturnsAsync(1000);

            // Act
            var result = await _contextManager.ManageContextAsync(request, 1500);

            // Assert
            Assert.NotSame(request, result);
            Assert.Equal(request.Model, result.Model);
            Assert.Equal(request.MaxTokens, result.MaxTokens);
            Assert.True(result.Messages.Count < request.Messages.Count);
        }

        [Fact]
        public async Task ManageContextAsync_PreservesSystemMessages_RemovesOldestNonSystemMessages()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message>
                {
                    new Message { Role = MessageRole.System, Content = "You are a helpful assistant." },
                    new Message { Role = MessageRole.User, Content = "First message" },
                    new Message { Role = MessageRole.Assistant, Content = "First response" },
                    new Message { Role = MessageRole.User, Content = "Second message" },
                    new Message { Role = MessageRole.Assistant, Content = "Second response" }
                }
            };

            _tokenCounterMock.Setup(x => x.EstimateTokenCountAsync(request.Model, request.Messages))
                .ReturnsAsync(2000);

            _tokenCounterMock.Setup(x => x.EstimateTokenCountAsync(request.Model,
                It.Is<List<Message>>(m => m.Count == 4 && m.Exists(msg => msg.Role == MessageRole.System))))
                .ReturnsAsync(1500);

            _tokenCounterMock.Setup(x => x.EstimateTokenCountAsync(request.Model,
                It.Is<List<Message>>(m => m.Count == 3 && m.Exists(msg => msg.Role == MessageRole.System))))
                .ReturnsAsync(900);

            // Act
            var result = await _contextManager.ManageContextAsync(request, 1000);

            // Assert
            Assert.Equal(3, result.Messages.Count);
            Assert.Contains(result.Messages, m => m.Role == MessageRole.System);
            Assert.DoesNotContain(result.Messages, m => (string?)m.Content == "First message");
            Assert.DoesNotContain(result.Messages, m => (string?)m.Content == "First response");
        }

        [Fact]
        public async Task ManageContextAsync_HandlesTrimming_IncludingMaxTokensReservation()
        {
            // Arrange
            var request = new ChatCompletionRequest
            {
                Model = "gpt-4",
                Messages = new List<Message>
                {
                    new Message { Role = MessageRole.System, Content = "You are a helpful assistant." },
                    new Message { Role = MessageRole.User, Content = "First message" },
                    new Message { Role = MessageRole.User, Content = "Second message" }
                },
                MaxTokens = 800 // Reserve tokens for completion
            };

            _tokenCounterMock.Setup(x => x.EstimateTokenCountAsync(request.Model, request.Messages))
                .ReturnsAsync(600);

            // Act
            var result = await _contextManager.ManageContextAsync(request, 1000); // Total context size 1000

            // Assert - should trim messages since context (600) + MaxTokens (800) > 1000
            Assert.NotSame(request, result);
            Assert.True(result.Messages.Count < request.Messages.Count);
        }
    }
}
