using System;
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
    public class TiktokenCounterTests
    {
        private readonly Mock<ILogger<TiktokenCounter>> _loggerMock;
        private readonly ITokenCounter _tokenCounter;

        public TiktokenCounterTests()
        {
            _loggerMock = new Mock<ILogger<TiktokenCounter>>();
            _tokenCounter = new TiktokenCounter(_loggerMock.Object);
        }

        [Fact]
        public async Task EstimateTokenCountAsync_EmptyMessages_ReturnsZero()
        {
            // Arrange
            var messages = new List<Message>();

            // Act
            var result = await _tokenCounter.EstimateTokenCountAsync("gpt-4", messages);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task EstimateTokenCountAsync_SingleMessage_ReturnsNonZero()
        {
            // Arrange
            var messages = new List<Message>
            {
                new Message
                {
                    Role = MessageRole.User,
                    Content = "Hello, world!"
                }
            };

            // Act
            var result = await _tokenCounter.EstimateTokenCountAsync("gpt-4", messages);

            // Assert - the result should be > 0 regardless of whether we use tiktoken or fallback
            Assert.True(result > 0, $"Expected positive token count, got {result}");
        }

        [Fact]
        public async Task EstimateTokenCountAsync_MultipleMessages_IncreaseTokenCount()
        {
            // Arrange
            var messages = new List<Message>
            {
                new Message
                {
                    Role = MessageRole.System,
                    Content = "You are a helpful assistant."
                },
                new Message
                {
                    Role = MessageRole.User,
                    Content = "Hello, how are you?"
                },
                new Message
                {
                    Role = MessageRole.Assistant,
                    Content = "I'm doing well, thank you for asking. How can I help you today?"
                }
            };

            // Act
            var result = await _tokenCounter.EstimateTokenCountAsync("gpt-4", messages);

            // Assert - token count should be positive
            Assert.True(result > 0, $"Expected positive token count, got {result}");

            // Estimate token count manually to provide a better assertion
            int estimatedFallbackTokens = (
                ("You are a helpful assistant.".Length +
                "Hello, how are you?".Length +
                "I'm doing well, thank you for asking. How can I help you today?".Length +
                "system".Length + "user".Length + "assistant".Length) / 4) + (3 * 5);

            // The actual count should be at least 1/3 of our fallback estimate
            // This allows for variations in tokenization methods
            Assert.True(result >= estimatedFallbackTokens / 3,
                $"Token count {result} is unexpectedly low compared to fallback estimate {estimatedFallbackTokens}");
        }

        [Fact]
        public async Task EstimateTokenCountAsync_LongContent_MoreTokensThanShortContent()
        {
            // Arrange
            var shortMessages = new List<Message>
            {
                new Message
                {
                    Role = MessageRole.User,
                    Content = "Hello"
                }
            };

            var longMessages = new List<Message>
            {
                new Message
                {
                    Role = MessageRole.User,
                    Content = "Hello, this is a much longer message that should definitely use more tokens than the shorter message. " +
                              "It contains multiple sentences and many more words which will increase the token count significantly."
                }
            };

            // Act
            var shortResult = await _tokenCounter.EstimateTokenCountAsync("gpt-4", shortMessages);
            var longResult = await _tokenCounter.EstimateTokenCountAsync("gpt-4", longMessages);

            // Assert
            Assert.True(longResult > shortResult,
                $"Expected longer message ({longResult} tokens) to use more tokens than shorter message ({shortResult} tokens)");
        }

        [Fact]
        public async Task EstimateTokenCountAsync_SingleText_ReturnsNonZero()
        {
            // Arrange
            var text = "Hello, world!";

            // Act
            var result = await _tokenCounter.EstimateTokenCountAsync("gpt-4", text);

            // Assert
            Assert.True(result > 0, $"Expected positive token count, got {result}");
        }
    }
}
