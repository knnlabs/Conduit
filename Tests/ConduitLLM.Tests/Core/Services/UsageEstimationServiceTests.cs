using System.Text;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public class UsageEstimationServiceTests
    {
        private readonly Mock<ITokenCounter> _mockTokenCounter;
        private readonly Mock<IImageTokenCalculator> _mockImageTokenCalculator;
        private readonly Mock<ILogger<UsageEstimationService>> _mockLogger;
        private readonly UsageEstimationService _service;

        public UsageEstimationServiceTests()
        {
            _mockTokenCounter = new Mock<ITokenCounter>();
            _mockImageTokenCalculator = new Mock<IImageTokenCalculator>();
            _mockLogger = new Mock<ILogger<UsageEstimationService>>();
            _service = new UsageEstimationService(_mockTokenCounter.Object, _mockImageTokenCalculator.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task EstimateUsageFromStreamingResponseAsync_ValidInput_ReturnsEstimatedUsageWithBuffer()
        {
            // Arrange
            var modelId = "gpt-4";
            var inputMessages = new List<Message>
            {
                new Message { Role = "user", Content = "Hello, how are you?" }
            };
            var streamedContent = "I'm doing well, thank you for asking! How can I help you today?";
            
            // Mock token counts (raw, without buffer)
            _mockTokenCounter.Setup(x => x.EstimateTokenCountAsync(modelId, inputMessages))
                .ReturnsAsync(new TokenCount(10, TokenCountFidelity.Exact));
            _mockTokenCounter.Setup(x => x.EstimateTokenCountAsync(modelId, streamedContent))
                .ReturnsAsync(new TokenCount(15, TokenCountFidelity.Exact));

            // Act
            var result = await _service.EstimateUsageFromStreamingResponseAsync(
                modelId, inputMessages, streamedContent);

            // Assert
            Assert.NotNull(result);
            // With 10% buffer: 10 * 1.1 = 11, 15 * 1.1 = 16.5 -> 17
            Assert.Equal(11, result.PromptTokens);
            Assert.Equal(17, result.CompletionTokens);
            Assert.Equal(28, result.TotalTokens);
            
            // Verify token counter was called
            _mockTokenCounter.Verify(x => x.EstimateTokenCountAsync(modelId, inputMessages), Times.Once);
            _mockTokenCounter.Verify(x => x.EstimateTokenCountAsync(modelId, streamedContent), Times.Once);
        }

        [Fact]
        public async Task EstimateUsageFromStreamingResponseAsync_TokenCounterFails_UsesFallbackEstimation()
        {
            // Arrange
            var modelId = "gpt-4";
            var inputMessages = new List<Message>
            {
                new Message { Role = "user", Content = "Test message with 20 characters" } // 31 chars in content
            };
            var streamedContent = "Response content that is forty chars long"; // 42 chars
            
            // Mock token counter to throw exception
            _mockTokenCounter.Setup(x => x.EstimateTokenCountAsync(It.IsAny<string>(), It.IsAny<List<Message>>()))
                .ThrowsAsync(new Exception("Token counter failed"));

            // Act
            var result = await _service.EstimateUsageFromStreamingResponseAsync(
                modelId, inputMessages, streamedContent);

            // Assert
            Assert.NotNull(result);
            // Fallback: ~35 total chars (role + content) / 4 = 8.75 -> 9 tokens, with 10% buffer = 9.9 -> 10
            // Response: 42 chars / 4 = 10.5 -> 11 tokens, with 10% buffer = 12.1 -> 13
            Assert.True(result.PromptTokens > 0);
            Assert.True(result.CompletionTokens > 0);
            Assert.Equal(result.PromptTokens + result.CompletionTokens, result.TotalTokens);
            
            // Verify logger warned about fallback
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to estimate usage")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task EstimateUsageFromTextAsync_ValidInput_ReturnsEstimatedUsage()
        {
            // Arrange
            var modelId = "gpt-3.5-turbo";
            var inputText = "What is the capital of France?";
            var outputText = "The capital of France is Paris.";
            
            _mockTokenCounter.Setup(x => x.EstimateTokenCountAsync(modelId, inputText))
                .ReturnsAsync(new TokenCount(8, TokenCountFidelity.Exact));
            _mockTokenCounter.Setup(x => x.EstimateTokenCountAsync(modelId, outputText))
                .ReturnsAsync(new TokenCount(7, TokenCountFidelity.Exact));

            // Act
            var result = await _service.EstimateUsageFromTextAsync(modelId, inputText, outputText);

            // Assert
            Assert.NotNull(result);
            // With 10% buffer: 8 * 1.1 = 8.8 -> 9, 7 * 1.1 = 7.7 -> 8
            Assert.Equal(9, result.PromptTokens);
            Assert.Equal(8, result.CompletionTokens);
            Assert.Equal(17, result.TotalTokens);
        }

        [Theory]
        [InlineData(null, "input", "output", typeof(ArgumentNullException))]
        [InlineData("", "input", "output", typeof(ArgumentNullException))]
        [InlineData("model", null, "output", typeof(ArgumentException))]
        [InlineData("model", "", "output", typeof(ArgumentException))]
        [InlineData("model", "input", null, typeof(ArgumentException))]
        [InlineData("model", "input", "", typeof(ArgumentException))]
        public async Task EstimateUsageFromTextAsync_InvalidInput_ThrowsCorrectException(
            string modelId, string inputText, string outputText, Type expectedExceptionType)
        {
            // Act & Assert
            await Assert.ThrowsAsync(expectedExceptionType,
                async () => await _service.EstimateUsageFromTextAsync(modelId, inputText, outputText));
        }

        [Fact]
        public async Task EstimateUsageFromStreamingResponseAsync_EmptyMessages_ThrowsArgumentException()
        {
            // Arrange
            var modelId = "gpt-4";
            var emptyMessages = new List<Message>();
            var streamedContent = "Some content";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _service.EstimateUsageFromStreamingResponseAsync(
                    modelId, emptyMessages, streamedContent));
        }

        [Fact]
        public async Task EstimateUsageFromStreamingResponseAsync_MultipartContent_HandlesCorrectly()
        {
            // Arrange
            var modelId = "gpt-4-vision";
            var inputMessages = new List<Message>
            {
                new Message 
                { 
                    Role = "user", 
                    Content = new List<object>
                    {
                        new TextContentPart { Text = "What's in this image?" },
                        new ImageUrlContentPart { ImageUrl = new ImageUrl { Url = "https://example.com/image.jpg" } }
                    }
                }
            };
            var streamedContent = "The image shows a beautiful sunset over the ocean.";
            
            _mockTokenCounter.Setup(x => x.EstimateTokenCountAsync(modelId, inputMessages))
                .ReturnsAsync(new TokenCount(100, TokenCountFidelity.Exact)); // Including image tokens
            _mockImageTokenCalculator.Setup(x => x.CalculateImageTokensAsync(It.IsAny<ImageUrl>()))
                .ReturnsAsync(850); // Standard high-res image tokens
            _mockTokenCounter.Setup(x => x.EstimateTokenCountAsync(modelId, streamedContent))
                .ReturnsAsync(new TokenCount(12, TokenCountFidelity.Exact));

            // Act
            var result = await _service.EstimateUsageFromStreamingResponseAsync(
                modelId, inputMessages, streamedContent);

            // Assert
            Assert.NotNull(result);
            // With 10% buffer: 100 * 1.1 = 110, 12 * 1.1 = 13.2 -> 14
            // Note: Math.Ceiling might round 110.0 to 110 or 111 depending on floating point precision
            Assert.InRange(result.PromptTokens!.Value, 110, 111);
            Assert.Equal(14, result.CompletionTokens);
            Assert.InRange(result.TotalTokens!.Value, 124, 125);
        }

        [Fact]
        public async Task EstimateUsageFromStreamingResponseAsync_LargeContent_AppliesBufferCorrectly()
        {
            // Arrange
            var modelId = "gpt-4";
            var inputMessages = new List<Message>
            {
                new Message { Role = "system", Content = "You are a helpful assistant." },
                new Message { Role = "user", Content = "Write a long essay about technology." }
            };
            var streamedContent = new StringBuilder();
            for (int i = 0; i < 100; i++)
            {
                streamedContent.AppendLine("This is a line of content in the essay about technology and its impact on society.");
            }
            
            _mockTokenCounter.Setup(x => x.EstimateTokenCountAsync(modelId, inputMessages))
                .ReturnsAsync(new TokenCount(50, TokenCountFidelity.Exact));
            _mockTokenCounter.Setup(x => x.EstimateTokenCountAsync(modelId, streamedContent.ToString()))
                .ReturnsAsync(new TokenCount(2000, TokenCountFidelity.Exact));

            // Act
            var result = await _service.EstimateUsageFromStreamingResponseAsync(
                modelId, inputMessages, streamedContent.ToString());

            // Assert
            Assert.NotNull(result);
            // With 10% buffer: 50 * 1.1 = 55, 2000 * 1.1 = 2200
            // Note: Math.Ceiling might round 55.0 to 55 or 56 depending on floating point precision
            Assert.InRange(result.PromptTokens!.Value, 55, 56);
            Assert.Equal(2200, result.CompletionTokens);
            Assert.InRange(result.TotalTokens!.Value, 2255, 2256);
        }

        [Fact]
        public async Task EstimateUsageFromStreamingResponseAsync_ApproximateVocabulary_AppliesLargerBuffer()
        {
            // Arrange: a Claude-style model whose counts come from a stand-in vocabulary.
            var modelId = "claude-model";
            var inputMessages = new List<Message> { new Message { Role = "user", Content = "hello" } };
            var streamedContent = "response";

            _mockTokenCounter.Setup(x => x.EstimateTokenCountAsync(modelId, inputMessages))
                .ReturnsAsync(new TokenCount(100, TokenCountFidelity.ApproximateVocabulary));
            _mockTokenCounter.Setup(x => x.EstimateTokenCountAsync(modelId, streamedContent))
                .ReturnsAsync(new TokenCount(200, TokenCountFidelity.ApproximateVocabulary));

            // Act
            var result = await _service.EstimateUsageFromStreamingResponseAsync(
                modelId, inputMessages, streamedContent);

            // Assert: 20% buffer for the approximate-vocabulary tier
            Assert.Equal(120, result.PromptTokens);
            Assert.Equal(240, result.CompletionTokens);
        }

        [Fact]
        public async Task EstimateUsageFromStreamingResponseAsync_CharacterHeuristic_AppliesLargestBuffer()
        {
            // Arrange: counts that came from the chars/4 fallback (broken vocabulary data).
            var modelId = "degraded-model";
            var inputMessages = new List<Message> { new Message { Role = "user", Content = "hello" } };
            var streamedContent = "response";

            _mockTokenCounter.Setup(x => x.EstimateTokenCountAsync(modelId, inputMessages))
                .ReturnsAsync(new TokenCount(100, TokenCountFidelity.CharacterHeuristic));
            _mockTokenCounter.Setup(x => x.EstimateTokenCountAsync(modelId, streamedContent))
                .ReturnsAsync(new TokenCount(200, TokenCountFidelity.CharacterHeuristic));

            // Act
            var result = await _service.EstimateUsageFromStreamingResponseAsync(
                modelId, inputMessages, streamedContent);

            // Assert: 40% buffer for the character-heuristic tier, which under-counts by 20-40%
            Assert.Equal(140, result.PromptTokens);
            Assert.Equal(280, result.CompletionTokens);
        }
    }
}